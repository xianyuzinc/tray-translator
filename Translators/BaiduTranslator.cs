using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using TrayTranslator.Models;
using TrayTranslator.Services;

namespace TrayTranslator.Translators
{
    public class BaiduTranslator : ITranslator
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        private static readonly SemaphoreSlim RateLimit = new SemaphoreSlim(1, 1);
        private static DateTime _lastRequestUtc = DateTime.MinValue;

        private readonly AppSettings _settings;
        private readonly string _apiKey;
        private readonly string _secretKey;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public BaiduTranslator(AppSettings settings)
        {
            _settings = settings;
            _apiKey = SecretProtector.Unprotect(settings.BaiduApiKeyProtected);
            _secretKey = SecretProtector.Unprotect(settings.BaiduSecretKeyProtected);
        }

        public string Name => "百度";
        public bool IsEnabled => _settings.BaiduEnabled;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) ||
                                    (!string.IsNullOrWhiteSpace(_settings.BaiduAppId) && !string.IsNullOrWhiteSpace(_secretKey));

        public async Task<TranslationResult> TranslateAsync(string text, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            try
            {
                await RateLimit.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    TimeSpan sinceLast = DateTime.UtcNow - _lastRequestUtc;
                    if (sinceLast < TimeSpan.FromMilliseconds(1100))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(1100) - sinceLast, cancellationToken).ConfigureAwait(false);
                    }

                    _lastRequestUtc = DateTime.UtcNow;
                }
                finally
                {
                    RateLimit.Release();
                }

                if (!string.IsNullOrWhiteSpace(_apiKey))
                {
                    return await TranslateWithApiKeyAsync(text, watch, cancellationToken).ConfigureAwait(false);
                }

                return await TranslateWithSignAsync(text, watch, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return Failure("百度请求超时。", watch.Elapsed);
            }
            catch (Exception ex)
            {
                return Failure("百度错误：" + ex.Message, watch.Elapsed);
            }
        }

        private async Task<TranslationResult> TranslateWithApiKeyAsync(string text, Stopwatch watch, CancellationToken cancellationToken)
        {
            string to = NormalizeBaiduTarget(_settings.TargetLanguage);
            string from = NormalizeBaiduSource(_settings.SourceLanguage);

            var body = new Dictionary<string, object>
            {
                ["q"] = text,
                ["from"] = from,
                ["to"] = to,
                ["model_type"] = "llm",
                ["reference"] = BuildReference(_settings.TargetLanguage)
            };

            if (!string.IsNullOrWhiteSpace(_settings.BaiduAppId))
            {
                body["appid"] = _settings.BaiduAppId;
            }

            string json = _serializer.Serialize(body);
            using (var request = new HttpRequestMessage(HttpMethod.Post, "https://fanyi-api.baidu.com/ait/api/aiTextTranslate"))
            {
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _apiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (HttpResponseMessage response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Failure("百度请求失败：" + (int)response.StatusCode + " " + response.ReasonPhrase, watch.Elapsed);
                    }

                    return ParseBaiduResult(responseText, watch);
                }
            }
        }

        private async Task<TranslationResult> TranslateWithSignAsync(string text, Stopwatch watch, CancellationToken cancellationToken)
        {
            string salt = ((long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds).ToString();
            string sign = Md5Lower(_settings.BaiduAppId + text + salt + _secretKey);
            string to = NormalizeBaiduTarget(_settings.TargetLanguage);
            string from = NormalizeBaiduSource(_settings.SourceLanguage);

            var values = new Dictionary<string, string>
            {
                ["q"] = text,
                ["from"] = from,
                ["to"] = to,
                ["appid"] = _settings.BaiduAppId,
                ["salt"] = salt,
                ["sign"] = sign
            };

            using (var content = new FormUrlEncodedContent(values))
            using (HttpResponseMessage response = await Client.PostAsync("https://fanyi-api.baidu.com/api/trans/vip/translate", content, cancellationToken).ConfigureAwait(false))
            {
                string responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    return Failure("百度请求失败：" + (int)response.StatusCode + " " + response.ReasonPhrase, watch.Elapsed);
                }

                return ParseBaiduResult(responseText, watch);
            }
        }

        private TranslationResult ParseBaiduResult(string responseText, Stopwatch watch)
        {
            Dictionary<string, object> root = _serializer.DeserializeObject(responseText) as Dictionary<string, object>;
            string errorCode = GetString(root, "error_code");
            if (!string.IsNullOrWhiteSpace(errorCode))
            {
                return Failure("百度错误 " + errorCode + "：" + GetString(root, "error_msg"), watch.Elapsed);
            }

            object[] results = GetArray(root, "trans_result");
            if (results.Length == 0)
            {
                Dictionary<string, object> resultDict = GetDictionary(root, "result");
                results = GetArray(resultDict, "trans_result");
            }

            if (results.Length == 0)
            {
                return Failure("百度未返回翻译结果。", watch.Elapsed);
            }

            var builder = new StringBuilder();
            foreach (object item in results)
            {
                var result = item as Dictionary<string, object>;
                string dst = GetString(result, "dst");
                if (!string.IsNullOrWhiteSpace(dst))
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }
                    builder.Append(dst);
                }
            }

            if (builder.Length == 0)
            {
                return Failure("百度返回内容为空。", watch.Elapsed);
            }

            return Success(builder.ToString(), watch.Elapsed);
        }

        private TranslationResult Success(string text, TimeSpan elapsed)
        {
            return new TranslationResult { EngineName = Name, Success = true, Text = text, Error = "", Elapsed = elapsed };
        }

        private TranslationResult Failure(string error, TimeSpan elapsed)
        {
            return new TranslationResult { EngineName = Name, Success = false, Text = "", Error = error, Elapsed = elapsed };
        }

        private static string BuildReference(string target)
        {
            string targetLanguage = DescribeTargetLanguage(target);
            return "无论原文是否为中英混杂、术语夹杂或多语言混合，都翻译为自然准确的" + targetLanguage
                   + "；只输出译文，保留公式、代码、变量名、参考文献编号和必须保留的专有名词。";
        }

        private static string DescribeTargetLanguage(string target)
        {
            if (string.Equals(target, "en", StringComparison.OrdinalIgnoreCase)) return "英文";
            if (string.Equals(target, "ja", StringComparison.OrdinalIgnoreCase)) return "日文";
            if (string.Equals(target, "ko", StringComparison.OrdinalIgnoreCase)) return "韩文";
            if (string.Equals(target, "fr", StringComparison.OrdinalIgnoreCase)) return "法文";
            if (string.Equals(target, "de", StringComparison.OrdinalIgnoreCase)) return "德文";
            if (string.Equals(target, "es", StringComparison.OrdinalIgnoreCase)) return "西班牙文";
            return "简体中文";
        }

        private static string NormalizeBaiduTarget(string target)
        {
            if (string.Equals(target, "zh-CN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, "zh_cn", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target, "zh-Hans", StringComparison.OrdinalIgnoreCase))
            {
                return "zh";
            }

            if (string.Equals(target, "ja", StringComparison.OrdinalIgnoreCase))
            {
                return "jp";
            }

            if (string.Equals(target, "ko", StringComparison.OrdinalIgnoreCase))
            {
                return "kor";
            }

            if (string.Equals(target, "fr", StringComparison.OrdinalIgnoreCase))
            {
                return "fra";
            }

            if (string.Equals(target, "es", StringComparison.OrdinalIgnoreCase))
            {
                return "spa";
            }

            return string.IsNullOrWhiteSpace(target) ? "zh" : target;
        }

        private static string NormalizeBaiduSource(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || string.Equals(source, "auto", StringComparison.OrdinalIgnoreCase))
            {
                return "auto";
            }

            return NormalizeBaiduTarget(source);
        }

        private static string Md5Lower(string text)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
                var builder = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
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
