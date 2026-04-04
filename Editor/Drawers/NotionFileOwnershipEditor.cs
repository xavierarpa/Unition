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
using Unition.Editor.Sync;
using Unition.Editor.Windows;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.Drawers
{
    [CustomEditor(typeof(Texture2D), true)]
    [CanEditMultipleObjects]
    internal sealed class NotionFileOwnershipEditor : UnityEditor.Editor
    {
        private NotionSyncProfile _matchedProfile;
        private NotionDownloadedFile _matchedFile;
        private NotionSyncEntry _matchedEntry;
        private bool _isOwned;

        private void OnEnable()
        {
            if (target == null)
            {
                return;
            }

            FindOwnership();
        }

        public override void OnInspectorGUI()
        {
            if (_isOwned)
            {
                DrawOwnershipHeader();
                EditorGUILayout.Space(4);
            }

            base.OnInspectorGUI();
        }

        private void DrawOwnershipHeader()
        {
            var wasEnabled = GUI.enabled;
            GUI.enabled = true;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            var dotRect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14));
            dotRect.y += 4;
            EditorGUI.DrawRect(new Rect(dotRect.x + 2, dotRect.y + 2, 10, 10), new Color(0.45f, 0.7f, 0.95f));

            GUILayout.Label("Downloaded via Unition", EditorStyles.boldLabel);

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
            }
            else if (_matchedFile != null && !string.IsNullOrEmpty(_matchedFile.NotionPageId))
            {
                if (GUILayout.Button("Open ↗", EditorStyles.miniButton, GUILayout.Width(55)))
                {
                    var pageId = _matchedFile.NotionPageId.Replace("-", "");
                    Application.OpenURL($"https://notion.so/{pageId}");
                }

                if (GUILayout.Button("View", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    NotionPageViewerWindow.ShowPage(_matchedFile.NotionPageId);
                }
            }

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

            if (_matchedFile != null && !string.IsNullOrEmpty(_matchedFile.PropertyName))
            {
                GUILayout.Label($"  |  Property: {_matchedFile.PropertyName}", dimStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_matchedProfile != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(18);

                EditorGUILayout.ObjectField("Profile", _matchedProfile, typeof(NotionSyncProfile), false);

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();

            GUI.enabled = wasEnabled;
        }

        private void FindOwnership()
        {
            _matchedProfile = null;
            _matchedFile = null;
            _matchedEntry = null;
            _isOwned = false;

            var assetPath = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(assetPath))
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

                foreach (var file in profile.DownloadedFiles)
                {
                    if (file.LocalAssetPath == assetPath)
                    {
                        _matchedProfile = profile;
                        _matchedFile = file;
                        _isOwned = true;

                        foreach (var entry in profile.SyncEntries)
                        {
                            if (entry.NotionPageId == file.NotionPageId)
                            {
                                _matchedEntry = entry;
                                break;
                            }
                        }

                        return;
                    }
                }
            }
        }
    }
}
