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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Unition.Models;

using UnityEngine;
using UnityEngine.Networking;

namespace Unition.Editor.Sync
{
    public static class NotionFileDownloader
    {
        public static async Task<List<NotionDownloadedFile>> DownloadAllAsync(
            List<NotionPage> pages,
            List<NotionPropertyMapping> filesMappings,
            string downloadPath,
            CancellationToken ct = default)
        {
            EnsureDirectoryExists(downloadPath);
            var results = new List<NotionDownloadedFile>();

            var urlToPageInfo = new Dictionary<string, (string pageId, string propertyName)>();

            foreach (var page in pages)
            {
                if (page.Archived || page.Properties == null)
                {
                    continue;
                }

                foreach (var mapping in filesMappings)
                {
                    if (!page.Properties.TryGetValue(mapping.NotionPropertyName, out var propValue))
                    {
                        continue;
                    }

                    if (propValue.Type != "files")
                    {
                        continue;
                    }

                    var urls = ExtractFileUrls(propValue);
                    foreach (var url in urls)
                    {
                        if (!urlToPageInfo.ContainsKey(url))
                        {
                            urlToPageInfo[url] = (page.Id, mapping.NotionPropertyName);
                        }
                    }
                }
            }

            foreach (var kvp in urlToPageInfo)
            {
                ct.ThrowIfCancellationRequested();

                var url = kvp.Key;
                var (pageId, propertyName) = kvp.Value;
                var fileName = ExtractFileName(url);
                var localPath = Path.Combine(downloadPath, fileName);
                var assetPath = localPath.Replace("\\", "/");

                var entry = new NotionDownloadedFile
                {
                    NotionPageId = pageId,
                    LocalAssetPath = assetPath,
                    PropertyName = propertyName
                };

                if (File.Exists(localPath))
                {
                    results.Add(entry);
                    continue;
                }

                try
                {
                    await DownloadFileAsync(url, localPath, ct);
                    results.Add(entry);
                    Debug.Log($"[Unition] Downloaded: {fileName}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Unition] Failed to download '{fileName}': {ex.Message}");
                }
            }

            return results;
        }

        private static List<string> ExtractFileUrls(NotionPropertyValue property)
        {
            var urls = new List<string>();

            if (property.Value == null || property.Value.Type != JTokenType.Array)
            {
                return urls;
            }

            foreach (var item in property.Value)
            {
                var type = item["type"]?.ToString();
                string url = null;

                if (type == "external")
                {
                    url = item["external"]?["url"]?.ToString();
                }
                else if (type == "file")
                {
                    url = item["file"]?["url"]?.ToString();
                }

                if (!string.IsNullOrEmpty(url))
                {
                    urls.Add(url);
                }
            }

            return urls;
        }

        private static string ExtractFileName(string url)
        {
            try
            {
                var uri = new Uri(url);
                var path = uri.AbsolutePath;
                var name = Path.GetFileName(path);

                if (!string.IsNullOrEmpty(name))
                {
                    return SanitizeFileName(Uri.UnescapeDataString(name));
                }
            }
            catch
            {
                // ignored
            }

            return "downloaded_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }

            return name.Trim();
        }

        private static async Task DownloadFileAsync(string url, string localPath, CancellationToken ct)
        {
            var tmpPath = localPath + ".unition_tmp";
            long existingBytes = 0;

            if (File.Exists(tmpPath))
            {
                existingBytes = new FileInfo(tmpPath).Length;
            }

            using (var request = UnityWebRequest.Get(url))
            {
                if (existingBytes > 0)
                {
                    request.SetRequestHeader("Range", $"bytes={existingBytes}-");
                }

                var operation = request.SendWebRequest();

                while (!operation.isDone)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    throw new Exception(request.error);
                }

                var dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = request.downloadHandler.data;
                var isPartial = request.responseCode == 206;

                if (isPartial && existingBytes > 0)
                {
                    using (var fs = new FileStream(tmpPath, FileMode.Append, FileAccess.Write))
                    {
                        fs.Write(data, 0, data.Length);
                    }
                }
                else
                {
                    File.WriteAllBytes(tmpPath, data);
                }

                if (File.Exists(localPath))
                {
                    File.Delete(localPath);
                }
                File.Move(tmpPath, localPath);
            }
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
    }
}
