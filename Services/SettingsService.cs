using System;
using System.IO;
using System.Web.Script.Serialization;
using TrayTranslator.Models;

namespace TrayTranslator.Services
{
    public class SettingsService
    {
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public string SettingsDirectory { get; }
        public string SettingsPath { get; }
        public bool LastLoadFailed { get; private set; }
        public string LastLoadError { get; private set; }

        public SettingsService()
        {
            SettingsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TrayTranslator");
            SettingsPath = Path.Combine(SettingsDirectory, "settings.json");
        }

        public AppSettings Load()
        {
            LastLoadFailed = false;
            LastLoadError = "";

            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AppSettings();
                }

                string json = File.ReadAllText(SettingsPath);
                AppSettings settings = _serializer.Deserialize<AppSettings>(json);
                return Normalize(settings ?? new AppSettings());
            }
            catch (Exception ex)
            {
                LastLoadFailed = true;
                LastLoadError = ex.Message;
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            if (LastLoadFailed && File.Exists(SettingsPath))
            {
                return;
            }

            Directory.CreateDirectory(SettingsDirectory);
            string json = _serializer.Serialize(Normalize(settings));
            File.WriteAllText(SettingsPath, PrettyJson(json));
        }

        private static AppSettings Normalize(AppSettings settings)
        {
            if (settings.MaxCharacters < 100)
            {
                settings.MaxCharacters = 100;
            }

            if (settings.MaxCharacters > 6000)
            {
                settings.MaxCharacters = 6000;
            }

            if (settings.UiFontSize < 9F)
            {
                settings.UiFontSize = 10.5F;
            }

            if (settings.UiFontSize > 16F)
            {
                settings.UiFontSize = 16F;
            }

            if (string.IsNullOrWhiteSpace(settings.SourceLanguage))
            {
                settings.SourceLanguage = "auto";
            }

            if (string.IsNullOrWhiteSpace(settings.TargetLanguage))
            {
                settings.TargetLanguage = "zh";
            }

            if (string.IsNullOrWhiteSpace(settings.DeepSeekBaseUrl))
            {
                settings.DeepSeekBaseUrl = "https://api.deepseek.com";
            }

            if (string.IsNullOrWhiteSpace(settings.DeepSeekModel))
            {
                settings.DeepSeekModel = "deepseek-v4-flash";
            }

            if (settings.HotkeyKey == 0)
            {
                settings.HotkeyKey = (int)System.Windows.Forms.Keys.T;
            }

            if (settings.HotkeyModifiers == 0)
            {
                settings.HotkeyModifiers = 0x0002 | 0x0004;
            }

            return settings;
        }

        private static string PrettyJson(string json)
        {
            int indent = 0;
            bool quoted = false;
            var builder = new System.Text.StringBuilder();

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                switch (ch)
                {
                    case '"':
                        builder.Append(ch);
                        bool escaped = false;
                        int index = i;
                        while (index > 0 && json[--index] == '\\')
                        {
                            escaped = !escaped;
                        }
                        if (!escaped)
                        {
                            quoted = !quoted;
                        }
                        break;
                    case '{':
                    case '[':
                        builder.Append(ch);
                        if (!quoted)
                        {
                            builder.AppendLine();
                            builder.Append(new string(' ', ++indent * 2));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            builder.AppendLine();
                            builder.Append(new string(' ', --indent * 2));
                        }
                        builder.Append(ch);
                        break;
                    case ',':
                        builder.Append(ch);
                        if (!quoted)
                        {
                            builder.AppendLine();
                            builder.Append(new string(' ', indent * 2));
                        }
                        break;
                    case ':':
                        builder.Append(ch);
                        if (!quoted)
                        {
                            builder.Append(' ');
                        }
                        break;
                    default:
                        builder.Append(ch);
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
