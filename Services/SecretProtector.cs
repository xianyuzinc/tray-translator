using System;
using System.Security.Cryptography;
using System.Text;

namespace TrayTranslator.Services
{
    public static class SecretProtector
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TrayTranslator.v1");

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return "";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(plainText);
            byte[] encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return "";
            }

            try
            {
                byte[] encrypted = Convert.FromBase64String(protectedText);
                byte[] bytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return "";
            }
        }
    }
}
