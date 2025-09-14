using System.Diagnostics;

namespace AutomationCore.Test;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🚀 AutomationCore Demo Application");
        Console.WriteLine("===================================");
        Console.WriteLine("This is a simple demonstration of AutomationCore capabilities.\n");

        try
        {
            // Создаем папку Assets если не существует
            var assetsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets");
            Directory.CreateDirectory(assetsPath);

            Console.WriteLine($"📁 Assets folder: {assetsPath}");
            Console.WriteLine("ℹ️  Place your image templates (PNG files) in the Assets folder for future demos\n");

            // Простая демонстрация автоматизации
            await RunSimpleAutomationDemo();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }

        Console.WriteLine("\n✅ Demo completed. Press any key to exit...");
        Console.ReadKey();
    }

    private static async Task RunSimpleAutomationDemo()
    {
        Console.WriteLine("🎬 Starting simple automation demo");
        Console.WriteLine();

        try
        {
            // 1. Открываем Блокнот
            Console.WriteLine("📝 Step 1: Opening Notepad...");
            var notepadProcess = Process.Start("notepad.exe");

            if (notepadProcess == null)
            {
                Console.WriteLine("❌ Failed to start Notepad");
                return;
            }

            // Ждем загрузки Notepad
            Console.WriteLine("⏳ Waiting for Notepad to load...");
            await Task.Delay(3000);
            Console.WriteLine("✅ Notepad should now be open!");

            // 2. Демонстрация поиска окон через WinAPI
            Console.WriteLine("\n🔍 Step 2: Demonstrating window enumeration...");
            await DemonstrateWindowEnumeration();

            // 3. Демонстрация эмуляции ввода
            Console.WriteLine("\n⌨️ Step 3: Demonstrating input simulation...");
            Console.WriteLine("📝 Click on Notepad window and wait 5 seconds for auto-typing...");

            await Task.Delay(5000); // Даем время пользователю кликнуть на Notepad

            await SimulateTextInput();

            Console.WriteLine("\n🎉 Basic automation demo completed!");
            Console.WriteLine("📝 Check Notepad to see if text was typed automatically!");

        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during automation demo: {ex.Message}");
        }
    }

    private static async Task DemonstrateWindowEnumeration()
    {
        try
        {
            Console.WriteLine("🪟 Enumerating Windows using WinAPI:");

            // Простая демонстрация работы с WinAPI
            var processes = Process.GetProcesses()
                .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                .Take(10)
                .ToList();

            Console.WriteLine($"Found {processes.Count} processes with windows:");

            foreach (var process in processes)
            {
                Console.WriteLine($"   📱 {process.MainWindowTitle} (PID: {process.Id})");
            }

            // Ищем Notepad
            var notepadProcesses = processes.Where(p =>
                p.ProcessName.Contains("notepad", StringComparison.OrdinalIgnoreCase)).ToList();

            if (notepadProcesses.Any())
            {
                Console.WriteLine($"✅ Found {notepadProcesses.Count} Notepad instance(s)");
            }
            else
            {
                Console.WriteLine("❌ No Notepad processes found with windows");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Error in window enumeration: {ex.Message}");
        }

        await Task.Delay(1000); // Small delay for readability
    }

    private static async Task SimulateTextInput()
    {
        try
        {
            Console.WriteLine("⌨️ Simulating text input...");

            // Простая эмуляция ввода через SendKeys (работает только если окно активно)
            var currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var testText = $"AutomationCore Demo - {currentTime}\r\n\r\n" +
                          "This text was automatically typed by the AutomationCore demo!\r\n\r\n" +
                          "Features that WILL be available once fully integrated:\r\n" +
                          "✅ Process automation\r\n" +
                          "✅ Advanced window management\r\n" +
                          "✅ Precise input simulation\r\n" +
                          "✅ Image recognition and template matching\r\n" +
                          "✅ Workflow automation\r\n\r\n" +
                          "AutomationCore architecture is ready! 🚀";

            // Используем простой SendKeys для демонстрации
            System.Windows.Forms.SendKeys.SendWait(testText);

            Console.WriteLine("✅ Text input simulation completed!");
            Console.WriteLine("📝 Text should appear in the active window (Notepad)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not simulate input: {ex.Message}");
            Console.WriteLine("💡 Make sure Notepad window is active and focused");
        }

        await Task.Delay(1000);
    }
}
