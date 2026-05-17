using System.Windows.Forms;

namespace TrayTranslator.Models
{
    public class AppSettings
    {
        public bool IsEnabled { get; set; } = true;
        public string SourceLanguage { get; set; } = "auto";
        public string TargetLanguage { get; set; } = "zh";
        public int MaxCharacters { get; set; } = 2000;
        public float UiFontSize { get; set; } = 10.5F;
        public int HotkeyModifiers { get; set; } = 0x0002 | 0x0004;
        public int HotkeyKey { get; set; } = (int)Keys.T;

        public bool GoogleEnabled { get; set; } = true;
        public string GoogleApiKeyProtected { get; set; } = "";

        public bool DeepLEnabled { get; set; } = true;
        public string DeepLApiKeyProtected { get; set; } = "";

        public bool BaiduEnabled { get; set; } = true;
        public string BaiduApiKeyProtected { get; set; } = "";
        public string BaiduAppId { get; set; } = "";
        public string BaiduSecretKeyProtected { get; set; } = "";

        public bool DeepSeekEnabled { get; set; } = true;
        public string DeepSeekApiKeyProtected { get; set; } = "";
        public string DeepSeekBaseUrl { get; set; } = "https://api.deepseek.com";
        public string DeepSeekModel { get; set; } = "deepseek-v4-flash";
    }
}
