using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace AutomationCore.UI
{
    /// <summary>
    /// Менеджер для управления современным оверлеем из любого потока
    /// </summary>
    public sealed class OverlayManager : IDisposable
    {
        private Thread _uiThread;
        private ModernOverlay _overlay;
        private readonly ManualResetEventSlim _ready = new(false);
        private readonly object _lock = new();
        private bool _disposed;
        private bool _visible;

        public bool IsVisible => _visible;

        /// <summary>
        /// Создает новый экземпляр менеджера оверлея
        /// </summary>
        public OverlayManager()
        {
            StartUIThread();
        }

        private void StartUIThread()
        {
            _uiThread = new Thread(() =>
            {
                System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                System.Windows.Forms.Application.EnableVisualStyles();

                _overlay = new ModernOverlay();
                _ready.Set();

                System.Windows.Forms.Application.Run(_overlay);
            })
            {
                Name = "OverlayUI",
                IsBackground = true
            };

            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.Start();

            // Ждем инициализации
            _ready.Wait(5000);
        }

        /// <summary>
        /// Выполнить действие в UI потоке
        /// </summary>
        private void InvokeUI(Action action)
        {
            if (_disposed || _overlay == null) return;

            try
            {
                if (_overlay.IsHandleCreated)
                {
                    _overlay.BeginInvoke(action);
                }
            }
            catch
            {
                // Окно закрывается, игнорируем
            }
        }

        /// <summary>
        /// Выполнить действие в UI потоке синхронно
        /// </summary>
        private T InvokeUISync<T>(Func<T> func)
        {
            if (_disposed || _overlay == null) return default(T);

            try
            {
                if (_overlay.IsHandleCreated)
                {
                    return (T)_overlay.Invoke(func);
                }
            }
            catch
            {
                // Окно закрывается
            }

            return default(T);
        }

        /// <summary>
        /// Показать оверлей на всех мониторах
        /// </summary>
        public void Show()
        {
            InvokeUI(() =>
            {
                _overlay.FitToVirtualScreen();
                _overlay.Show();
                _overlay.BringToTop();
                _visible = true;
            });
        }

        /// <summary>
        /// Скрыть оверлей
        /// </summary>
        public void Hide()
        {
            InvokeUI(() =>
            {
                _overlay.Hide();
                _visible = false;
            });
        }

        /// <summary>
        /// Очистить все элементы
        /// </summary>
        public void Clear()
        {
            InvokeUI(() => _overlay.Clear());
        }

        #region Методы добавления элементов

        /// <summary>
        /// Добавить рамку вокруг области
        /// </summary>
        /// <param name="rect">Прямоугольник</param>
        /// <param name="color">Цвет (null = зеленый)</param>
        /// <param name="style">Стиль отображения</param>
        public void DrawBox(Rectangle rect, Color? color = null, BoxStyle style = BoxStyle.Modern)
        {
            InvokeUI(() => _overlay.AddBox(rect, color ?? ModernOverlay.ColorSchemes.Success, style));
        }

        /// <summary>
        /// Добавить рамку вокруг найденного элемента с подсветкой
        /// </summary>
        public void HighlightMatch(Rectangle rect, double score, bool isHardPass = true)
        {
            var color = isHardPass ? ModernOverlay.ColorSchemes.Success : ModernOverlay.ColorSchemes.Warning;
            var style = isHardPass ? BoxStyle.Glow : BoxStyle.Dashed;

            InvokeUI(() =>
            {
                _overlay.AddBox(rect, color, style);

                // Добавляем текст со score
                var text = $"Score: {score:F3}";
                var textPos = new Point(rect.Right + 10, rect.Top);
                _overlay.AddText(textPos, text, color, TextStyle.Bold);

                // Добавляем пульсирующую точку в центре
                if (isHardPass)
                {
                    var center = new Point(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
                    _overlay.AddPulse(center, 5, color);
                }
            });
        }

        /// <summary>
        /// Добавить текст
        /// </summary>
        /// <param name="position">Позиция</param>
        /// <param name="text">Текст</param>
        /// <param name="color">Цвет (null = белый)</param>
        /// <param name="style">Стиль текста</param>
        public void DrawText(Point position, string text, Color? color = null, TextStyle style = TextStyle.Modern)
        {
            InvokeUI(() => _overlay.AddText(position, text, color ?? Color.White, style));
        }

        /// <summary>
        /// Добавить индикатор прогресса
        /// </summary>
        /// <param name="rect">Область</param>
        /// <param name="progress">Прогресс от 0 до 1</param>
        /// <param name="color">Цвет (null = синий)</param>
        public void DrawProgress(Rectangle rect, float progress, Color? color = null)
        {
            InvokeUI(() => _overlay.AddProgressBar(rect, progress, color ?? ModernOverlay.ColorSchemes.Info));
        }

        /// <summary>
        /// Добавить пульсирующую точку
        /// </summary>
        /// <param name="center">Центр</param>
        /// <param name="radius">Радиус</param>
        /// <param name="color">Цвет (null = фиолетовый)</param>
        public void DrawPulse(Point center, int radius = 10, Color? color = null)
        {
            InvokeUI(() => _overlay.AddPulse(center, radius, color ?? ModernOverlay.ColorSchemes.Accent));
        }

        /// <summary>
        /// Добавить стрелку
        /// </summary>
        /// <param name="from">Начальная точка</param>
        /// <param name="to">Конечная точка</param>
        /// <param name="color">Цвет (null = оранжевый)</param>
        /// <param name="style">Стиль стрелки</param>
        public void DrawArrow(Point from, Point to, Color? color = null, ArrowStyle style = ArrowStyle.Modern)
        {
            InvokeUI(() => _overlay.AddArrow(from, to, color ?? ModernOverlay.ColorSchemes.Warning, style));
        }

        /// <summary>
        /// Добавить всплывающую подсказку
        /// </summary>
        /// <param name="anchor">Точка привязки</param>
        /// <param name="text">Текст подсказки</param>
        /// <param name="bgColor">Цвет фона (null = темный)</param>
        /// <param name="position">Позиция относительно точки</param>
        public void DrawTooltip(Point anchor, string text, Color? bgColor = null, TooltipPosition position = TooltipPosition.Top)
        {
            InvokeUI(() => _overlay.AddTooltip(anchor, text, bgColor ?? ModernOverlay.ColorSchemes.Dark, position));
        }

        #endregion

        #region Специальные эффекты

        /// <summary>
        /// Показать успешное обнаружение с анимацией
        /// </summary>
        public void ShowSuccess(Rectangle rect, string message = "Found!")
        {
            InvokeUI(() =>
            {
                // Зеленая рамка с эффектом свечения
                _overlay.AddBox(rect, ModernOverlay.ColorSchemes.Success, BoxStyle.Glow);

                // Пульсирующие точки по углам
                _overlay.AddPulse(new Point(rect.Left, rect.Top), 8, ModernOverlay.ColorSchemes.Success);
                _overlay.AddPulse(new Point(rect.Right, rect.Top), 8, ModernOverlay.ColorSchemes.Success);
                _overlay.AddPulse(new Point(rect.Left, rect.Bottom), 8, ModernOverlay.ColorSchemes.Success);
                _overlay.AddPulse(new Point(rect.Right, rect.Bottom), 8, ModernOverlay.ColorSchemes.Success);

                // Сообщение сверху
                var textPos = new Point(rect.X + rect.Width / 2 - 30, rect.Y - 30);
                _overlay.AddText(textPos, message, ModernOverlay.ColorSchemes.Success, TextStyle.Large);
            });
        }

        /// <summary>
        /// Показать ошибку
        /// </summary>
        public void ShowError(Rectangle rect, string message = "Not found")
        {
            InvokeUI(() =>
            {
                _overlay.AddBox(rect, ModernOverlay.ColorSchemes.Error, BoxStyle.Dashed);

                var textPos = new Point(rect.X + rect.Width / 2 - 40, rect.Y + rect.Height / 2);
                _overlay.AddText(textPos, message, ModernOverlay.ColorSchemes.Error, TextStyle.Bold);
            });
        }

        /// <summary>
        /// Показать процесс поиска с анимацией
        /// </summary>
        public void ShowSearching(Rectangle searchArea, string message = "Searching...")
        {
            InvokeUI(() =>
            {
                // Мигающая рамка области поиска
                _overlay.AddBox(searchArea, ModernOverlay.ColorSchemes.Info, BoxStyle.Dashed);

                // Текст с информацией
                var textPos = new Point(searchArea.X, searchArea.Y - 25);
                _overlay.AddText(textPos, message, ModernOverlay.ColorSchemes.Info, TextStyle.Modern);

                // Индикатор прогресса внизу области
                var progressRect = new Rectangle(
                    searchArea.X,
                    searchArea.Bottom + 10,
                    searchArea.Width,
                    8
                );
                _overlay.AddProgressBar(progressRect, 0.3f, ModernOverlay.ColorSchemes.Info);
            });
        }

        /// <summary>
        /// Показать путь движения мыши
        /// </summary>
        public void ShowMousePath(Point from, Point to, string action = "Click")
        {
            InvokeUI(() =>
            {
                // Стрелка пути
                _overlay.AddArrow(from, to, ModernOverlay.ColorSchemes.Accent, ArrowStyle.Curved);

                // Начальная точка
                _overlay.AddPulse(from, 5, ModernOverlay.ColorSchemes.Info);

                // Конечная точка с действием
                _overlay.AddPulse(to, 8, ModernOverlay.ColorSchemes.Success);
                _overlay.AddTooltip(to, action, ModernOverlay.ColorSchemes.Dark, TooltipPosition.Top);
            });
        }

        /// <summary>
        /// Анимированный поиск по сетке
        /// </summary>
        public async Task ShowGridSearch(Rectangle area, int gridSize = 50)
        {
            var cols = area.Width / gridSize;
            var rows = area.Height / gridSize;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    var cellRect = new Rectangle(
                        area.X + col * gridSize,
                        area.Y + row * gridSize,
                        gridSize,
                        gridSize
                    );

                    InvokeUI(() =>
                    {
                        _overlay.AddBox(cellRect, ModernOverlay.ColorSchemes.Info, BoxStyle.Dashed);
                    });

                    await Task.Delay(50);
                }
            }
        }

        #endregion

        /// <summary>
        /// Освобождение ресурсов
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
            }

            try
            {
                InvokeUI(() => _overlay?.Close());

                if (_uiThread != null && _uiThread.IsAlive)
                {
                    _uiThread.Join(1000);
                }
            }
            catch
            {
                // Игнорируем ошибки при закрытии
            }
            finally
            {
                _ready?.Dispose();
                _overlay = null;
                _uiThread = null;
            }
        }
    }
}