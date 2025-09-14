// Infrastructure/Capture/WindowsGraphicsCapture/WgcDevice.cs
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
using Windows.Graphics.Capture;
using WinRT;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;
using D3D = SharpDX.Direct3D;

namespace AutomationCore.Infrastructure.Capture.WindowsGraphicsCapture
{
    /// <summary>
    /// Windows Graphics Capture устройство - низкоуровневый компонент для создания сессий WGC
    /// </summary>
    public sealed class WgcDevice : ICaptureDevice
    {
        private static readonly Guid IID_IInspectable = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");

        private D3D11.Device? _d3d11Device;
        private IDirect3DDevice? _winrtD3DDevice;
        private bool _disposed;

        public bool IsSupported
        {
            get
            {
                try
                {
                    return GraphicsCaptureSession.IsSupported();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Создает новую сессию захвата
        /// </summary>
        public async ValueTask<ICaptureDeviceSession> CreateSessionAsync(CaptureRequest request, CancellationToken ct = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WgcDevice));

            if (!IsSupported)
                throw new NotSupportedException("Windows Graphics Capture is not supported on this system");

            await EnsureInitializedAsync(ct);

            return request.Target switch
            {
                WindowCaptureTarget windowTarget => await CreateWindowSessionAsync(windowTarget, request.Options, ct),
                ScreenCaptureTarget screenTarget => await CreateScreenSessionAsync(screenTarget, request.Options, ct),
                _ => throw new NotSupportedException($"Capture target type {request.Target.GetType().Name} is not supported by WGC")
            };
        }

        private async ValueTask EnsureInitializedAsync(CancellationToken ct)
        {
            if (_d3d11Device != null && _winrtD3DDevice != null)
                return;

            await Task.Run(() =>
            {
                InitializeD3D11();
            }, ct);
        }

        private void InitializeD3D11()
        {
            if (_d3d11Device != null)
                return;

            var flags = D3D11.DeviceCreationFlags.BgraSupport | D3D11.DeviceCreationFlags.VideoSupport;

            _d3d11Device = new D3D11.Device(D3D.DriverType.Hardware, flags);

            // Создаем WinRT устройство из D3D11
            using var dxgiDevice = _d3d11Device.QueryInterface<DXGI.Device>();
            CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var raw);
            _winrtD3DDevice = MarshalInterface<IDirect3DDevice>.FromAbi(raw);
            Marshal.Release(raw);
        }

        private async ValueTask<ICaptureDeviceSession> CreateWindowSessionAsync(
            WindowCaptureTarget target,
            CaptureOptions options,
            CancellationToken ct)
        {
            var captureItem = await Task.Run(() => CreateCaptureItemForWindow(target.Handle.Value), ct);
            if (captureItem == null)
                throw new InvalidOperationException($"Failed to create capture item for window {target.Handle.Value}");

            return new WgcSession(_d3d11Device!, _winrtD3DDevice!, captureItem, target, options);
        }

        private async ValueTask<ICaptureDeviceSession> CreateScreenSessionAsync(
            ScreenCaptureTarget target,
            CaptureOptions options,
            CancellationToken ct)
        {
            var captureItem = await Task.Run(() => CreateCaptureItemForMonitor(target.MonitorIndex), ct);
            if (captureItem == null)
                throw new InvalidOperationException($"Failed to create capture item for monitor {target.MonitorIndex}");

            return new WgcSession(_d3d11Device!, _winrtD3DDevice!, captureItem, target, options);
        }

        private GraphicsCaptureItem? CreateCaptureItemForWindow(IntPtr windowHandle)
        {
            try
            {
                var factory = WinRT.WindowsRuntimeMarshal.GetActivationFactory(typeof(GraphicsCaptureItem));
                var interop = factory.As<IGraphicsCaptureItemInterop>();

                var hr = interop.CreateForWindow(windowHandle, IID_IInspectable, out var raw);
                if (hr < 0)
                    return null;

                var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(raw);
                Marshal.Release(raw);
                return item;
            }
            catch
            {
                return null;
            }
        }

        private GraphicsCaptureItem? CreateCaptureItemForMonitor(int monitorIndex)
        {
            try
            {
                // Заглушка - нужно реализовать получение доступных элементов захвата
                var displays = new List<GraphicsCaptureItem>();
                if (monitorIndex < 0 || monitorIndex >= displays.Count)
                    return null;

                return displays[monitorIndex];
            }
            catch
            {
                return null;
            }
        }

        #region P/Invoke Declarations

        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            int CreateForWindow(IntPtr window, Guid riid, out IntPtr result);
            int CreateForMonitor(IntPtr monitor, Guid riid, out IntPtr result);
        }

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        #endregion

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                _winrtD3DDevice?.Dispose();
                _d3d11Device?.Dispose();
            }
            catch
            {
                // Игнорируем ошибки при освобождении ресурсов
            }

            _winrtD3DDevice = null;
            _d3d11Device = null;
            _disposed = true;
        }
    }
}