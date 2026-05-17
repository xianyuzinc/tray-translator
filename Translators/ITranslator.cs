using System.Threading;
using System.Threading.Tasks;
using TrayTranslator.Models;

namespace TrayTranslator.Translators
{
    public interface ITranslator
    {
        string Name { get; }
        bool IsEnabled { get; }
        bool IsConfigured { get; }
        Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken);
    }
}
