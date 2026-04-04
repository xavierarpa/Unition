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

using UnityEditor;

using UnityEngine;

namespace Unition.Editor
{
    [InitializeOnLoad]
    internal static class UnitionPollingService
    {
        private const string EnabledKey = "Unition_Polling_Enabled";
        private const string IntervalKey = "Unition_Polling_Interval";
        private const float DefaultInterval = 300f;

        private static double _nextPollTime;
        private static bool _isPolling;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                EditorPrefs.SetBool(EnabledKey, value);
                if (value)
                {
                    _nextPollTime = EditorApplication.timeSinceStartup + IntervalSeconds;
                    EditorApplication.update -= OnEditorUpdate;
                    EditorApplication.update += OnEditorUpdate;
                }
                else
                {
                    EditorApplication.update -= OnEditorUpdate;
                }
            }
        }

        private const float MinIntervalSeconds = 30f;

        public static float IntervalSeconds
        {
            get => EditorPrefs.GetFloat(IntervalKey, DefaultInterval);
            set => EditorPrefs.SetFloat(IntervalKey, Mathf.Max(MinIntervalSeconds, value));
        }

        static UnitionPollingService()
        {
            if (Enabled)
            {
                _nextPollTime = EditorApplication.timeSinceStartup + IntervalSeconds;
                EditorApplication.update += OnEditorUpdate;
            }
        }

        private static void OnEditorUpdate()
        {
            if (!Enabled || _isPolling)
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextPollTime)
            {
                return;
            }

            _nextPollTime = EditorApplication.timeSinceStartup + IntervalSeconds;
            PollDatabases();
        }

        private static async void PollDatabases()
        {
            if (!UnitionCredentials.HasToken)
            {
                return;
            }

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                return;
            }

            _isPolling = true;

            try
            {
                var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
                foreach (var guid in profileGuids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var profile = AssetDatabase.LoadAssetAtPath<Sync.NotionSyncProfile>(path);
                    if (profile == null || profile.SyncMode != Sync.NotionSyncMode.Auto)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(profile.DatabaseId))
                    {
                        continue;
                    }

                    try
                    {
                        var db = await client.GetDatabaseAsync(profile.DatabaseId);
                        if (db == null)
                        {
                            continue;
                        }

                        if (!string.IsNullOrEmpty(profile.LastSyncedTime) &&
                            !string.IsNullOrEmpty(db.LastEditedTime))
                        {
                            if (DateTime.TryParse(profile.LastSyncedTime, out var lastSync) &&
                                DateTime.TryParse(db.LastEditedTime, out var dbEdited))
                            {
                                if (dbEdited > lastSync)
                                {
                                    Debug.Log($"[Unition] Database '{profile.DatabaseName}' has changes. Auto-syncing...");
                                    await Sync.NotionSyncEngine.SyncAsync(profile);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Unition] Polling failed for '{profile.DatabaseName}': {ex.Message}");
                    }
                }
            }
            finally
            {
                _isPolling = false;
            }
        }
    }
}
