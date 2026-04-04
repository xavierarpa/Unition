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
using System.Linq;
using System.Threading;

using Unition.Attributes;
using Unition.Editor.Sync;
using Unition.Editor.UI;
using Unition.Editor.Windows;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.Drawers
{
    public abstract class NotionLinkEditorBase : UnityEditor.Editor
    {
        private NotionLinkAttribute _linkAttribute;
        private NotionSyncProfile _matchedProfile;
        private NotionSyncEntry _matchedEntry;
        private bool _hasLink;
        private bool _isSynced;

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            _linkAttribute = target.GetType().GetCustomAttributes(typeof(NotionLinkAttribute), true)
                .FirstOrDefault() as NotionLinkAttribute;
            _hasLink = _linkAttribute != null;

            if (_hasLink)
            {
                FindMatchingProfileAndEntry();
            }
            else
            {
                FindProfileByAssetGuid();
            }
        }

        public override void OnInspectorGUI()
        {
            if (_hasLink)
            {
                DrawNotionHeader();
                EditorGUILayout.Space(4);
            }
            else if (_isSynced)
            {
                DrawSyncProfileHeader();
                EditorGUILayout.Space(4);
            }

            base.OnInspectorGUI();
        }

        private void DrawNotionHeader()
        {
            var wasEnabled = GUI.enabled;
            GUI.enabled = true;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            var statusColor = GetStatusColor();
            var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
            dotRect.y += 4;
            EditorGUI.DrawRect(new Rect(dotRect.x + 2, dotRect.y + 2, 10, 10), statusColor);

            var statusText = GetStatusText();
            var statusStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = statusColor }
            };
            GUILayout.Label(statusText, statusStyle);

            GUILayout.FlexibleSpace();

            if (_matchedEntry != null)
            {
                if (GUILayout.Button("Open in Notion ↗", EditorStyles.miniButton, GUILayout.Width(110)))
                {
                    OpenInNotion();
                }
            }

            if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(60)))
            {
                RefreshFromNotion();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);

            var dimStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            var dbLabel = _matchedProfile != null && !string.IsNullOrEmpty(_matchedProfile.DatabaseName)
                ? _matchedProfile.DatabaseName
                : $"{_linkAttribute.DatabaseId.Substring(0, Mathf.Min(8, _linkAttribute.DatabaseId.Length))}...";
            GUILayout.Label($"DB: {dbLabel}", dimStyle);

            if (_matchedEntry != null && !string.IsNullOrEmpty(_matchedEntry.LastEditedTime))
            {
                GUILayout.Label($"  |  Last synced: {TimeFormatUtility.FormatRelative(_matchedEntry.LastEditedTime)}", dimStyle);
            }

            if (_matchedProfile != null)
            {
                GUILayout.Label($"  |  Profile: {_matchedProfile.name}", dimStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUI.enabled = wasEnabled;
        }

        private string GetStatusText()
        {
            if (_matchedEntry != null)
            {
                return "Linked to Notion";
            }
            return "Notion Link (not synced)";
        }

        private Color GetStatusColor()
        {
            if (_matchedEntry != null)
            {
                return new Color(0.3f, 0.78f, 0.69f);
            }
            return new Color(0.86f, 0.86f, 0.67f);
        }

        private void OpenInNotion()
        {
            if (_matchedEntry == null || string.IsNullOrEmpty(_matchedEntry.NotionPageId))
            {
                return;
            }

            var pageId = _matchedEntry.NotionPageId.Replace("-", "");
            Application.OpenURL($"https://notion.so/{pageId}");
        }

        private async void RefreshFromNotion()
        {
            if (_matchedProfile == null)
            {
                FindMatchingProfileAndEntry();
                if (_matchedProfile == null)
                {
                    EditorUtility.DisplayDialog("Unition",
                        "No sync profile found for this database. Create one via Window > Unition > Import Wizard.",
                        "OK");
                    return;
                }
            }

            EditorUtility.DisplayProgressBar("Unition", "Syncing from Notion...", 0.5f);

            try
            {
                var result = await NotionSyncEngine.SyncAsync(_matchedProfile, CancellationToken.None);
                EditorUtility.ClearProgressBar();

                if (result.HasErrors)
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Sync completed with errors:\n{result.ToSummary()}", "OK");
                }
                else
                {
                    Debug.Log($"[Unition] Refresh complete: {result.ToSummary()}");
                }

                FindMatchingProfileAndEntry();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Unition] Refresh failed: {ex.Message}");
            }
        }

        private void FindMatchingProfileAndEntry()
        {
            _matchedProfile = null;
            _matchedEntry = null;

            if (_linkAttribute == null)
            {
                return;
            }

            var assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            if (string.IsNullOrEmpty(assetGuid))
            {
                return;
            }

            var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            foreach (var profileGuid in profileGuids)
            {
                var profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(profilePath);
                if (profile == null || profile.DatabaseId != _linkAttribute.DatabaseId)
                {
                    continue;
                }

                _matchedProfile = profile;

                foreach (var entry in profile.SyncEntries)
                {
                    if (entry.LocalAssetGuid == assetGuid)
                    {
                        _matchedEntry = entry;
                        return;
                    }
                }
            }
        }

        private void FindProfileByAssetGuid()
        {
            _matchedProfile = null;
            _matchedEntry = null;
            _isSynced = false;

            var assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(target));
            if (string.IsNullOrEmpty(assetGuid))
            {
                return;
            }

            var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            foreach (var profileGuid in profileGuids)
            {
                var profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(profilePath);
                if (profile == null)
                {
                    continue;
                }

                foreach (var entry in profile.SyncEntries)
                {
                    if (entry.LocalAssetGuid == assetGuid)
                    {
                        _matchedProfile = profile;
                        _matchedEntry = entry;
                        _isSynced = true;
                        return;
                    }

                    if (!string.IsNullOrEmpty(entry.LocalAssetPath) &&
                        entry.LocalAssetPath == AssetDatabase.GetAssetPath(target))
                    {
                        _matchedProfile = profile;
                        _matchedEntry = entry;
                        _isSynced = true;
                        return;
                    }
                }
            }
        }

        private void DrawSyncProfileHeader()
        {
            var wasEnabled = GUI.enabled;
            GUI.enabled = true;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
            dotRect.y += 4;
            EditorGUI.DrawRect(new Rect(dotRect.x + 2, dotRect.y + 2, 10, 10), new Color(0.3f, 0.78f, 0.69f));

            GUILayout.Label("Synced via Unition", EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (_matchedEntry != null && !string.IsNullOrEmpty(_matchedEntry.NotionPageId))
            {
                if (GUILayout.Button("Open ↗", EditorStyles.miniButton, GUILayout.Width(55)))
                {
                    var pageId = _matchedEntry.NotionPageId.Replace("-", "");
                    Application.OpenURL($"https://notion.so/{pageId}");
                }

                if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    NotionPageViewerWindow.ShowPage(_matchedEntry.NotionPageId);
                }

                if (GUILayout.Button("Copy ID", EditorStyles.miniButton, GUILayout.Width(55)))
                {
                    EditorGUIUtility.systemCopyBuffer = _matchedEntry.NotionPageId;
                    Debug.Log($"[Unition] Copied page ID: {_matchedEntry.NotionPageId}");
                }

                if (_matchedProfile != null)
                {
                    if (GUILayout.Button("Browse", EditorStyles.miniButton, GUILayout.Width(50)))
                    {
                        NotionBrowserWindow.ShowDatabase(_matchedProfile.DatabaseId);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);

            if (_matchedEntry != null)
            {
                if (GUILayout.Button("Pull", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    PullFromNotion();
                }

                if (target is ScriptableObject)
                {
                    if (GUILayout.Button("Push", EditorStyles.miniButton, GUILayout.Width(40)))
                    {
                        PushToNotion();
                    }
                }

                if (GUILayout.Button("Unlink", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    UnlinkFromNotion();
                }
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);

            var dimStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };

            if (_matchedProfile != null)
            {
                GUILayout.Label($"Profile: {_matchedProfile.name}", dimStyle);
            }

            if (_matchedEntry != null && !string.IsNullOrEmpty(_matchedEntry.LocalLastSyncedUtc))
            {
                GUILayout.Label($"  |  Last sync: {TimeFormatUtility.FormatRelative(_matchedEntry.LocalLastSyncedUtc)}", dimStyle);
            }
            else if (_matchedEntry != null && !string.IsNullOrEmpty(_matchedEntry.LastEditedTime))
            {
                GUILayout.Label($"  |  Edited: {TimeFormatUtility.FormatRelative(_matchedEntry.LastEditedTime)}", dimStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(18);

            EditorGUILayout.ObjectField("Profile", _matchedProfile, typeof(NotionSyncProfile), false);

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            GUI.enabled = wasEnabled;
        }

        private async void PullFromNotion()
        {
            if (_matchedProfile == null || _matchedEntry == null)
            {
                return;
            }

            EditorUtility.DisplayProgressBar("Unition", "Pulling from Notion...", 0.5f);

            try
            {
                var result = await NotionSyncEngine.SyncAsync(_matchedProfile, CancellationToken.None);
                EditorUtility.ClearProgressBar();

                if (result.HasErrors)
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Pull completed with errors:\n{result.ToSummary()}", "OK");
                }
                else
                {
                    Debug.Log($"[Unition] Pull complete: {result.ToSummary()}");
                }

                FindProfileByAssetGuid();
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Unition] Pull failed: {ex.Message}");
            }
        }

        private async void PushToNotion()
        {
            if (_matchedProfile == null || _matchedEntry == null || !(target is ScriptableObject so))
            {
                return;
            }

            EditorUtility.DisplayProgressBar("Unition", "Pushing to Notion...", 0.5f);

            try
            {
                var error = await NotionWriteBackEngine.PushAsync(so, _matchedProfile, _matchedEntry, CancellationToken.None);
                EditorUtility.ClearProgressBar();

                if (!string.IsNullOrEmpty(error))
                {
                    EditorUtility.DisplayDialog("Unition", $"Push failed:\n{error}", "OK");
                }
                else
                {
                    Debug.Log("[Unition] Push to Notion completed successfully.");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[Unition] Push failed: {ex.Message}");
            }
        }

        private void UnlinkFromNotion()
        {
            if (_matchedProfile == null || _matchedEntry == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("Unlink from Notion",
                $"Remove sync tracking for this asset?\n\nThis will NOT delete the Notion page or the local file.",
                "Unlink", "Cancel"))
            {
                return;
            }

            _matchedProfile.SyncEntries.Remove(_matchedEntry);
            EditorUtility.SetDirty(_matchedProfile);
            AssetDatabase.SaveAssets();

            _matchedEntry = null;
            _isSynced = false;

            Debug.Log("[Unition] Asset unlinked from Notion sync.");
        }


    }
}
