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
using System.Threading;
using System.Threading.Tasks;

using Unition.Models;

namespace Unition.Editor.Sync
{
    public static class NotionSchemaValidator
    {
        public static async Task<NotionSchemaValidationResult> ValidateAsync(
            NotionSyncProfile profile,
            CancellationToken ct = default)
        {
            var result = new NotionSchemaValidationResult();
            var client = UnitionEditorClient.Client;

            if (client == null)
            {
                result.Errors.Add("No Notion client available. Check your API token.");
                return result;
            }

            if (string.IsNullOrEmpty(profile.DatabaseId))
            {
                result.Errors.Add("Profile has no database ID configured.");
                return result;
            }

            NotionDatabase database;
            try
            {
                if (!NotionCache.TryGetDatabase(profile.DatabaseId, out database))
                {
                    database = await client.GetDatabaseAsync(profile.DatabaseId, ct);
                    NotionCache.SetDatabase(profile.DatabaseId, database);
                }
            }
            catch (System.Exception ex)
            {
                result.Errors.Add($"Failed to fetch database schema: {ex.Message}");
                return result;
            }

            if (database.Properties == null || database.Properties.Count == 0)
            {
                result.Warnings.Add("Database has no properties defined.");
                return result;
            }

            var enabledMappings = profile.PropertyMappings.FindAll(m => m.Enabled);

            if (enabledMappings.Count == 0)
            {
                result.Warnings.Add("No enabled property mappings in profile.");
                return result;
            }

            foreach (var mapping in enabledMappings)
            {
                if (string.IsNullOrEmpty(mapping.NotionPropertyName))
                {
                    result.Warnings.Add(
                        $"Mapping for field '{mapping.TargetFieldName}' has no Notion property name.");
                    continue;
                }

                if (!database.Properties.TryGetValue(mapping.NotionPropertyName, out var schema))
                {
                    result.Errors.Add(
                        $"Property '{mapping.NotionPropertyName}' does not exist in database '{database.PlainTitle}'.");
                    continue;
                }

                if (!string.IsNullOrEmpty(mapping.NotionPropertyType)
                    && schema.Type != mapping.NotionPropertyType)
                {
                    result.Errors.Add(
                        $"Property '{mapping.NotionPropertyName}' type mismatch: " +
                        $"expected '{mapping.NotionPropertyType}', actual '{schema.Type}'.");
                }
            }

            return result;
        }
    }
}
