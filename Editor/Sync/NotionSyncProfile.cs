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
using System.Collections.Generic;

using UnityEngine;

namespace Unition.Editor.Sync
{
    [CreateAssetMenu(menuName = "Unition/Sync Profile", fileName = "NewSyncProfile")]
    public sealed class NotionSyncProfile : ScriptableObject
    {
        [SerializeField] private string databaseId;
        [SerializeField] private string databaseName;
        [SerializeField] private string targetTypeName;
        [SerializeField] private string targetAssemblyName;
        [SerializeField] private string outputPath = "Assets/Data/";
        [SerializeField] private NotionOutputFormat outputFormat = NotionOutputFormat.ScriptableObject;
        [SerializeField] private NotionSyncMode syncMode = NotionSyncMode.Manual;
        [SerializeField] private string namingProperty = "Name";
        [SerializeField] private string filesDownloadPath = "Assets/Notion/Downloads/";
        [SerializeField] private NotionConflictResolution conflictResolution = NotionConflictResolution.NotionWins;
        [SerializeField] private NotionDuplicateStrategy duplicateStrategy = NotionDuplicateStrategy.SkipDuplicates;
        [SerializeField] private List<NotionPropertyMapping> propertyMappings = new List<NotionPropertyMapping>();
        [SerializeField] private List<NotionSyncEntry> syncEntries = new List<NotionSyncEntry>();
        [SerializeField] private List<NotionDownloadedFile> downloadedFiles = new List<NotionDownloadedFile>();
        [SerializeField] private string lastSyncedTime;
        [SerializeField] private string lastSyncLog;

        public string DatabaseId
        {
            get => databaseId;
            set => databaseId = value;
        }

        public string DatabaseName
        {
            get => databaseName;
            set => databaseName = value;
        }

        public string TargetTypeName
        {
            get => targetTypeName;
            set => targetTypeName = value;
        }

        public string TargetAssemblyName
        {
            get => targetAssemblyName;
            set => targetAssemblyName = value;
        }

        public string OutputPath
        {
            get => outputPath;
            set => outputPath = value;
        }

        public NotionOutputFormat OutputFormat
        {
            get => outputFormat;
            set => outputFormat = value;
        }

        public NotionSyncMode SyncMode
        {
            get => syncMode;
            set => syncMode = value;
        }

        public string NamingProperty
        {
            get => namingProperty;
            set => namingProperty = value;
        }

        public string FilesDownloadPath
        {
            get => filesDownloadPath;
            set => filesDownloadPath = value;
        }

        public NotionConflictResolution ConflictResolution
        {
            get => conflictResolution;
            set => conflictResolution = value;
        }

        public NotionDuplicateStrategy DuplicateStrategy
        {
            get => duplicateStrategy;
            set => duplicateStrategy = value;
        }

        public List<NotionPropertyMapping> PropertyMappings => propertyMappings;

        public List<NotionSyncEntry> SyncEntries => syncEntries;

        public List<NotionDownloadedFile> DownloadedFiles => downloadedFiles;

        public string LastSyncedTime
        {
            get => lastSyncedTime;
            set => lastSyncedTime = value;
        }

        public string LastSyncLog
        {
            get => lastSyncLog;
            set => lastSyncLog = value;
        }

        public System.Type ResolveTargetType()
        {
            if (string.IsNullOrEmpty(targetTypeName))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(targetAssemblyName))
            {
                var assembly = System.Reflection.Assembly.Load(targetAssemblyName);
                if (assembly != null)
                {
                    return assembly.GetType(targetTypeName);
                }
            }

            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(targetTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        public NotionSyncEntry FindEntryByPageId(string pageId)
        {
            for (int i = 0; i < syncEntries.Count; i++)
            {
                if (syncEntries[i].NotionPageId == pageId)
                {
                    return syncEntries[i];
                }
            }
            return null;
        }

        public void MarkDirty()
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
    }
}
