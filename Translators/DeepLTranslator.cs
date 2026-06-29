using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TrayTranslator.Models;
using TrayTranslator.Services;

namespace TrayTranslator.Translators
{
    public class DeepLTranslator : ITranslator
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private readonly AppSettings _settings;
        private readonly string _apiKey;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public DeepLTranslator(AppSettings settings)
        {
            _settings = settings;
            _apiKey = SecretProtector.Unprotect(settings.DeepLApiKeyProtected);
        }

        public string Name => "DeepL";
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                string endpoint = _apiKey.EndsWith(":fx", StringComparison.OrdinalIgnoreCase)
                    ? "https://api-free.deepl.com/v2/translate"
                    : "https://api.deepl.com/v2/translate";

                var values = new Dictionary<string, string>
                {
                    ["text"] = text,
                    ["target_lang"] = NormalizeTarget(_settings.TargetLanguage)
                };

                string source = NormalizeSource(_settings.SourceLanguage);
                if (!string.IsNullOrWhiteSpace(source))
                {
                    values["source_lang"] = source;
                }

                using (var request = new HttpRequestMessage(HttpMethod.Post, endpoint))
                {
                    request.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + _apiKey);
                    request.Content = new FormUrlEncodedContent(values);

                    using (HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            return Failure("DeepL 请求失败：" + (int)response.StatusCode + " " + response.ReasonPhrase, watch.Elapsed);
                        }

                        Dictionary<string, object> root = _serializer.DeserializeObject(responseText) as Dictionary<string, object>;
                        object[] translations = GetArray(root, "translations");
                        if (translations.Length == 0)
                        {
                            return Failure("DeepL 未返回翻译结果。", watch.Elapsed);
                        }

                        Dictionary<string, object> first = translations[0] as Dictionary<string, object>;
                        string translated = GetString(first, "text");
                        if (string.IsNullOrWhiteSpace(translated))
                        {
                            return Failure("DeepL 返回内容为空。", watch.Elapsed);
                        }

                        return Success(WebUtility.HtmlDecode(translated), watch.Elapsed);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return Failure("DeepL 请求超时。", watch.Elapsed);
            }
            catch (Exception ex)
            {
                return Failure("DeepL 错误：" + ex.Message, watch.Elapsed);
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

        private static string NormalizeTarget(string target)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return "ZH";
            }

            string normalized = target.Trim().Replace("_", "-").ToUpperInvariant();
            if (normalized == "ZH" || normalized == "ZH-CN" || normalized == "ZH-HANS")
            {
                return "ZH";
            }

            if (normalized == "EN")
            {
                return "EN-US";
            }

            if (normalized == "PT")
            {
                return "PT-PT";
            }

            return normalized;
        }

        private static string NormalizeSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || string.Equals(source, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "";
            }

            string normalized = source.Trim().Replace("_", "-").ToUpperInvariant();
            if (normalized == "ZH-CN" || normalized == "ZH-HANS")
            {
                return "ZH";
            }

            return normalized;
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
