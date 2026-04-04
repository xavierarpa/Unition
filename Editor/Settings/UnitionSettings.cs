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
using System.Collections.Generic;
using System.IO;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor
{
    [FilePath("UserSettings/UnitionSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class UnitionSettings : ScriptableSingleton<UnitionSettings>
    {
        [SerializeField] private string apiVersion = "2022-06-28";
        [SerializeField] private List<string> favoriteDatabaseIds = new List<string>();
        [SerializeField] private string proxyUrl = string.Empty;
        [SerializeField] private bool validateTokenOnStartup = true;

        public string ApiVersion => apiVersion;
        public IReadOnlyList<string> FavoriteDatabaseIds => favoriteDatabaseIds;

        public string ProxyUrl
        {
            get => proxyUrl;
            set
            {
                proxyUrl = value;
                Save(true);
            }
        }

        public bool ValidateTokenOnStartup
        {
            get => validateTokenOnStartup;
            set
            {
                validateTokenOnStartup = value;
                Save(true);
            }
        }

        public bool IsFavorite(string databaseId)
        {
            return favoriteDatabaseIds.Contains(databaseId);
        }

        public void ToggleFavorite(string databaseId)
        {
            if (favoriteDatabaseIds.Contains(databaseId))
            {
                favoriteDatabaseIds.Remove(databaseId);
            }
            else
            {
                favoriteDatabaseIds.Add(databaseId);
            }
            Save(true);
        }

        public void SetApiVersion(string version)
        {
            apiVersion = version;
            Save(true);
        }
    }
}
