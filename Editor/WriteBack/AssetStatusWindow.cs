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

using Newtonsoft.Json.Linq;

using Unition.Editor.Sync;
using Unition.Models;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.WriteBack
{
    internal sealed class AssetStatusWindow : EditorWindow
    {
        private string _assetName;
        private NotionSyncProfile _profile;
        private NotionSyncEntry _entry;

        private string[] _statusOptions;
        private string _statusPropertyName;
        private int _selectedIndex;
        private bool _fetching;
        private bool _fetched;
        private string _message;

        internal static void Show(string assetName, NotionSyncProfile profile, NotionSyncEntry entry)
        {
            var window = CreateInstance<AssetStatusWindow>();
            window._assetName = assetName;
            window._profile = profile;
            window._entry = entry;
            window.titleContent = new GUIContent("Update Notion Status");
            window.minSize = new Vector2(360, 200);
            window.maxSize = new Vector2(440, 260);
            window.ShowUtility();
            window.FetchStatuses();
        }

        private async void FetchStatuses()
        {
            _fetching = true;
            _message = "Fetching status options from Notion...";
            Repaint();

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                _fetching = false;
                _message = "No Notion client. Check your API token.";
                Repaint();
                return;
            }

            try
            {
                var db = await client.GetDatabaseAsync(_profile.DatabaseId);
                if (db.Properties == null)
                {
                    _fetching = false;
                    _message = "Database has no properties.";
                    Repaint();
                    return;
                }

                var options = new List<string>();
                foreach (var kvp in db.Properties)
                {
                    if (kvp.Value.Type == "status")
                    {
                        _statusPropertyName = kvp.Key;
                        var selectOptions = kvp.Value.GetSelectOptions();
                        foreach (var opt in selectOptions)
                        {
                            options.Add(opt.Name);
                        }
                        break;
                    }
                }

                if (options.Count == 0)
                {
                    _fetching = false;
                    _message = "No status property found in the database.";
                    Repaint();
                    return;
                }

                _statusOptions = options.ToArray();
                _fetched = true;
                _fetching = false;
                _message = null;
                Repaint();
            }
            catch (Exception ex)
            {
                _fetching = false;
                _message = $"Error: {ex.Message}";
                Debug.LogError($"[Unition] Failed to fetch statuses: {ex}");
                Repaint();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Update Notion Status", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Asset", _assetName);
            EditorGUILayout.LabelField("Page ID", _entry.NotionPageId.Substring(0, Math.Min(8, _entry.NotionPageId.Length)) + "...");

            EditorGUILayout.Space(8);

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _fetching ? MessageType.Info : MessageType.Warning);
            }

            if (_fetched && _statusOptions != null)
            {
                _selectedIndex = EditorGUILayout.Popup("Status", _selectedIndex, _statusOptions);

                EditorGUILayout.Space(8);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Cancel"))
                {
                    Close();
                }

                if (GUILayout.Button("Update Status"))
                {
                    UpdateStatus();
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private async void UpdateStatus()
        {
            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                _message = "No Notion client.";
                Repaint();
                return;
            }

            _message = "Updating...";
            Repaint();

            try
            {
                var properties = new JObject();
                properties[_statusPropertyName] = NotionPropertyBuilder.Status(_statusOptions[_selectedIndex]);

                await client.UpdatePageAsync(_entry.NotionPageId, properties);
                _message = $"Status updated to \"{_statusOptions[_selectedIndex]}\"!";
                Repaint();

                EditorApplication.delayCall += () =>
                {
                    EditorUtility.DisplayDialog("Unition",
                        $"Status updated to \"{_statusOptions[_selectedIndex]}\".", "OK");
                    Close();
                };
            }
            catch (Exception ex)
            {
                _message = $"Error: {ex.Message}";
                Debug.LogError($"[Unition] Failed to update status: {ex}");
                Repaint();
            }
        }
    }
}
