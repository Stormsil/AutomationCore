using System;
using System.Drawing;
using System.Threading.Tasks;
using AutomationCore.Capture;
using AutomationCore.Core;

namespace AutomationCore.UI
{
    /// <summary>
    /// Демонстрация возможностей современного оверлея
    /// </summary>
    public class OverlayDemo
    {
        /// <summary>
        /// Запустить полную демонстрацию всех возможностей
        /// </summary>
        public static async Task RunFullDemo()
        {
            using var overlay = new OverlayManager();

            Console.WriteLine("=== Демонстрация современного оверлея ===");
            Console.WriteLine("Показываем оверлей...");

            overlay.Show();
            await Task.Delay(500);

            // 1. Демонстрация различных стилей рамок
            Console.WriteLine("\n1. Стили рамок:");

            var rect1 = new Rectangle(100, 100, 200, 150);
            overlay.DrawBox(rect1, ModernOverlay.ColorSchemes.Success, BoxStyle.Modern);
            overlay.DrawText(new Point(rect1.X, rect1.Y - 25), "Modern Style", ModernOverlay.ColorSchemes.Success);
            await Task.Delay(1000);

            var rect2 = new Rectangle(350, 100, 200, 150);
            overlay.DrawBox(rect2, ModernOverlay.ColorSchemes.Info, BoxStyle.Glow);
            overlay.DrawText(new Point(rect2.X, rect2.Y - 25), "Glow Style", ModernOverlay.ColorSchemes.Info);
            await Task.Delay(1000);

            var rect3 = new Rectangle(600, 100, 200, 150);
            overlay.DrawBox(rect3, ModernOverlay.ColorSchemes.Warning, BoxStyle.Gradient);
            overlay.DrawText(new Point(rect3.X, rect3.Y - 25), "Gradient Style", ModernOverlay.ColorSchemes.Warning);
            await Task.Delay(1000);

            var rect4 = new Rectangle(850, 100, 200, 150);
            overlay.DrawBox(rect4, ModernOverlay.ColorSchemes.Error, BoxStyle.Dashed);
            overlay.DrawText(new Point(rect4.X, rect4.Y - 25), "Dashed Style", ModernOverlay.ColorSchemes.Error);
            await Task.Delay(2000);

            // 2. Пульсирующие элементы
            Console.WriteLine("2. Пульсирующие точки:");

            overlay.DrawPulse(new Point(200, 350), 10, ModernOverlay.ColorSchemes.Success);
            overlay.DrawPulse(new Point(400, 350), 15, ModernOverlay.ColorSchemes.Info);
            overlay.DrawPulse(new Point(600, 350), 20, ModernOverlay.ColorSchemes.Warning);
            overlay.DrawPulse(new Point(800, 350), 25, ModernOverlay.ColorSchemes.Error);
            await Task.Delay(2000);

            // 3. Индикаторы прогресса
            Console.WriteLine("3. Индикаторы прогресса:");

            for (float progress = 0; progress <= 1; progress += 0.1f)
            {
                overlay.Clear();

                var progressRect = new Rectangle(300, 450, 400, 30);
                overlay.DrawProgress(progressRect, progress, ModernOverlay.ColorSchemes.Accent);
                overlay.DrawText(new Point(progressRect.X, progressRect.Y - 25),
                    $"Loading... {(int)(progress * 100)}%", Color.White);

                await Task.Delay(200);
            }

            // 4. Стрелки и пути
            Console.WriteLine("4. Стрелки и навигация:");

            overlay.Clear();
            overlay.DrawArrow(new Point(100, 500), new Point(300, 400), ModernOverlay.ColorSchemes.Info, ArrowStyle.Modern);
            overlay.DrawArrow(new Point(400, 500), new Point(600, 400), ModernOverlay.ColorSchemes.Success, ArrowStyle.Curved);
            await Task.Delay(2000);

            // 5. Всплывающие подсказки
            Console.WriteLine("5. Всплывающие подсказки:");

            overlay.DrawTooltip(new Point(200, 600), "Подсказка сверху", ModernOverlay.ColorSchemes.Dark, TooltipPosition.Top);
            overlay.DrawTooltip(new Point(400, 600), "Подсказка снизу", ModernOverlay.ColorSchemes.Info, TooltipPosition.Bottom);
            overlay.DrawTooltip(new Point(600, 600), "Подсказка слева", ModernOverlay.ColorSchemes.Success, TooltipPosition.Left);
            overlay.DrawTooltip(new Point(800, 600), "Подсказка справа", ModernOverlay.ColorSchemes.Warning, TooltipPosition.Right);
            await Task.Delay(3000);

            // 6. Комплексная анимация поиска
            Console.WriteLine("6. Анимация поиска:");

            overlay.Clear();
            var searchArea = new Rectangle(300, 200, 600, 400);
            overlay.ShowSearching(searchArea, "Поиск элемента...");
            await Task.Delay(2000);

            // Имитация найденного элемента
            var foundRect = new Rectangle(500, 350, 150, 80);
            overlay.Clear();
            overlay.ShowSuccess(foundRect, "Элемент найден!");
            await Task.Delay(3000);

            // 7. Путь мыши
            Console.WriteLine("7. Визуализация действий мыши:");

            overlay.Clear();
            overlay.ShowMousePath(new Point(100, 300), new Point(500, 400), "Left Click");
            overlay.ShowMousePath(new Point(500, 400), new Point(700, 200), "Drag");
            overlay.ShowMousePath(new Point(700, 200), new Point(900, 500), "Right Click");
            await Task.Delay(3000);

            Console.WriteLine("\nДемонстрация завершена!");
            overlay.Hide();
        }

        /// <summary>
        /// Интеграция с поиском изображений
        /// </summary>
        public static async Task DemoWithImageSearch()
        {
            using var overlay = new OverlayManager();
            using var capture = new EnhancedScreenCapture();

            Console.WriteLine("=== Демонстрация поиска с оверлеем ===");

            overlay.Show();

            // Показываем область поиска
            var screenBounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            overlay.ShowSearching(screenBounds, "Сканирование экрана...");

            await Task.Delay(2000);

            // Имитация поиска нескольких элементов
            var random = new Random();
            for (int i = 0; i < 5; i++)
            {
                var x = random.Next(100, screenBounds.Width - 300);
                var y = random.Next(100, screenBounds.Height - 200);
                var width = random.Next(100, 250);
                var height = random.Next(80, 150);
                var rect = new Rectangle(x, y, width, height);

                var score = 0.7 + random.NextDouble() * 0.3;
                var isMatch = score > 0.85;

                overlay.Clear();

                if (isMatch)
                {
                    overlay.HighlightMatch(rect, score, true);
                    Console.WriteLine($"✓ Найдено совпадение: Score = {score:F3}");
                }
                else
                {
                    overlay.HighlightMatch(rect, score, false);
                    Console.WriteLine($"○ Частичное совпадение: Score = {score:F3}");
                }

                await Task.Delay(1500);
            }

            overlay.Clear();
            overlay.DrawText(new Point(screenBounds.Width / 2 - 100, screenBounds.Height / 2),
                "Поиск завершен", ModernOverlay.ColorSchemes.Success, TextStyle.Large);

            await Task.Delay(2000);
            overlay.Hide();

            Console.WriteLine("\nПоиск завершен!");
        }

        /// <summary>
        /// Пример использования в реальном сценарии
        /// </summary>
        public static async Task RealWorldExample()
        {
            using var overlay = new OverlayManager();
            using var capture = new EnhancedScreenCapture();

            try
            {
                Console.WriteLine("Поиск кнопки на экране...");

                overlay.Show();

                // Визуализация процесса поиска
                overlay.ShowSearching(System.Windows.Forms.Screen.PrimaryScreen.Bounds, "Поиск кнопки...");

                // Здесь должен быть реальный поиск через EnhancedScreenCapture
                // var result = await capture.FindBestMatchByKeyAsync("button_template");

                // Для демонстрации используем заглушку
                await Task.Delay(2000);
                var mockResult = new Rectangle(500, 300, 120, 40);
                var mockScore = 0.92;

                overlay.Clear();

                if (mockScore > 0.85)
                {
                    // Показываем найденный элемент
                    overlay.HighlightMatch(mockResult, mockScore, true);

                    // Добавляем дополнительную информацию
                    var center = new Point(mockResult.X + mockResult.Width / 2, mockResult.Y + mockResult.Height / 2);
                    overlay.DrawTooltip(center, $"Confidence: {mockScore:P0}",
                        ModernOverlay.ColorSchemes.Success, TooltipPosition.Bottom);

                    // Показываем, куда будет клик
                    await Task.Delay(1000);
                    overlay.ShowMousePath(new Point(100, 100), center, "Auto-click");

                    Console.WriteLine($"✓ Кнопка найдена! Score: {mockScore:F3}");
                    Console.WriteLine($"  Позиция: ({mockResult.X}, {mockResult.Y})");
                    Console.WriteLine($"  Размер: {mockResult.Width}x{mockResult.Height}");
                }
                else
                {
                    overlay.ShowError(System.Windows.Forms.Screen.PrimaryScreen.Bounds, "Кнопка не найдена");
                    Console.WriteLine("✗ Кнопка не найдена");
                }

                await Task.Delay(3000);
            }
            finally
            {
                overlay.Hide();
            }
        }
    }

    /// <summary>
    /// Точка входа для тестирования
    /// </summary>
    class OverlayTestProgram
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Выберите демонстрацию:");
            Console.WriteLine("1. Полная демонстрация возможностей");
            Console.WriteLine("2. Демонстрация с поиском изображений");
            Console.WriteLine("3. Реальный пример использования");
            Console.Write("\nВаш выбор (1-3): ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await OverlayDemo.RunFullDemo();
                        break;
                    case "2":
                        await OverlayDemo.DemoWithImageSearch();
                        break;
                    case "3":
                        await OverlayDemo.RealWorldExample();
                        break;
                    default:
                        Console.WriteLine("Неверный выбор");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }

            Console.WriteLine("\nНажмите любую клавишу для выхода...");
            Console.ReadKey();
        }
    }
}