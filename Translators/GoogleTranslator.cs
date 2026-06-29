using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TrayTranslator.Models;
using TrayTranslator.Services;

namespace TrayTranslator.Translators
{
    public class GoogleTranslator : ITranslator
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private readonly AppSettings _settings;
        private readonly string _apiKey;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public GoogleTranslator(AppSettings settings)
        {
            _settings = settings;
            _apiKey = SecretProtector.Unprotect(settings.GoogleApiKeyProtected);
        }

        public string Name => "Google";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                string url = "https://translation.googleapis.com/language/translate/v2?key=" + Uri.EscapeDataString(_apiKey);
                var body = new Dictionary<string, object>
                {
                    ["q"] = text,
                    ["target"] = _settings.TargetLanguage,
                    ["format"] = "text"
                };

                if (!string.Equals(_settings.SourceLanguage, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    body["source"] = _settings.SourceLanguage;
                }

                string json = _serializer.Serialize(body);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (HttpResponseMessage response = await Client.PostAsync(url, content, cancellationToken).ConfigureAwait(false))
                {
                    string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Failure("Google 请求失败：" + (int)response.StatusCode + " " + response.ReasonPhrase, watch.Elapsed);
                    }

                    Dictionary<string, object> root = _serializer.DeserializeObject(responseText) as Dictionary<string, object>;
                    Dictionary<string, object> data = GetDictionary(root, "data");
                    object[] translations = GetArray(data, "translations");
                    if (translations.Length == 0)
                    {
                        return Failure("Google 未返回翻译结果。", watch.Elapsed);
                    }

                    Dictionary<string, object> first = translations[0] as Dictionary<string, object>;
                    string translated = GetString(first, "translatedText");
                    if (string.IsNullOrWhiteSpace(translated))
                    {
                        return Failure("Google 返回内容为空。", watch.Elapsed);
                    }

                    return Success(WebUtility.HtmlDecode(translated), watch.Elapsed);
                }
            }
            catch (TaskCanceledException)
            {
                return Failure("Google 请求超时。", watch.Elapsed);
            }
            catch (Exception ex)
            {
                return Failure("Google 错误：" + ex.Message, watch.Elapsed);
            }
        }

        private TranslationResult Success(string text, TimeSpan elapsed)
        {
            return new TranslationResult { EngineName = Name, Success = true, Text = text, Error = "", Elapsed = elapsed };
        }

        private TranslationResult Failure(string error, TimeSpan elapsed)
        {
            return new TranslationResult { EngineName = Name, Success = false, Text = "", Error = error, Elapsed = elapsed };
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string key)
        {
            if (source != null && source.TryGetValue(key, out object value))
            {
                return value as Dictionary<string, object>;
            }

            return null;
        }

        private static object[] GetArray(Dictionary<string, object> source, string key)
        {
            if (source != null && source.TryGetValue(key, out object value))
            {
                return value as object[] ?? new object[0];
            }

            return new object[0];
        }

        private static string GetString(Dictionary<string, object> source, string key)
        {
            if (source != null && source.TryGetValue(key, out object value) && value != null)
            {
                return Convert.ToString(value);
            }

            return "";
        }
    }
}
