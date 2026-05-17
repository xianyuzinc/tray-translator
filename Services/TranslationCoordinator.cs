using System.Collections.Generic;
using TrayTranslator.Models;
using TrayTranslator.Translators;

namespace TrayTranslator.Services
{
    public class TranslationCoordinator
    {
        public IReadOnlyList<ITranslator> CreateTranslators(AppSettings settings)
        {
            return new ITranslator[]
            {
                new DeepLTranslator(settings),
                new GoogleTranslator(settings),
                new BaiduTranslator(settings),
                new DeepSeekTranslator(settings)
            };
        }
    }
}
