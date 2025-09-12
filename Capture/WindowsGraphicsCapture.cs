// ====================================
// Enhanced WindowsGraphicsCapture (refactored)
// Расширенный захват с поддержкой пула буферов, переиспользования staging и стабильных событий
// ====================================

using OpenCvSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

using D3D = SharpDX.Direct3D;
using D3D11 = SharpDX.Direct3D11;
using DXGI = SharpDX.DXGI;

namespace AutomationCore.Capture
{
    /// <summary>
    /// Расширенный класс захвата экрана через Windows Graphics Capture API.
    /// Поддерживает потоковый захват, слежение за окнами, различные форматы вывода.
    /// </summary>
    public class EnhancedWindowsGraphicsCapture : IDisposable
    {
        #region Constants & Fields

        private static readonly Guid IID_IInspectable = new("AF86E2E0-B12D-4C6A-9C5A-D7AA65101E90");

        // D3D11 Resources
        private D3D11.Device _d3d11Device;
        private D3D11.DeviceContext _d3d11Context;
        private IDirect3DDevice _winrtD3DDevice;

        // Staging для чтения в системную память (переиспользуем!)
        private D3D11.Texture2D _staging;

        // WGC Resources
        private Direct3D11CaptureFramePool _framePool;
        private GraphicsCaptureItem _captureItem;
        private GraphicsCaptureSession _captureSession;
        private SizeInt32 _poolSize;

        // Capture State
        private IntPtr _windowHandle;
        private bool _isCapturing;
        private CancellationTokenSource _captureCts;
        private readonly object _lock = new();

        // Window Tracking
        private System.Threading.Timer _windowTracker;
        private Rectangle _lastWindowRect;

        // Performance Metrics
        private readonly CaptureMetrics _metrics = new();
        private DateTime _lastFrameTime = DateTime.UtcNow;

        // Settings
        private readonly CaptureSettings _settings;

        #endregion

        #region Events

        /// <summary>Событие при получении нового кадра</summary>
        public event EventHandler<FrameCapturedEventArgs> FrameCaptured;

        /// <summary>Событие при изменении размера окна</summary>
        public event EventHandler<WindowChangedEventArgs> WindowChanged;

        /// <summary>Событие при потере окна (закрытие, минимизация)</summary>
        public event EventHandler WindowLost;

        /// <summary>Событие при ошибке захвата</summary>
        public event EventHandler<CaptureErrorEventArgs> CaptureError;

        private void Raise<T>(EventHandler<T> ev, T args) where T : EventArgs
            => Volatile.Read(ref ev)?.Invoke(this, args);

        private void Raise(EventHandler ev)
            => Volatile.Read(ref ev)?.Invoke(this, EventArgs.Empty);

        #endregion

        #region Constructor & Initialization

        public EnhancedWindowsGraphicsCapture(CaptureSettings settings = null)
        {
            _settings = settings ?? CaptureSettings.Default;
        }

        /// <summary>Проверяет поддержку WGC</summary>
        public static bool IsSupported()
        {
            try { return GraphicsCaptureSession.IsSupported(); }
            catch { return false; }
        }

        /// <summary>
        /// Инициализирует захват для окна или монитора
        /// </summary>
        public async Task InitializeAsync(IntPtr windowHandle = default, int monitorIndex = 0)
        {
            await Task.Run(() =>
            {
                InitializeD3D11();
                InitializeCaptureItem(windowHandle, monitorIndex);
                InitializeFramePool();

                if (_settings.AutoTrackWindow && windowHandle != IntPtr.Zero)
                {
                    StartWindowTracking(windowHandle);
                }
            });
        }

        private void InitializeD3D11()
        {
            var flags = D3D11.DeviceCreationFlags.BgraSupport;
            if (_settings.UseHardwareAcceleration)
                flags |= D3D11.DeviceCreationFlags.VideoSupport;

            _d3d11Device = new D3D11.Device(D3D.DriverType.Hardware, flags);
            _d3d11Context = _d3d11Device.ImmediateContext;

            using var dxgiDevice = _d3d11Device.QueryInterface<DXGI.Device>();
            CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var raw);
            _winrtD3DDevice = MarshalInterface<IDirect3DDevice>.FromAbi(raw);
            Marshal.Release(raw);
        }

        private void InitializeCaptureItem(IntPtr windowHandle, int monitorIndex)
        {
            _windowHandle = windowHandle;

            _captureItem = windowHandle != IntPtr.Zero
                ? CreateCaptureItemForWindow(windowHandle)
                : CreateCaptureItemForMonitor(monitorIndex);

            if (_captureItem == null)
                throw new InvalidOperationException("Failed to create GraphicsCaptureItem");

            _poolSize = _captureItem.Size;

            if (windowHandle != IntPtr.Zero)
            {
                GetWindowRect(windowHandle, out _lastWindowRect);
            }
        }

        private void InitializeFramePool()
        {
            _framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                _winrtD3DDevice,
                _settings.PixelFormat,
                _settings.FramePoolSize,
                _poolSize);

            _framePool.FrameArrived += OnFrameArrived;
        }

        #endregion

        #region Single Frame Capture

        /// <summary>Захватывает один кадр</summary>
        public async Task<CaptureFrame> CaptureFrameAsync()
        {
            if (_framePool == null || _captureItem == null)
                throw new InvalidOperationException("Not initialized. Call InitializeAsync first.");

            TaskCompletionSource<CaptureFrame> localTcs;
            lock (_lock)
            {
                localTcs = new TaskCompletionSource<CaptureFrame>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                // Объединяем одноразовый захват с обработчиком событий
                void OneShot(object s, object a)
                {
                    // no-op; мы используем OnFrameArrived для выдачи кадра
                }
            }

            using var session = _framePool.CreateCaptureSession(_captureItem);

            // Применяем флаги сессии, которые могли быть настроены
            if (_settings.IsCursorCaptureEnabled is bool c) session.IsCursorCaptureEnabled = c;


            session.StartCapture();

            using var cts = new CancellationTokenSource(_settings.CaptureTimeout);
            var tcs = new TaskCompletionSource<CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<FrameCapturedEventArgs> handler = null;
            handler = (sender, e) =>
            {
                tcs.TrySetResult(e.Frame);
                FrameCaptured -= handler;
            };
            FrameCaptured += handler;

            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                FrameCaptured -= handler;
                throw new TimeoutException($"Frame capture timeout ({_settings.CaptureTimeout}ms)");
            }
        }

        /// <summary>Захватывает кадр и возвращает как Bitmap</summary>
        public async Task<Bitmap> CaptureBitmapAsync()
        {
            var frame = await CaptureFrameAsync();
            return frame.ToBitmap();
        }

        /// <summary>Захватывает кадр и возвращает как byte[]</summary>
        public async Task<byte[]> CaptureByteArrayAsync(ImageFormat format = null)
        {
            var frame = await CaptureFrameAsync();
            return frame.ToByteArray(format ?? ImageFormat.Png);
        }

        #endregion

        #region Stream Capture

        /// <summary>Начинает потоковый захват</summary>
        public void StartCapture()
        {
            if (_isCapturing) return;

            lock (_lock)
            {
                if (_isCapturing) return;

                _captureCts = new CancellationTokenSource();
                _captureSession = _framePool.CreateCaptureSession(_captureItem);

                if (_settings.IsCursorCaptureEnabled is bool c)
                    _captureSession.IsCursorCaptureEnabled = c;


                _captureSession.StartCapture();
                _isCapturing = true;
                _metrics.Reset();
            }
        }

        /// <summary>Останавливает потоковый захват</summary>
        public void StopCapture()
        {
            if (!_isCapturing) return;

            lock (_lock)
            {
                if (!_isCapturing) return;

                _captureCts?.Cancel();
                _captureSession?.Dispose();
                _captureSession = null;
                _isCapturing = false;
            }
        }

        /// <summary>Получает следующий кадр из потока (блокирующий)</summary>
        public async Task<CaptureFrame> GetNextFrameAsync(CancellationToken cancellationToken = default)
        {
            if (!_isCapturing)
                throw new InvalidOperationException("Capture not started. Call StartCapture first.");

            var tcs = new TaskCompletionSource<CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<FrameCapturedEventArgs> handler = null;
            handler = (sender, e) =>
            {
                tcs.TrySetResult(e.Frame);
                FrameCaptured -= handler;
            };
            FrameCaptured += handler;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _captureCts.Token);
            try
            {
                return await tcs.Task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                FrameCaptured -= handler;
                throw;
            }
        }

        /// <summary>Получает последний захваченный кадр (неблокирующий)</summary>
        public CaptureFrame GetLastFrame() => _frameBuffer.GetLast();

        /// <summary>Получает N последних кадров из буфера</summary>
        public CaptureFrame[] GetRecentFrames(int count) => _frameBuffer.GetRecent(count);

        #endregion

        #region Window Tracking

        /// <summary>Начинает отслеживание окна</summary>
        private void StartWindowTracking(IntPtr hwnd)
        {
            _windowTracker = new System.Threading.Timer(CheckWindowState, hwnd, 100, 100);
        }

        private void CheckWindowState(object state)
        {
            var hwnd = (IntPtr)state;

            if (!IsWindow(hwnd))
            {
                Raise(WindowLost);
                StopCapture();
                _windowTracker?.Dispose();
                return;
            }

            if (!GetWindowRect(hwnd, out var rect))
                return;

            if (rect != _lastWindowRect)
            {
                var oldRect = _lastWindowRect;
                _lastWindowRect = rect;

                // Пересоздаем пул если изменился размер
                if (rect.Width != oldRect.Width || rect.Height != oldRect.Height)
                {
                    lock (_lock)
                    {
                        _poolSize = new SizeInt32 { Width = rect.Width, Height = rect.Height };
                        _framePool?.Recreate(_winrtD3DDevice, _settings.PixelFormat,
                            _settings.FramePoolSize, _poolSize);

                        // Сбросим staging — он станет неверного размера
                        _staging?.Dispose();
                        _staging = null;
                    }
                }

                Raise(WindowChanged, new WindowChangedEventArgs
                {
                    OldBounds = oldRect,
                    NewBounds = rect,
                    Type = rect.Size == oldRect.Size ? WindowChangeType.Moved : WindowChangeType.Resized
                });
            }
        }

        #endregion

        #region Frame Processing

        private readonly CircularBuffer<CaptureFrame> _frameBuffer = new(30);

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            using var frame = sender.TryGetNextFrame();
            if (frame == null) return;

            try
            {
                // Guard: бывают нулевые размеры при сворачивании/перемещении
                if (frame.ContentSize.Width <= 0 || frame.ContentSize.Height <= 0)
                    return;

                // Handle resize
                if (frame.ContentSize.Width != _poolSize.Width ||
                    frame.ContentSize.Height != _poolSize.Height)
                {
                    _poolSize = frame.ContentSize;
                    sender.Recreate(_winrtD3DDevice, _settings.PixelFormat,
                        _settings.FramePoolSize, _poolSize);

                    // Сброс staging под новый размер
                    _staging?.Dispose();
                    _staging = null;
                }

                // Process frame
                var captureFrame = ProcessFrame(frame);

                // Update metrics
                UpdateMetrics();

                // Add to buffer (кольцевой буфер владеет кадрами)
                _frameBuffer.Add(captureFrame);

                // Raise event
                Raise(FrameCaptured, new FrameCapturedEventArgs { Frame = captureFrame });
            }
            catch (Exception ex)
            {
                Raise(CaptureError, new CaptureErrorEventArgs { Exception = ex });
            }
        }

        private CaptureFrame ProcessFrame(Direct3D11CaptureFrame frame)
        {
            var texture = GetSharpDXTexture(frame.Surface);
            var width = frame.ContentSize.Width;
            var height = frame.ContentSize.Height;

            // Apply ROI if configured
            var roi = _settings.RegionOfInterest;
            if (roi.HasValue && roi.Value.Width > 0 && roi.Value.Height > 0)
            {
                width = Math.Min(roi.Value.Width, width);
                height = Math.Min(roi.Value.Height, height);
            }

            var captureFrame = new CaptureFrame
            {
                Timestamp = DateTime.UtcNow,
                Width = width,
                Height = height,
                FrameNumber = _metrics.TotalFrames,
                WindowHandle = _windowHandle
            };

            // Copy texture data into pooled byte[]
            CopyTextureData(texture, captureFrame, roi);

            return captureFrame;
        }

        private void CopyTextureData(D3D11.Texture2D source, CaptureFrame frame, Rectangle? roi)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var device = source.Device ?? _d3d11Device
                ?? throw new InvalidOperationException("D3D11 device is not initialized.");
            var context = device.ImmediateContext ?? _d3d11Context
                ?? throw new InvalidOperationException("D3D11 context is not initialized.");

            int width = Math.Max(0, frame.Width);
            int height = Math.Max(0, frame.Height);
            if (width == 0 || height == 0)
                throw new InvalidOperationException("Frame size is zero.");

            // Создаём/переиспользуем staging текстуру нужного размера/формата
            var fmt = source.Description.Format;
            var staging = GetOrCreateStaging(device, width, height, fmt);

            // Источник для копирования: clamp ROI в границы исходной текстуры
            var srcW = source.Description.Width;
            var srcH = source.Description.Height;

            int x0 = 0, y0 = 0, x1 = width, y1 = height;
            if (roi.HasValue && roi.Value.Width > 0 && roi.Value.Height > 0)
            {
                x0 = Math.Clamp(roi.Value.Left, 0, srcW);
                y0 = Math.Clamp(roi.Value.Top, 0, srcH);
                x1 = Math.Clamp(roi.Value.Right, 0, srcW);
                y1 = Math.Clamp(roi.Value.Bottom, 0, srcH);
            }

            var region = new D3D11.ResourceRegion(x0, y0, 0, x1, y1, 1);

            // Копируем сабресурс в staging
            context.CopySubresourceRegion(source, 0, region, staging, 0);

            // Чтение staging в системную память
            var box = context.MapSubresource(staging, 0, D3D11.MapMode.Read, D3D11.MapFlags.None);
            try
            {
                // Рентуем буфер из пула (Width * Height * 4 BGRA)
                int byteCount = width * height * 4;
                frame.RentFromPool(byteCount);
                frame.Stride = width * 4;

                int rowBytes = width * 4;

                unsafe
                {
                    byte* srcBase = (byte*)box.DataPointer;
                    var dst = frame.Data; // из пула

                    // построчное копирование: учитываем RowPitch staging'a
                    for (int y = 0; y < height; y++)
                    {
                        var srcRow = new ReadOnlySpan<byte>(srcBase + y * box.RowPitch, rowBytes);
                        var dstRow = new Span<byte>(dst, y * frame.Stride, rowBytes);
                        srcRow.CopyTo(dstRow);
                    }
                }
            }
            finally
            {
                context.UnmapSubresource(staging, 0);
            }
        }

        private D3D11.Texture2D GetOrCreateStaging(D3D11.Device device, int w, int h, DXGI.Format fmt)
        {
            if (_staging != null)
            {
                var d = _staging.Description;
                if (d.Width == w && d.Height == h && d.Format == fmt)
                    return _staging;
            }

            _staging?.Dispose();
            _staging = new D3D11.Texture2D(device, new D3D11.Texture2DDescription
            {
                Width = w,
                Height = h,
                MipLevels = 1,
                ArraySize = 1,
                Format = fmt,
                SampleDescription = new DXGI.SampleDescription(1, 0),
                Usage = D3D11.ResourceUsage.Staging,
                BindFlags = D3D11.BindFlags.None,
                CpuAccessFlags = D3D11.CpuAccessFlags.Read,
                OptionFlags = D3D11.ResourceOptionFlags.None
            });
            return _staging;
        }

        #endregion

        #region Metrics

        private void UpdateMetrics()
        {
            var now = DateTime.UtcNow;
            var elapsed = (now - _lastFrameTime).TotalMilliseconds;

            _metrics.TotalFrames++;
            _metrics.CurrentFps = elapsed > 0 ? 1000.0 / elapsed : 0;
            _metrics.UpdateAverageFps(_metrics.CurrentFps);

            if (elapsed > 1000.0 / _settings.TargetFps * 1.5)
            {
                _metrics.DroppedFrames++;
            }

            _lastFrameTime = now;
        }

        /// <summary>Получает метрики захвата</summary>
        public CaptureMetrics GetMetrics() => _metrics.Clone();

        #endregion

        #region Helper Methods

        private D3D11.Texture2D GetSharpDXTexture(IDirect3DSurface surface)
        {
            var access = surface.As<IDirect3DDxgiInterfaceAccess>();
            var guid = typeof(D3D11.Texture2D).GUID;
            access.GetInterface(ref guid, out var raw);
            return new D3D11.Texture2D(raw);
        }

        private GraphicsCaptureItem CreateCaptureItemForWindow(IntPtr hwnd)
        {
            var interop = GetActivationFactory<IGraphicsCaptureItemInterop>(
                "Windows.Graphics.Capture.GraphicsCaptureItem");
            var iid = IID_IInspectable;
            interop.CreateForWindow(hwnd, ref iid, out var ptr);
            var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(ptr);
            Marshal.Release(ptr);
            return item;
        }

        private GraphicsCaptureItem CreateCaptureItemForMonitor(int index = 0)
        {
            var monitors = GetMonitors();
            if (index >= monitors.Count)
                throw new ArgumentException($"Monitor index {index} not found");

            var interop = GetActivationFactory<IGraphicsCaptureItemInterop>(
                "Windows.Graphics.Capture.GraphicsCaptureItem");
            var iid = IID_IInspectable;
            interop.CreateForMonitor(monitors[index], ref iid, out var ptr);
            var item = MarshalInterface<GraphicsCaptureItem>.FromAbi(ptr);
            Marshal.Release(ptr);
            return item;
        }

        private List<IntPtr> GetMonitors()
        {
            var monitors = new List<IntPtr>();
            MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                monitors.Add(hMonitor);
                return true;
            };
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return monitors;
        }

        #endregion

        #region Dispose

        public void Dispose()
        {
            try
            {
                StopCapture();

                _windowTracker?.Dispose();

                if (_framePool != null)
                {
                    _framePool.FrameArrived -= OnFrameArrived;
                    _framePool.Dispose();
                    _framePool = null;
                }

                _captureSession?.Dispose();
                _captureSession = null;

                _captureItem = null;
                _winrtD3DDevice = null;

                _staging?.Dispose();
                _staging = null;

                _d3d11Context?.Dispose();
                _d3d11Device?.Dispose();
            }
            catch
            {
                // ignore on dispose-path
            }
        }

        #endregion

        #region P/Invoke

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out Rectangle lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
            MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
            ref RECT lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("combase.dll")]
        private static extern int WindowsCreateString([MarshalAs(UnmanagedType.LPWStr)] string source,
            int length, out IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("combase.dll")]
        private static extern int RoGetActivationFactory(IntPtr hstring, ref Guid iid, out IntPtr factory);

        [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
        private static extern uint CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice,
            out IntPtr graphicsDevice);

        private static TFactory GetActivationFactory<TFactory>(string runtimeClass)
        {
            IntPtr hstr = IntPtr.Zero;
            IntPtr pFactory = IntPtr.Zero;
            try
            {
                WindowsCreateString(runtimeClass, runtimeClass.Length, out hstr);
                var iid = typeof(TFactory).GUID;
                RoGetActivationFactory(hstr, ref iid, out pFactory);
                return (TFactory)Marshal.GetObjectForIUnknown(pFactory);
            }
            finally
            {
                if (pFactory != IntPtr.Zero) Marshal.Release(pFactory);
                if (hstr != IntPtr.Zero) WindowsDeleteString(hstr);
            }
        }

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

        #endregion
    }

    #region Supporting Classes

    /// <summary>Настройки захвата</summary>
    public class CaptureSettings
    {
        public DirectXPixelFormat PixelFormat { get; set; } = DirectXPixelFormat.B8G8R8A8UIntNormalized;
        public int FramePoolSize { get; set; } = 2;
        public int TargetFps { get; set; } = 60;
        public int CaptureTimeout { get; set; } = 5000;
        public bool AutoTrackWindow { get; set; } = true;
        public bool UseHardwareAcceleration { get; set; } = true;
        public bool? IsCursorCaptureEnabled { get; set; }
        public bool? IsBorderRequired { get; set; }
        public Rectangle? RegionOfInterest { get; set; }

        public static CaptureSettings Default => new();

        public static CaptureSettings HighPerformance => new()
        {
            FramePoolSize = 3,
            TargetFps = 144,
            UseHardwareAcceleration = true
        };

        public static CaptureSettings LowLatency => new()
        {
            FramePoolSize = 1,
            TargetFps = 30,
            CaptureTimeout = 1000
        };

        public CaptureSettings Clone() => new CaptureSettings
        {
            PixelFormat = this.PixelFormat,
            FramePoolSize = this.FramePoolSize,
            TargetFps = this.TargetFps,
            CaptureTimeout = this.CaptureTimeout,
            AutoTrackWindow = this.AutoTrackWindow,
            UseHardwareAcceleration = this.UseHardwareAcceleration,
            IsCursorCaptureEnabled = this.IsCursorCaptureEnabled,
            IsBorderRequired = this.IsBorderRequired,
            RegionOfInterest = this.RegionOfInterest
        };
    }

    /// <summary>Кадр захвата (буфер арендован из ArrayPool, возвращается в Dispose)</summary>
    public class CaptureFrame : IDisposable
    {
        private static readonly ArrayPool<byte> _pool = ArrayPool<byte>.Shared;

        public DateTime Timestamp { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Stride { get; set; }
        public byte[] Data { get; private set; }
        public long FrameNumber { get; set; }
        public IntPtr WindowHandle { get; set; }

        private int _rentedSize;

        internal void RentFromPool(int size)
        {
            // если уже есть буфер достаточного размера — вернём старый и возьмём новый (упрощение)
            if (Data != null)
            {
                _pool.Return(Data);
                Data = null;
                _rentedSize = 0;
            }
            Data = _pool.Rent(size);
            _rentedSize = size;
        }

        public Bitmap ToBitmap()
        {
            if (Data == null || _rentedSize == 0)
                throw new InvalidOperationException("Frame has no data");

            var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, Width, Height);
            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                int srcStride = (Stride > 0) ? Stride : Width * 4;
                int dstStride = bmpData.Stride;
                int rowBytes = Width * 4;

                unsafe
                {
                    fixed (byte* pSrcBase = Data)
                    {
                        byte* src = pSrcBase;
                        byte* dst = (byte*)bmpData.Scan0;

                        for (int y = 0; y < Height; y++)
                        {
                            Buffer.MemoryCopy(src, dst, dstStride, rowBytes);
                            src += srcStride;
                            dst += dstStride;
                        }
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }

        /// <summary>Конвертирует в byte array с указанным форматом</summary>
        public byte[] ToByteArray(ImageFormat format)
        {
            using var bitmap = ToBitmap();
            using var ms = new System.IO.MemoryStream();
            bitmap.Save(ms, format);
            return ms.ToArray();
        }

        /// <summary>Получает OpenCV-совместимый массив BGR</summary>
        public byte[] ToBgrArray()
        {
            if (Data == null || _rentedSize == 0)
                throw new InvalidOperationException("Frame has no data");

            var bgr = new byte[Width * Height * 3];
            var srcIndex = 0;
            var dstIndex = 0;

            for (int i = 0; i < Width * Height; i++)
            {
                bgr[dstIndex++] = Data[srcIndex];     // B
                bgr[dstIndex++] = Data[srcIndex + 1]; // G
                bgr[dstIndex++] = Data[srcIndex + 2]; // R
                srcIndex += 4; // Skip A
            }

            return bgr;
        }

        /// <summary>Клонирует кадр (новый буфер)</summary>
        public CaptureFrame Clone()
        {
            var clone = new CaptureFrame
            {
                Timestamp = Timestamp,
                Width = Width,
                Height = Height,
                Stride = Stride,
                FrameNumber = FrameNumber,
                WindowHandle = WindowHandle
            };
            if (Data != null && _rentedSize > 0)
            {
                clone.RentFromPool(_rentedSize);
                Buffer.BlockCopy(Data, 0, clone.Data, 0, _rentedSize);
            }
            return clone;
        }

        public void Dispose()
        {
            if (Data != null)
            {
                _pool.Return(Data);
                Data = null;
                _rentedSize = 0;
            }
        }
    }

    /// <summary>Метрики захвата</summary>
    public class CaptureMetrics
    {
        public long TotalFrames { get; set; }
        public long DroppedFrames { get; set; }
        public double CurrentFps { get; set; }
        public double AverageFps { get; private set; }
        public DateTime StartTime { get; private set; }
        public TimeSpan Uptime => DateTime.UtcNow - StartTime;

        private double _fpsSum;
        private int _fpsCount;

        public CaptureMetrics() => Reset();

        public void Reset()
        {
            TotalFrames = 0;
            DroppedFrames = 0;
            CurrentFps = 0;
            AverageFps = 0;
            _fpsSum = 0;
            _fpsCount = 0;
            StartTime = DateTime.UtcNow;
        }

        public void UpdateAverageFps(double fps)
        {
            _fpsSum += fps;
            _fpsCount++;
            AverageFps = _fpsSum / _fpsCount;
        }

        public CaptureMetrics Clone() => new CaptureMetrics
        {
            TotalFrames = TotalFrames,
            DroppedFrames = DroppedFrames,
            CurrentFps = CurrentFps,
            AverageFps = AverageFps,
            StartTime = StartTime,
            _fpsSum = _fpsSum,
            _fpsCount = _fpsCount
        };
    }

    /// <summary>Кольцевой буфер для кадров</summary>
    public class CircularBuffer<T> : IDisposable where T : IDisposable
    {
        private readonly T[] _buffer;
        private readonly object _lock = new();
        private int _head;
        private int _count;

        public CircularBuffer(int capacity)
        {
            if (capacity <= 0) capacity = 1;
            _buffer = new T[capacity];
        }

        public void Add(T item)
        {
            lock (_lock)
            {
                if (_count < _buffer.Length)
                {
                    _buffer[_count++] = item;
                }
                else
                {
                    _buffer[_head]?.Dispose();
                    _buffer[_head] = item;
                    _head = (_head + 1) % _buffer.Length;
                }
            }
        }

        public T GetLast()
        {
            lock (_lock)
            {
                if (_count == 0) return default;
                var index = _count < _buffer.Length ? _count - 1 : (_head - 1 + _buffer.Length) % _buffer.Length;
                return _buffer[index];
            }
        }

        public T[] GetRecent(int count)
        {
            lock (_lock)
            {
                count = Math.Min(count, _count);
                var result = new T[count];

                for (int i = 0; i < count; i++)
                {
                    var index = _count < _buffer.Length
                        ? _count - 1 - i
                        : (_head - 1 - i + _buffer.Length) % _buffer.Length;
                    result[i] = _buffer[index];
                }

                return result;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                for (int i = 0; i < _count; i++)
                {
                    _buffer[i]?.Dispose();
                }
            }
        }
    }

    #endregion

    #region Event Args

    public class FrameCapturedEventArgs : EventArgs
    {
        public CaptureFrame Frame { get; set; }
    }

    public class WindowChangedEventArgs : EventArgs
    {
        public Rectangle OldBounds { get; set; }
        public Rectangle NewBounds { get; set; }
        public WindowChangeType Type { get; set; }
    }

    public class CaptureErrorEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
    }

    public enum WindowChangeType
    {
        Moved,
        Resized,
        Minimized,
        Restored
    }

    #endregion
}
