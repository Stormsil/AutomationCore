using System.Threading.Tasks;
using AutomationCore.Core.Models;
using OpenCvSharp;
using TemplateMatchOptions = AutomationCore.Core.Models.TemplateMatchOptions;
using MatchOptions = AutomationCore.Core.Models.MatchOptions;

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
