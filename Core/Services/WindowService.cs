namespace AutomationCore.Core.Services
{
    /// <summary>
    /// Сервис для работы с окнами (изолирует P/Invoke)
    /// </summary>
    public interface IWindowService
    {
        IReadOnlyList<WindowInfo> GetAllWindows();
        IReadOnlyList<WindowInfo> FindWindows(WindowSearchCriteria criteria);
        WindowInfo GetWindowInfo(WindowHandle handle);
        bool IsWindowValid(WindowHandle handle);
        Rectangle GetWindowBounds(WindowHandle handle);
    }

    public class WindowSearchCriteria
    {
        public string TitlePattern { get; set; }
        public string ProcessName { get; set; }
        public string ClassName { get; set; }
        public bool ExactMatch { get; set; }
        public bool VisibleOnly { get; set; } = true;

        // Fluent API
        public static WindowSearchCriteria WithTitle(string pattern)
            => new() { TitlePattern = pattern };
    }

    public class WindowService : IWindowService
    {
        private readonly IPInvokeWrapper _pinvoke;
        private readonly ILogger<WindowService> _logger;

        public WindowService(IPInvokeWrapper pinvoke, ILogger<WindowService> logger)
        {
            _pinvoke = pinvoke ?? throw new ArgumentNullException(nameof(pinvoke));
            _logger = logger ?? NullLogger<WindowService>.Instance;
        }

        public IReadOnlyList<WindowInfo> GetAllWindows()
        {
            _logger.LogDebug("Enumerating all windows");
            var windows = new List<WindowInfo>();

            _pinvoke.EnumWindows((hwnd, _) =>
            {
                if (_pinvoke.IsWindowVisible(hwnd))
                {
                    var info = BuildWindowInfo(new WindowHandle(hwnd));
                    if (info != null) windows.Add(info);
                }
                return true;
            });

            _logger.LogDebug("Found {Count} windows", windows.Count);
            return windows;
        }

        // ... остальная реализация
    }
}