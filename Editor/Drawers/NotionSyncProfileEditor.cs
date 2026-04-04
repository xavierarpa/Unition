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
using System.Reflection;
using System.Threading;

using Unition.Editor.Sync;
using Unition.Editor.UI;
using Unition.Editor.Windows;

using UnityEditor;
using UnityEditorInternal;

using UnityEngine;

namespace Unition.Editor.Drawers
{
    [CustomEditor(typeof(NotionSyncProfile))]
    public sealed class NotionSyncProfileEditor : UnityEditor.Editor
    {
        private SerializedProperty _databaseId;
        private SerializedProperty _databaseName;
        private SerializedProperty _targetTypeName;
        private SerializedProperty _targetAssemblyName;
        private SerializedProperty _outputPath;
        private SerializedProperty _outputFormat;
        private SerializedProperty _syncMode;
        private SerializedProperty _namingProperty;
        private SerializedProperty _conflictResolution;
        private SerializedProperty _duplicateStrategy;
        private SerializedProperty _filesDownloadPath;
        private SerializedProperty _propertyMappings;
        private SerializedProperty _syncEntries;
        private SerializedProperty _lastSyncedTime;
        private SerializedProperty _lastSyncLog;

        private ReorderableList _mappingsList;
        private bool _showMappings = true;
        private bool _showEntries;
        private bool _showLog;

        private UnityEngine.Object _outputFolder;
        private UnityEngine.Object _filesFolder;

        private void OnEnable()
        {
            _databaseId = serializedObject.FindProperty("databaseId");
            _databaseName = serializedObject.FindProperty("databaseName");
            _targetTypeName = serializedObject.FindProperty("targetTypeName");
            _targetAssemblyName = serializedObject.FindProperty("targetAssemblyName");
            _outputPath = serializedObject.FindProperty("outputPath");
            _outputFormat = serializedObject.FindProperty("outputFormat");
            _syncMode = serializedObject.FindProperty("syncMode");
            _namingProperty = serializedObject.FindProperty("namingProperty");
            _conflictResolution = serializedObject.FindProperty("conflictResolution");
            _duplicateStrategy = serializedObject.FindProperty("duplicateStrategy");
            _filesDownloadPath = serializedObject.FindProperty("filesDownloadPath");
            _propertyMappings = serializedObject.FindProperty("propertyMappings");
            _syncEntries = serializedObject.FindProperty("syncEntries");
            _lastSyncedTime = serializedObject.FindProperty("lastSyncedTime");
            _lastSyncLog = serializedObject.FindProperty("lastSyncLog");

            _outputFolder = LoadFolderAsset(_outputPath.stringValue);
            _filesFolder = LoadFolderAsset(_filesDownloadPath.stringValue);

            BuildMappingsList();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawStatusHeader();
            EditorGUILayout.Space(6);
            DrawDatabaseSection();
            EditorGUILayout.Space(4);
            DrawTargetSection();
            EditorGUILayout.Space(4);
            DrawOutputSection();
            EditorGUILayout.Space(4);
            DrawMappingsSection();
            EditorGUILayout.Space(4);
            DrawSyncEntriesSection();
            EditorGUILayout.Space(4);
            DrawUploadSection();
            EditorGUILayout.Space(4);
            DrawLogSection();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawStatusHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var profile = (NotionSyncProfile)target;
            var statusText = GetStatusText(profile);
            var statusColor = GetStatusColor(profile);

            var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
            dotRect.y += 4;
            EditorGUI.DrawRect(new Rect(dotRect.x + 2, dotRect.y + 2, 10, 10), statusColor);

            var statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = statusColor }
            };
            GUILayout.Label(statusText, statusStyle);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Open", GUILayout.Width(50)))
            {
                Application.OpenURL($"https://www.notion.so/{profile.DatabaseId.Replace("-", "")}");
            }

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                NotionBrowserWindow.ShowDatabase(profile.DatabaseId);
            }

            if (GUILayout.Button("Sync Now", GUILayout.Width(80)))
            {
                SyncNow(profile);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Force Full Sync", EditorStyles.miniButton, GUILayout.Width(100)))
            {
                ForceFullSync(profile);
            }

            if (GUILayout.Button("Copy DB ID", EditorStyles.miniButton, GUILayout.Width(75)))
            {
                EditorGUIUtility.systemCopyBuffer = profile.DatabaseId;
                Debug.Log($"[Unition] Copied database ID: {profile.DatabaseId}");
            }

            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_lastSyncedTime.stringValue))
            {
                var timeStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
                };
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Last sync: {TimeFormatUtility.FormatRelative(_lastSyncedTime.stringValue)}", timeStyle);
                GUILayout.Label($"  |  {_syncEntries.arraySize} assets  |  {CountEnabledMappings()} mappings", timeStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawDatabaseSection()
        {
            EditorGUILayout.LabelField("Database", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            var wasEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_databaseName, new GUIContent("Name", "Name of the linked Notion database."));
            EditorGUILayout.PropertyField(_databaseId, new GUIContent("ID", "Unique identifier of the Notion database. Set automatically during setup."));
            GUI.enabled = wasEnabled;
            EditorGUI.indentLevel--;
        }

        private void DrawTargetSection()
        {
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            var wasEnabled = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_targetTypeName, new GUIContent("Type", "The ScriptableObject type that maps to this database."));
            EditorGUILayout.PropertyField(_targetAssemblyName, new GUIContent("Assembly", "Assembly containing the target type."));
            GUI.enabled = wasEnabled;
            EditorGUI.indentLevel--;
        }

        private void DrawOutputSection()
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            var wasEnabledFormat = GUI.enabled;
            GUI.enabled = false;
            EditorGUILayout.PropertyField(_outputFormat, new GUIContent("Format", "Asset format created during sync: ScriptableObject or JSON."));
            EditorGUILayout.PropertyField(_namingProperty, new GUIContent("Naming Property", "Notion property used as the file name for synced assets."));
            GUI.enabled = wasEnabledFormat;

            EditorGUILayout.PropertyField(_syncMode, new GUIContent("Sync Mode", "Manual: sync only when you click 'Sync Now'.\nAutomatic: sync periodically in the background."));
            EditorGUILayout.PropertyField(_conflictResolution, new GUIContent("Conflict Resolution", "How to resolve conflicts when both Notion and the local asset have changed since last sync."));
            EditorGUILayout.PropertyField(_duplicateStrategy, new GUIContent("Duplicate Strategy", "How to handle uploads when a page with the same name already exists in Notion.\n\nCreate New: always create a new page.\nSkip Duplicates: skip assets whose name already exists.\nUpdate Existing: overwrite the existing page.\nAsk Each Time: prompt for each duplicate."));

            var wasEnabled = GUI.enabled;
            GUI.enabled = false;
            _outputFolder = EditorGUILayout.ObjectField(
                new GUIContent("Output Path", "Folder where synced assets are created or updated."),
                _outputFolder, typeof(DefaultAsset), false);
            GUI.enabled = wasEnabled;

            EditorGUILayout.BeginHorizontal();
            var newFilesFolder = EditorGUILayout.ObjectField(
                new GUIContent("Files Path", "Folder where attached files (images, PDFs, etc.) from Notion 'files' properties are downloaded."),
                _filesFolder, typeof(DefaultAsset), false);
            if (newFilesFolder != _filesFolder)
            {
                var path = AssetDatabase.GetAssetPath(newFilesFolder);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    _filesFolder = newFilesFolder;
                    _filesDownloadPath.stringValue = path + "/";
                }
                else if (newFilesFolder == null)
                {
                    _filesFolder = null;
                    _filesDownloadPath.stringValue = "Assets/Notion/Downloads/";
                }
            }
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                var selected = EditorUtility.OpenFolderPanel("Select Files Download Folder", "Assets", "");
                if (!string.IsNullOrEmpty(selected))
                {
                    var dataPath = Application.dataPath;
                    if (selected.StartsWith(dataPath))
                    {
                        var relative = "Assets" + selected.Substring(dataPath.Length) + "/";
                        _filesDownloadPath.stringValue = relative;
                        _filesFolder = LoadFolderAsset(relative);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel--;
        }

        private void BuildMappingsList()
        {
            _mappingsList = new ReorderableList(serializedObject, _propertyMappings, false, true, false, false);

            _mappingsList.drawHeaderCallback = rect =>
            {
                var w = rect.width - 20;
                var colW = w / 5f;
                rect.x += 16;
                EditorGUI.LabelField(new Rect(rect.x, rect.y, 16, rect.height), "✓");
                EditorGUI.LabelField(new Rect(rect.x + 18, rect.y, colW, rect.height), "Notion Property");
                EditorGUI.LabelField(new Rect(rect.x + 18 + colW, rect.y, colW * 0.6f, rect.height), "Type");
                EditorGUI.LabelField(new Rect(rect.x + 18 + colW * 1.6f, rect.y, 16, rect.height), "→");
                EditorGUI.LabelField(new Rect(rect.x + 18 + colW * 1.6f + 18, rect.y, colW, rect.height), "Target Field");
                EditorGUI.LabelField(new Rect(rect.x + 18 + colW * 2.6f + 18, rect.y, colW * 0.6f, rect.height), "Type");
            };

            _mappingsList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var mapping = _propertyMappings.GetArrayElementAtIndex(index);
                var enabled = mapping.FindPropertyRelative("enabled");
                var notionName = mapping.FindPropertyRelative("notionPropertyName");
                var notionType = mapping.FindPropertyRelative("notionPropertyType");
                var targetField = mapping.FindPropertyRelative("targetFieldName");
                var targetType = mapping.FindPropertyRelative("targetFieldType");

                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                var w = rect.width - 20;
                var colW = w / 5f;

                var wasEnabled = GUI.enabled;
                enabled.boolValue = EditorGUI.Toggle(new Rect(rect.x, rect.y, 16, rect.height), enabled.boolValue);

                GUI.enabled = enabled.boolValue;

                EditorGUI.LabelField(new Rect(rect.x + 18, rect.y, colW, rect.height), notionName.stringValue);

                var prev = GUI.color;
                GUI.color = new Color(0.5f, 0.65f, 0.8f);
                EditorGUI.LabelField(new Rect(rect.x + 18 + colW, rect.y, colW * 0.6f, rect.height), notionType.stringValue, EditorStyles.miniLabel);
                GUI.color = prev;

                EditorGUI.LabelField(new Rect(rect.x + 18 + colW * 1.6f, rect.y, 16, rect.height), "→");

                EditorGUI.LabelField(
                    new Rect(rect.x + 18 + colW * 1.6f + 18, rect.y, colW, rect.height),
                    targetField.stringValue);

                prev = GUI.color;
                GUI.color = new Color(0.5f, 0.65f, 0.8f);
                EditorGUI.LabelField(
                    new Rect(rect.x + 18 + colW * 2.6f + 18, rect.y, colW * 0.6f, rect.height),
                    targetType.stringValue, EditorStyles.miniLabel);
                GUI.color = prev;

                GUI.enabled = wasEnabled;
            };

            _mappingsList.elementHeightCallback = index => EditorGUIUtility.singleLineHeight + 4;
        }

        private void DrawMappingsSection()
        {
            _showMappings = EditorGUILayout.Foldout(_showMappings,
                $"Property Mappings ({_propertyMappings.arraySize})", true, EditorStyles.foldoutHeader);

            if (!_showMappings)
            {
                return;
            }

            if (_propertyMappings.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No mappings configured. Use the Import Wizard to set them up.", MessageType.Info);
            }

            _mappingsList.DoLayoutList();

            if (_propertyMappings.arraySize > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Validate Mappings", EditorStyles.miniButton, GUILayout.Width(110)))
                {
                    ValidateMappings();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSyncEntriesSection()
        {
            _showEntries = EditorGUILayout.Foldout(_showEntries,
                $"Synced Assets ({_syncEntries.arraySize})", true, EditorStyles.foldoutHeader);

            if (!_showEntries)
            {
                return;
            }

            if (_syncEntries.arraySize > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Remove Missing", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    RemoveMissingEntries();
                }

                if (GUILayout.Button("Clear All", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    ClearAllEntries();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel++;

            for (int i = 0; i < _syncEntries.arraySize; i++)
            {
                var entry = _syncEntries.GetArrayElementAtIndex(i);
                var pageId = entry.FindPropertyRelative("notionPageId");
                var guid = entry.FindPropertyRelative("localAssetGuid");
                var assetPath = entry.FindPropertyRelative("localAssetPath");

                EditorGUILayout.BeginHorizontal();

                var shortId = pageId.stringValue;
                if (shortId.Length > 8)
                {
                    shortId = shortId.Substring(0, 8) + "...";
                }
                EditorGUILayout.LabelField(shortId, GUILayout.Width(90));

                var path = AssetDatabase.GUIDToAssetPath(guid.stringValue);
                if (string.IsNullOrEmpty(path))
                {
                    path = assetPath.stringValue;
                }

                UnityEngine.Object asset = null;
                if (!string.IsNullOrEmpty(path))
                {
                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }

                if (asset != null)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(asset, typeof(ScriptableObject), false);
                    EditorGUI.EndDisabledGroup();
                }
                else
                {
                    var prev = GUI.color;
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    EditorGUILayout.LabelField(string.IsNullOrEmpty(path) ? "(missing)" : $"✗ {path}");
                    GUI.color = prev;
                }
                if (GUILayout.Button("View", GUILayout.Width(40)))
                {
                    NotionPageViewerWindow.ShowPage(pageId.stringValue);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawUploadSection()
        {
            var profile = (NotionSyncProfile)target;
            var targetType = profile.ResolveTargetType();
            if (targetType == null || !typeof(ScriptableObject).IsAssignableFrom(targetType))
            {
                return;
            }

            EditorGUILayout.LabelField("Upload Existing Assets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Upload existing {targetType.Name} assets (not yet tracked) as new Notion pages.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Upload from Folder..."))
            {
                UploadFromFolder(profile, targetType);
            }
            if (GUILayout.Button("Upload Selected in Project"))
            {
                UploadSelected(profile, targetType);
            }
            EditorGUILayout.EndHorizontal();
        }

        private async void UploadFromFolder(NotionSyncProfile profile, Type targetType)
        {
            var folder = EditorUtility.OpenFolderPanel("Select folder with assets to upload", "Assets", "");
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            folder = folder.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            string searchFolder;
            if (folder.StartsWith(dataPath))
            {
                searchFolder = "Assets" + folder.Substring(dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("Unition", "Selected folder must be inside the Assets folder.", "OK");
                return;
            }

            var untracked = FindUntrackedAssets(profile, targetType, searchFolder);
            await ConfirmAndUpload(profile, untracked);
        }

        private async void UploadSelected(NotionSyncProfile profile, Type targetType)
        {
            var selected = Selection.objects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog("Unition", "Select one or more assets in the Project window first.", "OK");
                return;
            }

            var trackedGuids = new HashSet<string>();
            foreach (var entry in profile.SyncEntries)
            {
                if (!string.IsNullOrEmpty(entry.LocalAssetGuid))
                {
                    trackedGuids.Add(entry.LocalAssetGuid);
                }
            }

            var untracked = new List<ScriptableObject>();
            foreach (var obj in selected)
            {
                if (obj == null || !targetType.IsInstanceOfType(obj))
                {
                    continue;
                }
                var path = AssetDatabase.GetAssetPath(obj);
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (!trackedGuids.Contains(guid))
                {
                    untracked.Add(obj as ScriptableObject);
                }
            }

            await ConfirmAndUpload(profile, untracked);
        }

        private List<ScriptableObject> FindUntrackedAssets(NotionSyncProfile profile, Type targetType, string searchFolder = null)
        {
            var trackedGuids = new HashSet<string>();
            foreach (var entry in profile.SyncEntries)
            {
                if (!string.IsNullOrEmpty(entry.LocalAssetGuid))
                {
                    trackedGuids.Add(entry.LocalAssetGuid);
                }
            }

            var searchIn = string.IsNullOrEmpty(searchFolder) ? new string[0] : new[] { searchFolder };
            var guids = AssetDatabase.FindAssets($"t:{targetType.Name}", searchIn);
            var untracked = new List<ScriptableObject>();
            foreach (var guid in guids)
            {
                if (trackedGuids.Contains(guid))
                {
                    continue;
                }
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, targetType) as ScriptableObject;
                if (asset != null)
                {
                    untracked.Add(asset);
                }
            }
            return untracked;
        }

        private async System.Threading.Tasks.Task ConfirmAndUpload(NotionSyncProfile profile, List<ScriptableObject> untracked)
        {
            if (untracked.Count == 0)
            {
                EditorUtility.DisplayDialog("Unition",
                    "No untracked assets found. All existing assets are already synced.", "OK");
                return;
            }

            var names = new List<string>();
            foreach (var asset in untracked)
            {
                names.Add(asset.name);
            }

            if (!EditorUtility.DisplayDialog("Upload to Notion",
                $"Upload {untracked.Count} asset(s) to '{profile.DatabaseName}'?\n\n" +
                string.Join("\n", names),
                "Upload", "Cancel"))
            {
                return;
            }

            EditorUtility.DisplayProgressBar("Unition", "Uploading assets to Notion...", 0.5f);

            try
            {
                var result = await NotionSyncEngine.UploadAssetsAsync(profile, untracked, CancellationToken.None);
                EditorUtility.ClearProgressBar();

                if (result.HasErrors)
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Upload completed with errors:\n{result.ToSummary()}", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Upload completed!\n{result.ToSummary()}", "OK");
                }

                serializedObject.Update();
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Unition", $"Upload failed: {ex.Message}", "OK");
            }
        }

        private void DrawLogSection()
        {
            if (string.IsNullOrEmpty(_lastSyncLog.stringValue))
            {
                return;
            }

            _showLog = EditorGUILayout.Foldout(_showLog, "Last Sync Log", true, EditorStyles.foldoutHeader);

            if (!_showLog)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox(_lastSyncLog.stringValue, MessageType.None);
            EditorGUI.indentLevel--;
        }

        private async void SyncNow(NotionSyncProfile profile)
        {
            EditorUtility.DisplayProgressBar("Unition", $"Syncing '{profile.name}'...", 0.5f);

            try
            {
                var result = await NotionSyncEngine.SyncAsync(profile, CancellationToken.None);
                EditorUtility.ClearProgressBar();

                if (result.HasErrors)
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Sync completed with errors:\n{result.ToSummary()}", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Sync completed successfully!\n{result.ToSummary()}", "OK");
                }

                serializedObject.Update();
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Unition", $"Sync failed: {ex.Message}", "OK");
            }
        }

        private async void ForceFullSync(NotionSyncProfile profile)
        {
            if (!EditorUtility.DisplayDialog("Force Full Sync",
                "This will ignore delta timestamps and re-sync ALL pages from Notion.\n\nExisting assets will be updated.",
                "Force Sync", "Cancel"))
            {
                return;
            }

            var savedTime = profile.LastSyncedTime;
            profile.LastSyncedTime = null;
            EditorUtility.SetDirty(profile);

            EditorUtility.DisplayProgressBar("Unition", $"Full sync '{profile.name}'...", 0.5f);

            try
            {
                var result = await NotionSyncEngine.SyncAsync(profile, CancellationToken.None);
                EditorUtility.ClearProgressBar();

                if (result.HasErrors)
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Full sync completed with errors:\n{result.ToSummary()}", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Full sync completed!\n{result.ToSummary()}", "OK");
                }

                serializedObject.Update();
                Repaint();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                profile.LastSyncedTime = savedTime;
                EditorUtility.SetDirty(profile);
                EditorUtility.DisplayDialog("Unition", $"Full sync failed: {ex.Message}", "OK");
            }
        }

        private void ValidateMappings()
        {
            var profile = (NotionSyncProfile)target;
            var targetType = profile.ResolveTargetType();

            if (targetType == null)
            {
                if (profile.OutputFormat == NotionOutputFormat.JSON)
                {
                    EditorUtility.DisplayDialog("Unition",
                        "JSON format profiles don't have a target type to validate against.", "OK");
                }
                else
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Cannot resolve target type '{profile.TargetTypeName}'. The type may have been renamed or removed.", "OK");
                }
                return;
            }

            var fields = targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var fieldNames = new HashSet<string>();
            foreach (var f in fields)
            {
                fieldNames.Add(f.Name);
            }

            var issues = new List<string>();
            var validCount = 0;

            for (int i = 0; i < profile.PropertyMappings.Count; i++)
            {
                var mapping = profile.PropertyMappings[i];
                if (!mapping.Enabled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(mapping.TargetFieldName))
                {
                    issues.Add($"'{mapping.NotionPropertyName}' → (no target field)");
                }
                else if (!fieldNames.Contains(mapping.TargetFieldName))
                {
                    issues.Add($"'{mapping.NotionPropertyName}' → '{mapping.TargetFieldName}' (field not found on {targetType.Name})");
                }
                else
                {
                    validCount++;
                }
            }

            if (issues.Count == 0)
            {
                EditorUtility.DisplayDialog("Unition",
                    $"All {validCount} enabled mapping(s) are valid.", "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Unition",
                    $"{validCount} valid, {issues.Count} issue(s) found:\n\n" + string.Join("\n", issues), "OK");
            }
        }

        private int CountEnabledMappings()
        {
            int count = 0;
            for (int i = 0; i < _propertyMappings.arraySize; i++)
            {
                var mapping = _propertyMappings.GetArrayElementAtIndex(i);
                var enabled = mapping.FindPropertyRelative("enabled");
                if (enabled != null && enabled.boolValue)
                {
                    count++;
                }
            }
            return count;
        }

        private void RemoveMissingEntries()
        {
            var profile = (NotionSyncProfile)target;
            int removed = 0;

            for (int i = profile.SyncEntries.Count - 1; i >= 0; i--)
            {
                var entry = profile.SyncEntries[i];
                var path = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                if (string.IsNullOrEmpty(path))
                {
                    path = entry.LocalAssetPath;
                }

                var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                if (asset == null)
                {
                    profile.SyncEntries.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
            {
                EditorUtility.SetDirty(profile);
                AssetDatabase.SaveAssets();
                serializedObject.Update();
                Repaint();
                Debug.Log($"[Unition] Removed {removed} missing entry(ies).");
            }
            else
            {
                EditorUtility.DisplayDialog("Unition", "No missing entries found.", "OK");
            }
        }

        private void ClearAllEntries()
        {
            var profile = (NotionSyncProfile)target;

            if (!EditorUtility.DisplayDialog("Clear All Entries",
                $"Remove all {profile.SyncEntries.Count} sync entries?\n\nThis will NOT delete any Notion pages or local files, but all tracking will be lost.\nThe next sync will re-create entries.",
                "Clear", "Cancel"))
            {
                return;
            }

            profile.SyncEntries.Clear();
            profile.LastSyncedTime = null;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            serializedObject.Update();
            Repaint();

            Debug.Log("[Unition] All sync entries cleared.");
        }

        private static string GetStatusText(NotionSyncProfile profile)
        {
            if (string.IsNullOrEmpty(profile.LastSyncedTime))
            {
                return "Not synced yet";
            }
            if (!string.IsNullOrEmpty(profile.LastSyncLog) && profile.LastSyncLog.Contains("Error"))
            {
                return "Sync errors";
            }
            return "Synced";
        }

        private static Color GetStatusColor(NotionSyncProfile profile)
        {
            if (string.IsNullOrEmpty(profile.LastSyncedTime))
            {
                return new Color(0.86f, 0.86f, 0.67f);
            }
            if (!string.IsNullOrEmpty(profile.LastSyncLog) && profile.LastSyncLog.Contains("Error"))
            {
                return new Color(0.96f, 0.28f, 0.28f);
            }
            return new Color(0.3f, 0.78f, 0.69f);
        }

        private static UnityEngine.Object LoadFolderAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var trimmed = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(trimmed))
            {
                return AssetDatabase.LoadAssetAtPath<DefaultAsset>(trimmed);
            }

            return null;
        }


    }
}
