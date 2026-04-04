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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Unition.Editor.Sync;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Unition.Editor.Windows
{
    public sealed class NotionSyncWindow : EditorWindow
    {
        private const int MaxConcurrentSyncs = 2;
        private const int MaxLogEntries = 50;

        private VisualElement _root;
        private VisualElement _profileList;
        private VisualElement _logPanel;
        private ProgressBar _progressBar;
        private Button _cancelBtn;
        private CancellationTokenSource _cts;

        [MenuItem("Window/Unition/Notion Sync Manager", false, 1001)]
        public static void ShowWindow()
        {
            var window = GetWindow<NotionSyncWindow>();
            window.titleContent = new GUIContent("Notion Sync Manager", EditorGUIUtility.IconContent("d_Refresh").image);
            window.minSize = new Vector2(500, 350);
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private void OnFocus()
        {
            AutoSyncProfiles(NotionSyncMode.OnEditorFocus);
        }

        private void CreateGUI()
        {
            _root = rootVisualElement;
            _root.AddToClassList("unition-root");

            LoadStyleSheet();
            BuildHeader();
            BuildBody();

            RefreshProfileList();
        }

        private void LoadStyleSheet()
        {
            var guids = AssetDatabase.FindAssets("Unition t:StyleSheet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Unition.uss"))
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet != null)
                    {
                        _root.styleSheets.Add(sheet);
                    }
                    break;
                }
            }
        }

        private void BuildHeader()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("unition-toolbar");
            _root.Add(toolbar);

            var title = new Label("Unition — Notion Sync Manager");
            title.AddToClassList("unition-toolbar__title");
            toolbar.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("unition-toolbar__spacer");
            toolbar.Add(spacer);

            var syncAllBtn = new Button(() => SyncAllProfiles()) { text = "Sync All", tooltip = "Pull data from Notion for all sync profiles." };
            syncAllBtn.AddToClassList("unition-btn-primary");
            toolbar.Add(syncAllBtn);

            var refreshBtn = new Button(RefreshProfileList) { text = "↻", tooltip = "Refresh the profile list from the AssetDatabase." };
            refreshBtn.style.width = 28;
            refreshBtn.style.height = 22;
            toolbar.Add(refreshBtn);
        }

        private void BuildBody()
        {
            var body = new VisualElement();
            body.style.flexDirection = FlexDirection.Column;
            body.style.flexGrow = 1;
            _root.Add(body);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            body.Add(scroll);

            _profileList = new VisualElement();
            _profileList.style.paddingLeft = 8;
            _profileList.style.paddingRight = 8;
            _profileList.style.paddingTop = 4;
            scroll.Add(_profileList);

            var progressRow = new VisualElement();
            progressRow.style.flexDirection = FlexDirection.Row;
            progressRow.style.alignItems = Align.Center;
            progressRow.style.paddingLeft = 8;
            progressRow.style.paddingRight = 8;
            progressRow.style.paddingTop = 4;
            progressRow.style.paddingBottom = 4;
            progressRow.style.display = DisplayStyle.None;
            body.Add(progressRow);

            _progressBar = new ProgressBar();
            _progressBar.style.flexGrow = 1;
            _progressBar.style.height = 18;
            progressRow.Add(_progressBar);

            _cancelBtn = new Button(() => _cts?.Cancel()) { text = "Cancel" };
            _cancelBtn.style.marginLeft = 6;
            _cancelBtn.style.width = 60;
            progressRow.Add(_cancelBtn);

            _logPanel = new VisualElement();
            _logPanel.AddToClassList("unition-sync-log");
            _logPanel.style.minHeight = 100;
            _logPanel.style.maxHeight = 200;
            _logPanel.style.paddingLeft = 8;
            _logPanel.style.paddingRight = 8;
            _logPanel.style.paddingTop = 4;
            _logPanel.style.paddingBottom = 4;
            _logPanel.style.borderTopWidth = 1;
            _logPanel.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            body.Add(_logPanel);

            var logTitle = new Label("Log");
            logTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            logTitle.style.marginBottom = 4;
            _logPanel.Add(logTitle);
        }

        private void RefreshProfileList()
        {
            _profileList.Clear();

            var profiles = FindAllSyncProfiles();
            if (profiles.Count == 0)
            {
                var empty = new Label("No sync profiles found. Create one via Assets > Create > Unition > Sync Profile or use the Import Wizard.");
                empty.AddToClassList("unition-empty");
                empty.style.whiteSpace = WhiteSpace.Normal;
                _profileList.Add(empty);

                var createBtn = new Button(() => NotionImportWizard.ShowWizard()) { text = "Open Notion Import Wizard" };
                createBtn.AddToClassList("unition-btn-primary");
                createBtn.style.marginTop = 8;
                createBtn.style.alignSelf = Align.Center;
                _profileList.Add(createBtn);
                return;
            }

            foreach (var profile in profiles)
            {
                _profileList.Add(BuildProfileCard(profile));
            }
        }

        private VisualElement BuildProfileCard(NotionSyncProfile profile)
        {
            var card = new VisualElement();
            card.AddToClassList("unition-settings-card");
            card.style.marginBottom = 6;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;
            card.Add(header);

            var statusIcon = new Label(GetStatusIcon(profile));
            statusIcon.style.fontSize = 14;
            statusIcon.style.marginRight = 6;
            header.Add(statusIcon);

            var nameLabel = new Label(profile.name);
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.flexGrow = 1;
            header.Add(nameLabel);

            var syncBtn = new Button(() => SyncProfile(profile)) { text = "Sync" };
            syncBtn.style.width = 50;
            header.Add(syncBtn);

            var validateBtn = new Button(() => ValidateProfile(profile)) { text = "Validate", tooltip = "Check that profile mappings match the Notion database schema." };
            validateBtn.style.width = 60;
            header.Add(validateBtn);

            var selectBtn = new Button(() => Selection.activeObject = profile) { text = "Select" };
            selectBtn.style.width = 50;
            header.Add(selectBtn);

            var details = new VisualElement();
            details.style.flexDirection = FlexDirection.Row;
            details.style.justifyContent = Justify.SpaceBetween;
            card.Add(details);

            var dbLabel = new Label($"DB: {(string.IsNullOrEmpty(profile.DatabaseName) ? profile.DatabaseId?.Substring(0, Mathf.Min(8, profile.DatabaseId?.Length ?? 0)) : profile.DatabaseName)}");
            dbLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            dbLabel.style.fontSize = 11;
            details.Add(dbLabel);

            var typeLabel = new Label($"→ {profile.TargetTypeName ?? "not set"}");
            typeLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            typeLabel.style.fontSize = 11;
            details.Add(typeLabel);

            var formatLabel = new Label($"{profile.OutputFormat} | {profile.SyncMode}");
            formatLabel.style.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            formatLabel.style.fontSize = 11;
            details.Add(formatLabel);

            if (!string.IsNullOrEmpty(profile.LastSyncedTime))
            {
                var syncedLabel = new Label($"Last sync: {FormatSyncTime(profile.LastSyncedTime)}");
                syncedLabel.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                syncedLabel.style.fontSize = 10;
                syncedLabel.style.marginTop = 2;
                card.Add(syncedLabel);
            }

            var mappingsLabel = new Label($"{profile.PropertyMappings.Count} mappings, {profile.SyncEntries.Count} synced assets");
            mappingsLabel.style.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            mappingsLabel.style.fontSize = 10;
            mappingsLabel.style.marginTop = 2;
            card.Add(mappingsLabel);

            return card;
        }

        private async void SyncProfile(NotionSyncProfile profile)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            ShowProgress($"Validating '{profile.name}'...", 0);
            AppendLog($"Syncing '{profile.name}'...");

            try
            {
                var validation = await NotionSchemaValidator.ValidateAsync(profile);
                if (!validation.IsValid)
                {
                    foreach (var warning in validation.Warnings)
                    {
                        AppendLog($"[{profile.name}] ⚠ {warning}");
                    }
                    foreach (var error in validation.Errors)
                    {
                        AppendLog($"[{profile.name}] ✗ {error}");
                    }

                    if (validation.Errors.Count > 0)
                    {
                        AppendLog($"[{profile.name}] Sync aborted due to schema errors.");
                        HideProgress();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{profile.name}] Validation skipped: {ex.Message}");
            }

            ShowProgress($"Syncing '{profile.name}'...", 0);

            var progress = new Progress<(int current, int total)>(p =>
            {
                float pct = p.total > 0 ? (float)p.current / p.total * 100f : 0f;
                ShowProgress($"{profile.name}: {p.current}/{p.total}", pct);
            });

            try
            {
                var result = await NotionSyncEngine.SyncAsync(profile, _cts.Token, progress);
                AppendLog($"[{profile.name}] {result.ToSummary()}");

                if (result.HasErrors)
                {
                    foreach (var error in result.Errors)
                    {
                        Debug.LogError($"[Unition] {profile.name}: {error}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog($"[{profile.name}] Sync cancelled.");
            }
            catch (Exception ex)
            {
                AppendLog($"[{profile.name}] Error: {ex.Message}");
                Debug.LogException(ex);
            }

            HideProgress();
            RefreshProfileList();
        }

        private async void ValidateProfile(NotionSyncProfile profile)
        {
            AppendLog($"Validating '{profile.name}'...");

            try
            {
                var result = await NotionSchemaValidator.ValidateAsync(profile);

                if (result.IsValid)
                {
                    AppendLog($"[{profile.name}] Schema valid — all mappings match.");
                }
                else
                {
                    foreach (var warning in result.Warnings)
                    {
                        AppendLog($"[{profile.name}] ⚠ {warning}");
                    }
                    foreach (var error in result.Errors)
                    {
                        AppendLog($"[{profile.name}] ✗ {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"[{profile.name}] Validation error: {ex.Message}");
            }
        }

        private async void SyncAllProfiles()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            var profiles = FindAllSyncProfiles();
            AppendLog($"Syncing all ({profiles.Count} profiles)...");

            int completedProfiles = 0;
            var semaphore = new SemaphoreSlim(MaxConcurrentSyncs, MaxConcurrentSyncs);

            var tasks = profiles.Select(async profile =>
            {
                await semaphore.WaitAsync(_cts.Token);
                try
                {
                    var idx = Interlocked.Increment(ref completedProfiles);
                    var progress = new Progress<(int current, int total)>(p =>
                    {
                        float profilePct = p.total > 0 ? (float)p.current / p.total : 0f;
                        float overallPct = ((float)(idx - 1) + profilePct) / profiles.Count * 100f;
                        ShowProgress($"{profile.name}: {p.current}/{p.total} ({idx}/{profiles.Count})", overallPct);
                    });

                    ShowProgress($"Starting '{profile.name}'...", (float)(idx - 1) / profiles.Count * 100f);

                    var result = await NotionSyncEngine.SyncAsync(profile, _cts.Token, progress);
                    AppendLog($"[{profile.name}] {result.ToSummary()}");
                }
                catch (OperationCanceledException)
                {
                    AppendLog($"[{profile.name}] Sync cancelled.");
                }
                catch (Exception ex)
                {
                    AppendLog($"[{profile.name}] Error: {ex.Message}");
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                AppendLog("Sync All cancelled.");
            }

            semaphore.Dispose();
            HideProgress();
            RefreshProfileList();
        }

        private void AutoSyncProfiles(NotionSyncMode mode)
        {
            if (!UnitionCredentials.HasToken)
            {
                return;
            }

            var profiles = FindAllSyncProfiles();
            foreach (var profile in profiles)
            {
                if (profile.SyncMode == mode)
                {
                    SyncProfile(profile);
                }
            }
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                AutoSyncProfiles(NotionSyncMode.OnPlay);
            }
        }

        private void ShowProgress(string title, float percent)
        {
            var row = _progressBar?.parent;
            if (row == null)
            {
                return;
            }

            row.style.display = DisplayStyle.Flex;
            _progressBar.title = title;
            _progressBar.value = percent;
        }

        private void HideProgress()
        {
            var row = _progressBar?.parent;
            if (row == null)
            {
                return;
            }

            row.style.display = DisplayStyle.None;
            _progressBar.value = 0;
            _progressBar.title = string.Empty;
        }

        private void AppendLog(string message)
        {
            if (_logPanel == null)
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var label = new Label($"[{timestamp}] {message}");
            label.style.fontSize = 11;
            label.style.whiteSpace = WhiteSpace.Normal;

            if (message.Contains("Error") || message.Contains("✗") || message.Contains("aborted"))
            {
                label.style.color = new Color(1f, 0.4f, 0.4f, 1f);
            }
            else if (message.Contains("⚠") || message.Contains("Warning") || message.Contains("cancelled"))
            {
                label.style.color = new Color(1f, 0.8f, 0.3f, 1f);
            }
            else if (message.Contains("valid") || message.Contains("Created") || message.Contains("Updated"))
            {
                label.style.color = new Color(0.4f, 0.9f, 0.4f, 1f);
            }

            _logPanel.Add(label);

            if (_logPanel.childCount > MaxLogEntries + 1)
            {
                _logPanel.RemoveAt(1);
            }
        }

        private static string GetStatusIcon(NotionSyncProfile profile)
        {
            if (string.IsNullOrEmpty(profile.LastSyncedTime))
            {
                return "○";
            }

            if (!string.IsNullOrEmpty(profile.LastSyncLog) && profile.LastSyncLog.Contains("Error"))
            {
                return "✖";
            }

            return "✔";
        }

        private static string FormatSyncTime(string isoTime)
        {
            if (DateTime.TryParse(isoTime, out var dt))
            {
                var diff = DateTime.UtcNow - dt;
                if (diff.TotalMinutes < 1)
                {
                    return "just now";
                }
                if (diff.TotalHours < 1)
                {
                    return $"{(int)diff.TotalMinutes}m ago";
                }
                if (diff.TotalDays < 1)
                {
                    return $"{(int)diff.TotalHours}h ago";
                }
                return dt.ToLocalTime().ToString("g");
            }
            return isoTime;
        }

        private static List<NotionSyncProfile> FindAllSyncProfiles()
        {
            var result = new List<NotionSyncProfile>();
            var guids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(path);
                if (profile != null)
                {
                    result.Add(profile);
                }
            }
            return result;
        }
    }
}
