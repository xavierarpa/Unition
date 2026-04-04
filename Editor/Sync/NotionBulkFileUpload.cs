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

using UnityEngine;

namespace Unition.Editor.Sync
{
    internal static class NotionBulkFileUpload
    {
        internal static async Task<int> SetExternalFilesAsync(
            string databaseId,
            string filePropertyName,
            IReadOnlyList<(string pageId, string fileName, string externalUrl)> entries,
            CancellationToken ct = default)
        {
            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                Debug.LogError("[Unition] No Notion client available.");
                return 0;
            }

            int updated = 0;

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var properties = new JObject
                    {
                        [filePropertyName] = NotionPropertyBuilder.Files((entry.fileName, entry.externalUrl))
                    };

                    await client.UpdatePageAsync(entry.pageId, properties, ct);
                    updated++;
                    Debug.Log($"[Unition] Set file on page {entry.pageId}: {entry.fileName}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Unition] Failed to set file on page '{entry.pageId}': {ex.Message}");
                }
            }

            return updated;
        }
    }
}
