using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TrayTranslator.Models;
using TrayTranslator.Services;

namespace TrayTranslator.Translators
{
    public class DeepSeekTranslator : ITranslator
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        private readonly AppSettings _settings;
        private readonly string _apiKey;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public DeepSeekTranslator(AppSettings settings)
        {
            _settings = settings;
            _apiKey = SecretProtector.Unprotect(settings.DeepSeekApiKeyProtected);
        }

        public string Name => "AI";
        public bool IsEnabled => _settings.DeepSeekEnabled;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public async Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                string baseUrl = (_settings.DeepSeekBaseUrl ?? "https://api.deepseek.com").TrimEnd('/');
                string url = baseUrl + "/chat/completions";
                string targetLanguage = DescribeTargetLanguage(_settings.TargetLanguage);

                var body = new Dictionary<string, object>
                {
                    ["model"] = _settings.DeepSeekModel,
                    ["stream"] = false,
                    ["temperature"] = 0.1,
                    ["messages"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["role"] = "system",
                            ["content"] = BuildSystemPrompt(targetLanguage)
                        },
                        new Dictionary<string, object>
                        {
                            ["role"] = "user",
                            ["content"] = text
                        }
                    }
                };

                string json = _serializer.Serialize(body);
                using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                    using (HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                    {
                        string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            return Failure("DeepSeek 请求失败：" + (int)response.StatusCode + " " + response.ReasonPhrase, watch.Elapsed);
                        }

                        Dictionary<string, object> root = _serializer.DeserializeObject(responseText) as Dictionary<string, object>;
                        object[] choices = GetArray(root, "choices");
                        if (choices.Length == 0)
                        {
                            return Failure("DeepSeek 未返回翻译结果。", watch.Elapsed);
                        }

                        Dictionary<string, object> first = choices[0] as Dictionary<string, object>;
                        Dictionary<string, object> message = GetDictionary(first, "message");
                        string translated = GetString(message, "content").Trim();
                        if (string.IsNullOrWhiteSpace(translated))
                        {
                            return Failure("DeepSeek 返回内容为空。", watch.Elapsed);
                        }

                        return Success(translated, watch.Elapsed);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return Failure("DeepSeek 请求超时。", watch.Elapsed);
            }
            catch (Exception ex)
            {
                return Failure("DeepSeek 错误：" + ex.Message, watch.Elapsed);
            }
        }

        private static string BuildSystemPrompt(string targetLanguage)
        {
            return "你是专业翻译引擎。将用户输入完整翻译为" + targetLanguage + "。"
                   + "如果源文本是中英混杂、术语夹杂或多语言混合，也必须全部转成" + targetLanguage + "。"
                   + "只输出译文，不要解释，不要添加标题，不要输出原文。"
                   + "专业术语、公式、代码、变量名、参考文献编号和必须保留的专有名词可以保留原样；其余自然语言内容必须翻译。";
        }

        private static string DescribeTargetLanguage(string target)
        {
            if (string.Equals(target, "en", StringComparison.OrdinalIgnoreCase))
            {
                return "英文";
            }

            if (string.Equals(target, "ja", StringComparison.OrdinalIgnoreCase))
            {
                return "日文";
            }

            if (string.Equals(target, "ko", StringComparison.OrdinalIgnoreCase))
            {
                return "韩文";
            }

            if (string.Equals(target, "fr", StringComparison.OrdinalIgnoreCase))
            {
                return "法文";
            }

            if (string.Equals(target, "de", StringComparison.OrdinalIgnoreCase))
            {
                return "德文";
            }

            if (string.Equals(target, "es", StringComparison.OrdinalIgnoreCase))
            {
                return "西班牙文";
            }

            return "简体中文";
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
