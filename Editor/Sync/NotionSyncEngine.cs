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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Unition.Models;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.Sync
{
    public static class NotionSyncEngine
    {
        private const int LocalModificationGraceSeconds = 2;

        public static async Task<NotionSyncResult> SyncAsync(
            NotionSyncProfile profile,
            CancellationToken ct = default,
            IProgress<(int current, int total)> progress = null)
        {
            var result = new NotionSyncResult();
            var client = UnitionEditorClient.Client;

            if (client == null)
            {
                result.Errors.Add("No Notion client available. Check your API token.");
                return result;
            }

            if (string.IsNullOrEmpty(profile.DatabaseId))
            {
                result.Errors.Add("Database ID is not set in the sync profile.");
                return result;
            }

            var targetType = profile.ResolveTargetType();
            if (targetType == null && profile.OutputFormat == NotionOutputFormat.ScriptableObject)
            {
                result.Errors.Add($"Cannot resolve target type: {profile.TargetTypeName}");
                return result;
            }

            List<NotionPage> pages;
            bool isDeltaSync = false;

            bool hasMissingAssets = false;
            for (int i = profile.SyncEntries.Count - 1; i >= 0; i--)
            {
                var e = profile.SyncEntries[i];
                var path = AssetDatabase.GUIDToAssetPath(e.LocalAssetGuid);
                if (string.IsNullOrEmpty(path) || AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) == null)
                {
                    e.LocalAssetGuid = null;
                    e.LocalAssetPath = null;
                    e.LastEditedTime = null;
                    e.LocalLastSyncedUtc = null;
                    hasMissingAssets = true;
                }
            }

            try
            {
                JObject filter = null;
                if (!hasMissingAssets &&
                    !string.IsNullOrEmpty(profile.LastSyncedTime) &&
                    DateTime.TryParse(profile.LastSyncedTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var lastSync))
                {
                    filter = new JObject
                    {
                        ["timestamp"] = "last_edited_time",
                        ["last_edited_time"] = new JObject
                        {
                            ["after"] = lastSync.AddMinutes(-2).ToString("o")
                        }
                    };
                    isDeltaSync = true;
                }

                NotionCache.InvalidateDatabase(profile.DatabaseId);
                var freshDb = await client.GetDatabaseAsync(profile.DatabaseId, ct);
                NotionCache.SetDatabase(profile.DatabaseId, freshDb);

                if (targetType != null)
                {
                    await SyncSelectOptionsAsync(client, profile, targetType, freshDb, ct);
                }

                List<string> filterPropertyIds = null;
                var enabledMappings = profile.PropertyMappings.FindAll(m => m.Enabled);
                if (enabledMappings.Count > 0 && freshDb?.Properties != null)
                {
                    filterPropertyIds = new List<string>();
                    foreach (var mapping in enabledMappings)
                    {
                        if (freshDb.Properties.TryGetValue(mapping.NotionPropertyName, out var schema) &&
                            !string.IsNullOrEmpty(schema.Id))
                        {
                            filterPropertyIds.Add(schema.Id);
                        }
                        else
                        {
                            filterPropertyIds = null;
                            break;
                        }
                    }
                    if (filterPropertyIds != null && filterPropertyIds.Count == 0)
                    {
                        filterPropertyIds = null;
                    }
                }

                pages = await client.QueryAllPagesAsync(profile.DatabaseId, filter: filter, filterProperties: filterPropertyIds, ct: ct);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Failed to query Notion database: {ex.Message}");
                return result;
            }

            EnsureDirectoryExists(profile.OutputPath);

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Unition Sync");
            Undo.RegisterCompleteObjectUndo(profile, "Unition Sync");

            if (!string.IsNullOrEmpty(profile.FilesDownloadPath))
            {
                var filesMappings = profile.PropertyMappings.FindAll(m => m.Enabled && m.NotionPropertyType == "files");
                if (filesMappings.Count > 0)
                {
                    var downloadedFiles = await NotionFileDownloader.DownloadAllAsync(pages, filesMappings, profile.FilesDownloadPath, ct);
                    var existingByPath = new Dictionary<string, NotionDownloadedFile>();
                    foreach (var f in profile.DownloadedFiles)
                    {
                        existingByPath[f.LocalAssetPath] = f;
                    }
                    foreach (var f in downloadedFiles)
                    {
                        existingByPath[f.LocalAssetPath] = f;
                    }
                    profile.DownloadedFiles.Clear();
                    profile.DownloadedFiles.AddRange(existingByPath.Values);

                    if (downloadedFiles.Count > 0)
                    {
                        AssetDatabase.Refresh();
                    }
                }
            }

            var existingEntries = new Dictionary<string, NotionSyncEntry>();
            foreach (var entry in profile.SyncEntries)
            {
                existingEntries[entry.NotionPageId] = entry;
            }

            var processedPageIds = new HashSet<string>();
            var totalPages = pages.Count;
            var currentPage = 0;

            foreach (var page in pages)
            {
                if (page.Archived)
                {
                    currentPage++;
                    continue;
                }

                ct.ThrowIfCancellationRequested();
                processedPageIds.Add(page.Id);
                currentPage++;
                progress?.Report((currentPage, totalPages));

                try
                {
                    if (profile.OutputFormat == NotionOutputFormat.ScriptableObject)
                    {
                        await SyncPageAsScriptableObject(profile, page, targetType, existingEntries, isDeltaSync, result, ct);
                    }
                    else
                    {
                        SyncPageAsJson(profile, page, targetType, result);
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Error syncing page '{page.GetTitle() ?? page.Id}': {ex.Message}");
                }
            }

            if (!isDeltaSync)
            {
                var entriesToRemove = new List<NotionSyncEntry>();
                foreach (var entry in profile.SyncEntries)
                {
                    if (!processedPageIds.Contains(entry.NotionPageId))
                    {
                        if (DeleteLocalAsset(entry))
                        {
                            result.Deleted++;
                        }
                        entriesToRemove.Add(entry);
                    }
                }

                foreach (var entry in entriesToRemove)
                {
                    profile.SyncEntries.Remove(entry);
                }
            }
            else
            {
                foreach (var entry in profile.SyncEntries)
                {
                    if (processedPageIds.Contains(entry.NotionPageId))
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(entry.LocalAssetGuid))
                    {
                        continue;
                    }

                    if (!IsLocallyModified(entry))
                    {
                        continue;
                    }

                    var assetPath = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    if (asset == null)
                    {
                        continue;
                    }

                    await HandleLocalOnlyChange(profile, asset, entry, result, ct);
                }
            }

            profile.LastSyncedTime = DateTime.UtcNow.ToString("o");
            profile.LastSyncLog = result.ToSummary();
            profile.MarkDirty();

            Undo.CollapseUndoOperations(undoGroup);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return result;
        }

        private static async Task SyncPageAsScriptableObject(
            NotionSyncProfile profile,
            NotionPage page,
            Type targetType,
            Dictionary<string, NotionSyncEntry> existingEntries,
            bool isDeltaSync,
            NotionSyncResult result,
            CancellationToken ct)
        {
            bool isNew = false;
            ScriptableObject asset = null;
            NotionSyncEntry entry = null;

            if (existingEntries.TryGetValue(page.Id, out entry))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                }
            }

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance(targetType);
                isNew = true;
                if (entry == null)
                {
                    entry = new NotionSyncEntry { NotionPageId = page.Id };
                }
                else
                {
                    entry.LocalAssetGuid = null;
                    entry.LocalAssetPath = null;
                }
            }

            if (!isNew && !isDeltaSync && entry.LastEditedTime == page.LastEditedTime)
            {
                bool localChanged = IsLocallyModified(entry);
                if (localChanged)
                {
                    await HandleLocalOnlyChange(profile, asset, entry, result, ct);
                }
                else
                {
                    result.Skipped++;
                }
                return;
            }

            if (!isNew)
            {
                bool localChanged = IsLocallyModified(entry);
                bool notionChanged = entry.LastEditedTime != page.LastEditedTime;

                if (localChanged && notionChanged)
                {
                    result.Conflicts++;
                    var resolution = profile.ConflictResolution;

                    if (resolution == NotionConflictResolution.AskUser)
                    {
                        var title = page.GetTitle() ?? page.Id;
                        int choice = EditorUtility.DisplayDialogComplex(
                            "Unition — Conflict",
                            $"'{title}' was modified both locally and in Notion.\nWhich version should win?",
                            "Notion (pull)",
                            "Cancel",
                            "Local (push)");

                        if (choice == 0)
                        {
                            resolution = NotionConflictResolution.NotionWins;
                        }
                        else if (choice == 2)
                        {
                            resolution = NotionConflictResolution.LocalWins;
                        }
                        else
                        {
                            result.Skipped++;
                            return;
                        }
                    }

                    if (resolution == NotionConflictResolution.LocalWins)
                    {
                        await HandleLocalOnlyChange(profile, asset, entry, result, ct);
                        return;
                    }
                }
            }

            if (!isNew)
            {
                Undo.RegisterCompleteObjectUndo(asset, "Unition Sync");
            }

            ApplyMappings(profile, page, asset);

            if (isNew)
            {
                var fileName = GetAssetFileName(profile, page);
                var assetPath = Path.Combine(profile.OutputPath, fileName + ".asset");
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                AssetDatabase.CreateAsset(asset, assetPath);


                entry.LocalAssetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                entry.LocalAssetPath = assetPath;
                if (!profile.SyncEntries.Contains(entry))
                {
                    profile.SyncEntries.Add(entry);
                }

                result.Created++;
            }
            else
            {
                EditorUtility.SetDirty(asset);
                result.Updated++;
            }

            entry.LastEditedTime = page.LastEditedTime;
            entry.LocalLastSyncedUtc = DateTime.UtcNow.ToString("o");
        }

        private static bool IsLocallyModified(NotionSyncEntry entry)
        {
            if (string.IsNullOrEmpty(entry.LocalLastSyncedUtc))
            {
                return false;
            }

            if (!DateTime.TryParse(entry.LocalLastSyncedUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var lastSynced))
            {
                return false;
            }

            var assetPath = string.Empty;
            if (!string.IsNullOrEmpty(entry.LocalAssetGuid))
            {
                assetPath = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
            }

            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = entry.LocalAssetPath;
            }

            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            {
                return false;
            }

            var localModified = File.GetLastWriteTimeUtc(assetPath);
            return localModified > lastSynced.AddSeconds(LocalModificationGraceSeconds);
        }

        private static async Task HandleLocalOnlyChange(
            NotionSyncProfile profile,
            ScriptableObject asset,
            NotionSyncEntry entry,
            NotionSyncResult result,
            CancellationToken ct)
        {
            var error = await NotionWriteBackEngine.PushAsync(asset, profile, entry, ct);
            if (error == null)
            {
                entry.LocalLastSyncedUtc = DateTime.UtcNow.ToString("o");
                result.PushedBack++;
            }
            else
            {
                result.Errors.Add($"Push-back failed for '{asset.name}': {error}");
            }
        }

        private static void SyncPageAsJson(
            NotionSyncProfile profile,
            NotionPage page,
            Type targetType,
            NotionSyncResult result)
        {
            var fileName = GetAssetFileName(profile, page);
            var filePath = Path.Combine(profile.OutputPath, fileName + ".json");
            bool isNew = !File.Exists(filePath);

            var data = new Dictionary<string, object>();
            data["_notionPageId"] = page.Id;
            data["_lastEditedTime"] = page.LastEditedTime;

            foreach (var mapping in profile.PropertyMappings)
            {
                if (!mapping.Enabled)
                {
                    continue;
                }

                if (page.Properties != null && page.Properties.TryGetValue(mapping.NotionPropertyName, out var propValue))
                {
                    var key = string.IsNullOrEmpty(mapping.TargetFieldName)
                        ? mapping.NotionPropertyName
                        : mapping.TargetFieldName;
                    var fieldType = ResolveFieldType(mapping.TargetFieldType);
                    if (fieldType != null)
                    {
                        data[key] = NotionPropertyMapper.Convert(propValue, fieldType);
                    }
                    else
                    {
                        data[key] = propValue.AsPlainText();
                    }
                }
            }

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(filePath, json);

            AssetDatabase.Refresh();
            var assetGuid = AssetDatabase.AssetPathToGUID(filePath);

            if (isNew)
            {
                result.Created++;
                var entry = profile.FindEntryByPageId(page.Id);
                if (entry == null)
                {
                    entry = new NotionSyncEntry
                    {
                        NotionPageId = page.Id,
                        LocalAssetGuid = assetGuid,
                        LocalAssetPath = filePath,
                        LastEditedTime = page.LastEditedTime,
                        LocalLastSyncedUtc = System.DateTime.UtcNow.ToString("o")
                    };
                    profile.SyncEntries.Add(entry);
                }
            }
            else
            {
                result.Updated++;
                var entry = profile.FindEntryByPageId(page.Id);
                if (entry != null)
                {
                    entry.LastEditedTime = page.LastEditedTime;
                    entry.LocalLastSyncedUtc = System.DateTime.UtcNow.ToString("o");
                    if (string.IsNullOrEmpty(entry.LocalAssetGuid))
                    {
                        entry.LocalAssetGuid = assetGuid;
                    }
                }
            }
        }

        private static void ApplyMappings(NotionSyncProfile profile, NotionPage page, UnityEngine.Object target)
        {
            var targetType = target.GetType();

            foreach (var mapping in profile.PropertyMappings)
            {
                if (!mapping.Enabled)
                {
                    continue;
                }

                if (page.Properties == null || !page.Properties.TryGetValue(mapping.NotionPropertyName, out var propValue))
                {
                    continue;
                }

                var field = targetType.GetField(mapping.TargetFieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    continue;
                }

                var converted = NotionPropertyMapper.Convert(propValue, field.FieldType);
                if (converted != null)
                {
                    field.SetValue(target, converted);
                }
            }
        }

        private static string GetAssetFileName(NotionSyncProfile profile, NotionPage page)
        {
            if (!string.IsNullOrEmpty(profile.NamingProperty) && page.Properties != null)
            {
                if (page.Properties.TryGetValue(profile.NamingProperty, out var nameProp))
                {
                    var name = nameProp.AsPlainText();
                    if (!string.IsNullOrEmpty(name))
                    {
                        return SanitizeFileName(name);
                    }
                }
            }

            var title = page.GetTitle();
            if (!string.IsNullOrEmpty(title))
            {
                return SanitizeFileName(title);
            }

            return page.Id.Substring(0, 8);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name.Trim();
        }

        private static bool DeleteLocalAsset(NotionSyncEntry entry)
        {
            if (!string.IsNullOrEmpty(entry.LocalAssetGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return AssetDatabase.DeleteAsset(path);
                }
            }

            if (!string.IsNullOrEmpty(entry.LocalAssetPath) && File.Exists(entry.LocalAssetPath))
            {
                return AssetDatabase.DeleteAsset(entry.LocalAssetPath);
            }

            return false;
        }

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static async Task SyncSelectOptionsAsync(
            Unition.NotionClient client,
            NotionSyncProfile profile,
            Type targetType,
            NotionDatabase freshDb,
            CancellationToken ct)
        {
            if (freshDb?.Properties == null)
            {
                return;
            }

            var updatePayload = new JObject();
            var needsUpdate = false;

            foreach (var mapping in profile.PropertyMappings)
            {
                if (!mapping.Enabled)
                {
                    continue;
                }

                if (mapping.NotionPropertyType != "select" && mapping.NotionPropertyType != "multi_select")
                {
                    continue;
                }

                var field = targetType.GetField(mapping.TargetFieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null || !field.FieldType.IsEnum)
                {
                    continue;
                }

                if (!freshDb.Properties.TryGetValue(mapping.NotionPropertyName, out var schema))
                {
                    continue;
                }

                var existingOptions = schema.GetSelectOptions();
                var existingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var opt in existingOptions)
                {
                    existingNames.Add(opt.Name);
                }

                var enumNames = Enum.GetNames(field.FieldType);
                var missingOptions = new JArray();
                foreach (var name in enumNames)
                {
                    if (!existingNames.Contains(name))
                    {
                        missingOptions.Add(new JObject { ["name"] = name });
                    }
                }

                if (missingOptions.Count > 0)
                {
                    var allOptions = new JArray();
                    foreach (var existing in existingOptions)
                    {
                        allOptions.Add(new JObject { ["name"] = existing.Name });
                    }
                    foreach (var missing in missingOptions)
                    {
                        allOptions.Add(missing);
                    }

                    updatePayload[mapping.NotionPropertyName] = new JObject
                    {
                        [mapping.NotionPropertyType] = new JObject
                        {
                            ["options"] = allOptions
                        }
                    };
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                try
                {
                    await client.UpdateDatabaseAsync(freshDb.Id, updatePayload, ct);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Unition] Failed to sync select options: {ex.Message}");
                }
            }
        }

        private static Type ResolveFieldType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            switch (typeName)
            {
                case "string":
                case "System.String":
                    return typeof(string);
                case "int":
                case "System.Int32":
                    return typeof(int);
                case "float":
                case "System.Single":
                    return typeof(float);
                case "double":
                case "System.Double":
                    return typeof(double);
                case "bool":
                case "System.Boolean":
                    return typeof(bool);
                case "string[]":
                case "System.String[]":
                    return typeof(string[]);
                default:
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var type = assembly.GetType(typeName);
                        if (type != null)
                        {
                            return type;
                        }
                    }
                    return null;
            }
        }

        public static async Task<NotionSyncResult> UploadAssetsAsync(
            NotionSyncProfile profile,
            List<ScriptableObject> assets,
            CancellationToken ct = default)
        {
            var result = new NotionSyncResult();
            var client = UnitionEditorClient.Client;

            if (client == null)
            {
                result.Errors.Add("No Notion client available. Check your API token.");
                return result;
            }

            if (string.IsNullOrEmpty(profile.DatabaseId))
            {
                result.Errors.Add("Database ID is not set in the sync profile.");
                return result;
            }

            if (assets == null || assets.Count == 0)
            {
                return result;
            }

            var existingGuids = new HashSet<string>();
            foreach (var entry in profile.SyncEntries)
            {
                if (!string.IsNullOrEmpty(entry.LocalAssetGuid))
                {
                    existingGuids.Add(entry.LocalAssetGuid);
                }
            }

            var strategy = profile.DuplicateStrategy;
            Dictionary<string, NotionPage> existingPagesByTitle = null;

            if (strategy != NotionDuplicateStrategy.CreateNew)
            {
                try
                {
                    var allPages = await client.QueryAllPagesAsync(profile.DatabaseId, ct: ct);
                    existingPagesByTitle = new Dictionary<string, NotionPage>(StringComparer.OrdinalIgnoreCase);
                    foreach (var page in allPages)
                    {
                        var title = page.GetTitle();
                        if (!string.IsNullOrEmpty(title) && !existingPagesByTitle.ContainsKey(title))
                        {
                            existingPagesByTitle[title] = page;
                        }
                    }
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to query existing pages for duplicate check: {ex.Message}");
                    return result;
                }
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Unition Upload");
            Undo.RegisterCompleteObjectUndo(profile, "Unition Upload");

            foreach (var asset in assets)
            {
                if (asset == null)
                {
                    continue;
                }

                var assetPath = AssetDatabase.GetAssetPath(asset);
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

                if (existingGuids.Contains(assetGuid))
                {
                    result.Skipped++;
                    continue;
                }

                ct.ThrowIfCancellationRequested();

                var properties = NotionWriteBackEngine.BuildProperties(asset, profile);

                if (existingPagesByTitle != null && existingPagesByTitle.TryGetValue(asset.name, out var existingPage))
                {
                    var action = strategy;
                    if (action == NotionDuplicateStrategy.AskEachTime)
                    {
                        var choice = EditorUtility.DisplayDialogComplex(
                            "Duplicate Found",
                            $"'{asset.name}' already exists in Notion.\nWhat would you like to do?",
                            "Update Existing",
                            "Skip",
                            "Create New");

                        switch (choice)
                        {
                            case 0:
                                action = NotionDuplicateStrategy.UpdateExisting;
                                break;
                            case 1:
                                action = NotionDuplicateStrategy.SkipDuplicates;
                                break;
                            default:
                                action = NotionDuplicateStrategy.CreateNew;
                                break;
                        }
                    }

                    if (action == NotionDuplicateStrategy.SkipDuplicates)
                    {
                        result.Skipped++;
                        continue;
                    }

                    if (action == NotionDuplicateStrategy.UpdateExisting)
                    {
                        try
                        {
                            await client.UpdatePageAsync(existingPage.Id, properties, ct);

                            var existingEntry = profile.SyncEntries.Find(e => e.NotionPageId == existingPage.Id);
                            if (existingEntry != null)
                            {
                                existingEntry.LocalAssetGuid = assetGuid;
                                existingEntry.LocalAssetPath = assetPath;
                                existingEntry.LocalLastSyncedUtc = DateTime.UtcNow.ToString("o");
                            }
                            else
                            {
                                var entry = new NotionSyncEntry
                                {
                                    NotionPageId = existingPage.Id,
                                    LocalAssetGuid = assetGuid,
                                    LocalAssetPath = assetPath,
                                    LastEditedTime = existingPage.LastEditedTime,
                                    LocalLastSyncedUtc = DateTime.UtcNow.ToString("o")
                                };
                                profile.SyncEntries.Add(entry);
                            }
                            existingGuids.Add(assetGuid);
                            result.Updated++;
                        }
                        catch (Exception ex)
                        {
                            result.Errors.Add($"Failed to update '{asset.name}': {ex.Message}");
                        }
                        continue;
                    }
                }

                try
                {
                    var page = await client.CreatePageAsync(profile.DatabaseId, properties, ct);

                    var entry = new NotionSyncEntry
                    {
                        NotionPageId = page.Id,
                        LocalAssetGuid = assetGuid,
                        LocalAssetPath = assetPath,
                        LastEditedTime = page.LastEditedTime,
                        LocalLastSyncedUtc = DateTime.UtcNow.ToString("o")
                    };
                    profile.SyncEntries.Add(entry);
                    existingGuids.Add(assetGuid);
                    result.Created++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to upload '{asset.name}': {ex.Message}");
                }
            }

            profile.MarkDirty();
            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();

            return result;
        }
    }
}
