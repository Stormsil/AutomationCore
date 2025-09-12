// ====================================
// WindowsGraphicsCapture (DX11-only)
// Захват окна/экрана через Windows.Graphics.Capture
// ====================================

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using Windows.Graphics;                           // SizeInt32
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;                                     // MarshalInterface<T>.FromAbi

// SharpDX алиасы (исключаем неоднозначности DXGI/D3D11)
using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;

namespace AutomationCore.Capture
{
    /// <summary>
    /// Захват экрана через Windows Graphics Capture API (WGC) на чистом D3D11.
    /// Поддерживает захват конкретного окна (в т.ч. перекрытого/свернутого) и всего экрана.
    /// Возвращает одиночный кадр как <see cref="Bitmap"/>.
    /// </summary>
    public class WindowsGraphicsCapture : IDisposable
    {
        private static readonly Guid IID_IInspectable = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");

        // D3D11
        private D3D11.Device _d3d11Device;
        private D3D11.DeviceContext _d3d11Context;

        // WinRT-обёртка D3D устройства для WGC
        private IDirect3DDevice _winrtD3DDevice;

        // WGC
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureItem _captureItem;
        private SizeInt32 _poolSize;

        // ожидание одного кадра
        private TaskCompletionSource<Bitmap> _frameTcs;
        private readonly object _lock = new();

        /// <summary>Проверяет, поддерживается ли WGC на текущей системе.</summary>
        public static bool IsSupported()
        {
            try { return GraphicsCaptureSession.IsSupported(); }
            catch { return false; }
        }

        /// <summary>
        /// Инициализирует захват. Если <paramref name="windowHandle"/> == IntPtr.Zero — захват основного монитора.
        /// </summary>
        /// <param name="windowHandle">HWND окна-источника или IntPtr.Zero для монитора.</param>
        public Task InitializeAsync(IntPtr windowHandle)
        {
            // --- D3D11 устройство/контекст (BGRA для совместимости с WIC/GDI+) ---
            _d3d11Device = new D3D11.Device(D3D.DriverType.Hardware, D3D11.DeviceCreationFlags.BgraSupport);
            _d3d11Context = _d3d11Device.ImmediateContext;

            // --- WinRT IDirect3DDevice для WGC ---
            using (var dxgiDevice = _d3d11Device.QueryInterface<DXGI.Device>())
            {
                CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var raw);
                _winrtD3DDevice = MarshalInterface<IDirect3DDevice>.FromAbi(raw);
                Marshal.Release(raw);
            }

            // --- Источник захвата ---
            _captureItem = windowHandle != IntPtr.Zero
                ? CreateCaptureItemForWindow(windowHandle)
                : CreateCaptureItemForMonitor();

            if (_captureItem == null)
                throw new InvalidOperationException("Не удалось создать GraphicsCaptureItem.");

            _poolSize = _captureItem.Size;

            // --- Пул кадров (без создания сессии тут) ---
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtD3DDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                _poolSize);

            _framePool.FrameArrived += OnFrameArrived;
            return Task.CompletedTask;
        }

        /// <summary>Захватывает один кадр из текущего источника (окно или экран).</summary>
        public async Task<Bitmap> CaptureFrameAsync()
        {
            if (_framePool == null || _captureItem == null)
                throw new InvalidOperationException("Сначала вызовите InitializeAsync().");

            // TCS под первый кадр
            lock (_lock)
            {
                _frameTcs = new TaskCompletionSource<Bitmap>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            // Локальная одноразовая сессия — это устраняет гонки/0x8000FFFF
            using (var session = _framePool.CreateCaptureSession(_captureItem))
            {
                session.StartCapture();

                var captureTask = _frameTcs.Task;
                var timeoutTask = Task.Delay(5000);

                var completed = await Task.WhenAny(captureTask, timeoutTask).ConfigureAwait(false);
                if (completed == timeoutTask)
                    throw new TimeoutException("Таймаут при захвате кадра.");

                return await captureTask.ConfigureAwait(false);
            }
        }

        /// <summary>Захватывает один кадр всего экрана (основного монитора).</summary>
        public async Task<Bitmap> CaptureScreenAsync()
        {
            _captureItem = CreateCaptureItemForMonitor();
            if (_captureItem == null)
                throw new InvalidOperationException("Не удалось создать GraphicsCaptureItem для монитора.");

            _poolSize = _captureItem.Size;

            _framePool?.Dispose();
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtD3DDevice,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                1,
                _poolSize);

            _framePool.FrameArrived += OnFrameArrived;
            return await CaptureFrameAsync().ConfigureAwait(false);
        }

        // --------------------------------------------------
        // FrameArrived: берём кадр, следим за resize, Bitmap
        // --------------------------------------------------
        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            // реагируем на изменение размера источника до копирования
            if (frame.ContentSize.Width != _poolSize.Width ||
                frame.ContentSize.Height != _poolSize.Height)
            {
                _poolSize = frame.ContentSize;
                sender.Recreate(_winrtD3DDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, _poolSize);
            }

            var bitmap = ConvertFrameToBitmap(frame);

            lock (_lock)
            {
                _frameTcs?.TrySetResult(bitmap);
            }
        }

        // --------------------------------------------
        // Конвертация кадра (ID3D11Texture2D -> Bitmap)
        // --------------------------------------------
        private Bitmap ConvertFrameToBitmap(Direct3D11CaptureFrame frame)
        {
            var src = GetSharpDXTexture(frame.Surface);

            int width = frame.ContentSize.Width;
            int height = frame.ContentSize.Height;

            var stagingDesc = new D3D11.Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = src.Description.Format,                 // обычно B8G8R8A8_UNorm
                SampleDescription = new DXGI.SampleDescription(1, 0),       // без MSAA
                Usage = D3D11.ResourceUsage.Staging,
                BindFlags = D3D11.BindFlags.None,
                CpuAccessFlags = D3D11.CpuAccessFlags.Read,
                OptionFlags = D3D11.ResourceOptionFlags.None
            };

            using var staging = new D3D11.Texture2D(_d3d11Device, stagingDesc);

            var region = new D3D11.ResourceRegion(0, 0, 0, width, height, 1);
            _d3d11Context.CopySubresourceRegion(src, 0, region, staging, 0);

            var box = _d3d11Context.MapSubresource(staging, 0, D3D11.MapMode.Read, D3D11.MapFlags.None);
            try
            {
                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                var rect = new Rectangle(0, 0, width, height);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);
                try
                {
                    var srcPtr = box.DataPointer;
                    var dstPtr = data.Scan0;
                    int rowBytes = width * 4;

                    for (int y = 0; y < height; y++)
                    {
                        CopyMemory(dstPtr, srcPtr, (UIntPtr)rowBytes);
                        srcPtr = IntPtr.Add(srcPtr, box.RowPitch);
                        dstPtr = IntPtr.Add(dstPtr, data.Stride);
                    }
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return bmp;
            }
            finally
            {
                _d3d11Context.UnmapSubresource(staging, 0);
            }
        }

        // Получить SharpDX D3D11 Texture2D из WinRT IDirect3DSurface
        private D3D11.Texture2D GetSharpDXTexture(IDirect3DSurface surface)
        {
            try
            {
                var access = WinRT.CastExtensions.As<IDirect3DDxgiInterfaceAccess>(surface);
                var guid = typeof(D3D11.Texture2D).GUID;
                access.GetInterface(ref guid, out var raw);
                return new D3D11.Texture2D(raw);
            }
            catch
            {
                // Fallback через COM
                IntPtr unk = IntPtr.Zero, acc = IntPtr.Zero;
                try
                {
                    unk = Marshal.GetIUnknownForObject(surface);
                    var iid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
                    Marshal.QueryInterface(unk, ref iid, out acc);
                    var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(acc);

                    var guid = typeof(D3D11.Texture2D).GUID;
                    access.GetInterface(ref guid, out var raw);
                    return new D3D11.Texture2D(raw);
                }
                finally
                {
                    if (acc != IntPtr.Zero) Marshal.Release(acc);
                    if (unk != IntPtr.Zero) Marshal.Release(unk);
                }
            }
        }

        // ---- Создание GraphicsCaptureItem (окно/монитор) через RoGetActivationFactory ----

        private GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr hwnd)
        {
            var interop = GetActivationFactory<IGraphicsCaptureItemInterop>("Windows.Graphics.Capture.GraphicsCaptureItem");
            var iid = IID_IInspectable; // локальная копия для ref
            interop.CreateForWindow(hwnd, ref iid, out var ptr);
            var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(ptr);
            Marshal.Release(ptr);
            return item;
        }

        private GraphicsCaptureItem CreateCaptureItemForMonitor()
        {
            var interop = GetActivationFactory<IGraphicsCaptureItemInterop>("Windows.Graphics.Capture.GraphicsCaptureItem");
            var monitor = MonitorFromWindow(GetDesktopWindow(), MONITOR_DEFAULTTOPRIMARY);
            var iid = IID_IInspectable; // локальная копия для ref
            interop.CreateForMonitor(monitor, ref iid, out var ptr);
            var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(ptr);
            Marshal.Release(ptr);
            return item;
        }

        /// <summary>Освобождение ресурсов.</summary>
        public void Dispose()
        {
            if (_framePool != null)
            {
                _framePool.FrameArrived -= OnFrameArrived;
                _framePool.Dispose();
                _framePool = null;
            }

            _captureItem = null;
            _winrtD3DDevice = null;

            _d3d11Context?.Dispose();
            _d3d11Context = null;

            _d3d11Device?.Dispose();
            _d3d11Device = null;
        }

        // ============================
        //    P/Invoke / Interop
        // ============================

        // WinRT Activation (RoGetActivationFactory)
        [DllImport("combase.dll")]
        private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string source, int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr hstring, ref Guid iid, out IntPtr factory);

        private static TFactory GetActivationFactory<TFactory>(string runtimeClass)
        {
            IntPtr hstr = IntPtr.Zero;
            IntPtr pFactory = IntPtr.Zero;
            try
            {
                int hr = WindowsCreateString(runtimeClass, runtimeClass.Length, out hstr);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                var iid = typeof(TFactory).GUID;
                hr = RoGetActivationFactory(hstr, ref iid, out pFactory);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);

                return (TFactory)Marshal.GetObjectForIUnknown(pFactory);
            }
            finally
            {
                if (pFactory != IntPtr.Zero) Marshal.Release(pFactory);
                if (hstr != IntPtr.Zero) WindowsDeleteString(hstr);
            }
        }

        // WGC helper: CreateDirect3D11DeviceFromDXGIDevice
        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", SetLastError = true)]
        private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

        // Быстрый memcopy (на некоторых системах CopyMemory не экспортируется)
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory", SetLastError = false)]
        private static extern void CopyMemory(IntPtr dest, IntPtr src, UIntPtr count);

        // Win32 для монитор/окно
        [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
        [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);
        private const uint MONITOR_DEFAULTTOPRIMARY = 1;

        // Интерфейсы interop
        [ComImport]
        [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IGraphicsCaptureItemInterop
        {
            IntPtr CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);
            IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
        }

        [ComImport]
        [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IDirect3DDxgiInterfaceAccess
        {
            IntPtr GetInterface(ref Guid iid, out IntPtr p);
        }
    }
}
