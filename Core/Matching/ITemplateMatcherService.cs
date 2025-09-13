using System.Threading.Tasks;
using OpenCvSharp;
using static AutomationCore.Core.EnhancedScreenCapture;
using AutomationCore.Core.Abstractions;
using AutomationCore.Assets;

namespace AutomationCore.Core.Matching
{
    public interface ITemplateMatcherService
    {
        // Для сценариев, где у нас уже есть Mat источника
        Task<MatchResult> FindAsync(string templateKey, Mat sourceImage, MatchOptions options = null);

        // Удобная перегрузка для фасада (AutomationEngine) с TemplateMatchOptions
        Task<MatchResult> FindAsync(string templateKey, TemplateMatchOptions presets);
    }
}
