// AutomationCore/Core/Capture/Contracts.cs
using AutomationCore.Capture;
using AutomationCore.Core.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace AutomationCore.Core.Capture
{
    public interface ICaptureFactory
    {
        IScreenCapture CreateCapture();
    }

    public interface ICaptureService
    {
        Task<CaptureResult> CaptureWindowAsync(string windowTitle, CaptureSettings settings = null);
        Task<string> StartSessionAsync(string sessionId, CaptureRequest request);
    }
}
