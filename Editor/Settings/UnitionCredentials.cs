/*
Copyright (c) 2026 Xavier Arpa López Thomas Peter ('xavierarpa')

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor
{
    public static class UnitionCredentials
    {
        private const string EnvVarName = "NOTION_API_TOKEN";
        private const string TokenPrefix = "Unition_Token_";
        private const string SourcePrefix = "Unition_TokenSource_";

        private static string PrefsKey => TokenPrefix + PlayerSettings.productName;
        private static string SourceKey => SourcePrefix + PlayerSettings.productName;

        public static TokenSource CurrentSource
        {
            get
            {
                var val = EditorPrefs.GetString(SourceKey, "EditorPrefs");
                if (Enum.TryParse<TokenSource>(val, out var src))
                {
                    return src;
                }
                return TokenSource.EditorPrefs;
            }
            set => EditorPrefs.SetString(SourceKey, value.ToString());
        }

        public static string Token
        {
            get
            {
                if (CurrentSource == TokenSource.EnvironmentVariable)
                {
                    var envToken = Environment.GetEnvironmentVariable(EnvVarName);
                    if (!string.IsNullOrEmpty(envToken))
                    {
                        return envToken;
                    }
                }

                var encrypted = EditorPrefs.GetString(PrefsKey, string.Empty);
                if (string.IsNullOrEmpty(encrypted))
                {
                    return string.Empty;
                }

                try
                {
                    return Decrypt(encrypted);
                }
                catch
                {
                    return encrypted;
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    EditorPrefs.DeleteKey(PrefsKey);
                }
                else
                {
                    EditorPrefs.SetString(PrefsKey, Encrypt(value));
                }
            }
        }

        public static bool HasToken => !string.IsNullOrEmpty(Token);

        public static bool HasEnvironmentToken => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(EnvVarName));

        public static void Clear()
        {
            EditorPrefs.DeleteKey(PrefsKey);
            EditorPrefs.DeleteKey(SourceKey);
        }

        private static byte[] DeriveKey()
        {
            var seed = Application.dataPath + PlayerSettings.productName;
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
            }
        }

        private static string Encrypt(string plainText)
        {
            var key = DeriveKey();
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        var bytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(bytes, 0, bytes.Length);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private static string Decrypt(string cipherText)
        {
            var allBytes = Convert.FromBase64String(cipherText);
            var key = DeriveKey();
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                var iv = new byte[16];
                Array.Copy(allBytes, 0, iv, 0, 16);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(allBytes, 16, allBytes.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
