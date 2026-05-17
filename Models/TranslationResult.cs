using System;

namespace TrayTranslator.Models
{
    public class TranslationResult
    {
        public string EngineName { get; set; }
        public bool Success { get; set; }
        public string Text { get; set; }
        public string Error { get; set; }
        public TimeSpan Elapsed { get; set; }

        public static TranslationResult Skipped(string engineName, string reason)
        {
            return new TranslationResult
            {
                EngineName = engineName,
                Success = false,
                Text = "",
                Error = reason,
                Elapsed = TimeSpan.Zero
            };
        }
    }
}
