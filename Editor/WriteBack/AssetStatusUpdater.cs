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

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.WriteBack
{
    internal static class AssetStatusUpdater
    {
        [MenuItem("Assets/Unition/Update Notion Status", false, 1000)]
        private static void OpenUpdater()
        {
            var selected = Selection.activeObject as ScriptableObject;
            if (selected == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

            NotionSyncProfile matchedProfile = null;
            NotionSyncEntry matchedEntry = null;

            var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            foreach (var guid in profileGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(path);
                if (profile == null)
                {
                    continue;
                }

                foreach (var entry in profile.SyncEntries)
                {
                    if (entry.LocalAssetGuid == assetGuid)
                    {
                        matchedProfile = profile;
                        matchedEntry = entry;
                        break;
                    }
                }

                if (matchedProfile != null)
                {
                    break;
                }
            }

            if (matchedProfile == null || matchedEntry == null)
            {
                EditorUtility.DisplayDialog("Unition",
                    "This asset is not tracked by any Notion Sync Profile.", "OK");
                return;
            }

            AssetStatusWindow.Show(selected.name, matchedProfile, matchedEntry);
        }

        [MenuItem("Assets/Unition/Update Notion Status", true)]
        private static bool ValidateOpenUpdater()
        {
            return Selection.activeObject is ScriptableObject;
        }

        [MenuItem("Assets/Unition/Push to Notion", false, 1001)]
        private static void OpenWriteBack()
        {
            var selected = Selection.activeObject as ScriptableObject;
            if (selected == null)
            {
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

            NotionSyncProfile matchedProfile = null;
            NotionSyncEntry matchedEntry = null;

            var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            foreach (var guid in profileGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(path);
                if (profile == null)
                {
                    continue;
                }

                foreach (var entry in profile.SyncEntries)
                {
                    if (entry.LocalAssetGuid == assetGuid)
                    {
                        matchedProfile = profile;
                        matchedEntry = entry;
                        break;
                    }
                }

                if (matchedProfile != null)
                {
                    break;
                }
            }

            if (matchedProfile == null || matchedEntry == null)
            {
                EditorUtility.DisplayDialog("Unition",
                    "This asset is not tracked by any Notion Sync Profile.", "OK");
                return;
            }

            AssetWriteBackWindow.Show(selected, matchedProfile, matchedEntry);
        }

        [MenuItem("Assets/Unition/Push to Notion", true)]
        private static bool ValidateOpenWriteBack()
        {
            return Selection.activeObject is ScriptableObject;
        }
    }
}
