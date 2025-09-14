// AutomationCore/Core/Capture/Contracts.cs
using AutomationCore.Core.Abstractions;
using AutomationCore.Core.Models;
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
