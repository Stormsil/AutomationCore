using OpenCvSharp;

namespace AutomationCore.Assets
{
    public interface ITemplateStore : System.IDisposable
    {
        bool Contains(string key);
        Mat GetTemplate(string key); // Возвращает BGR Mat (8UC3)
    }
}
