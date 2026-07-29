using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Purrch
{
    /// Stores the image-API key encrypted with Windows DPAPI (per-user), so it
    /// isn't sitting in plain text — the equivalent of the macOS Keychain.
    internal static class KeyStore
    {
        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Purrch", "geminikey.bin");

        public static void Save(string key)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                Directory.CreateDirectory(dir);
                if (string.IsNullOrEmpty(key))
                {
                    if (File.Exists(FilePath)) File.Delete(FilePath);
                    return;
                }
                var enc = ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(FilePath, enc);
            }
            catch { /* best effort */ }
        }

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return "";
                var dec = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return ""; }
        }
    }
}
