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

using Unition.Editor.Sync;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.WriteBack
{
    internal sealed class AssetWriteBackWindow : EditorWindow
    {
        private ScriptableObject _asset;
        private NotionSyncProfile _profile;
        private NotionSyncEntry _entry;
        private string _message;
        private bool _pushing;

        internal static void Show(ScriptableObject asset, NotionSyncProfile profile, NotionSyncEntry entry)
        {
            var window = CreateInstance<AssetWriteBackWindow>();
            window._asset = asset;
            window._profile = profile;
            window._entry = entry;
            window.titleContent = new GUIContent("Push to Notion");
            window.minSize = new Vector2(360, 200);
            window.maxSize = new Vector2(440, 260);
            window.ShowUtility();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Push Asset to Notion", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Asset", _asset != null ? _asset.name : "(null)");
            EditorGUILayout.LabelField("Profile", _profile != null ? _profile.name : "(null)");
            EditorGUILayout.LabelField("Page ID", _entry.NotionPageId.Substring(0, Math.Min(8, _entry.NotionPageId.Length)) + "...");

            EditorGUILayout.Space(4);

            int mappingCount = 0;
            foreach (var m in _profile.PropertyMappings)
            {
                if (m.Enabled)
                {
                    mappingCount++;
                }
            }
            EditorGUILayout.LabelField("Mappings to push", mappingCount.ToString());

            EditorGUILayout.Space(8);

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _pushing ? MessageType.Info : MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            EditorGUI.BeginDisabledGroup(_pushing);
            if (GUILayout.Button("Push All Properties"))
            {
                PushToNotion();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        private async void PushToNotion()
        {
            _pushing = true;
            _message = "Pushing to Notion...";
            Repaint();

            var error = await NotionWriteBackEngine.PushAsync(_asset, _profile, _entry);

            _pushing = false;

            if (error == null)
            {
                _message = "Successfully pushed to Notion!";
                Repaint();
                EditorApplication.delayCall += () =>
                {
                    EditorUtility.DisplayDialog("Unition", "Asset pushed to Notion successfully.", "OK");
                    Close();
                };
            }
            else
            {
                _message = $"Error: {error}";
                Debug.LogError($"[Unition] Push failed: {error}");
                Repaint();
            }
        }
    }
}
