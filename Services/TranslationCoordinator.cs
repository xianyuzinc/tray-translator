using System.Collections.Generic;
using TrayTranslator.Models;
using TrayTranslator.Translators;

namespace TrayTranslator.Services
{
    public class TranslationCoordinator
    {
        public IReadOnlyList<ITranslator> CreateTranslators(AppSettings settings)
        {
            var translators = new List<ITranslator>();

            if (settings.DeepLEnabled)
            {
                translators.Add(new DeepLTranslator(settings));
            }

            if (settings.GoogleEnabled)
            {
                translators.Add(new GoogleTranslator(settings));
            }

            if (settings.BaiduEnabled)
            {
                translators.Add(new BaiduTranslator(settings));
            }

            if (settings.DeepSeekEnabled)
            {
                translators.Add(new DeepSeekTranslator(settings));
            }

            return translators;
        }
    }
}
