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

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.Sync
{
    public static class NotionSyncProfileTemplates
    {
        [MenuItem("Assets/Create/Unition/Template — Character Data", false, 1100)]
        private static void CreateCharacterTemplate()
        {
            CreateFromTemplate("CharacterSyncProfile", "Characters", new List<NotionPropertyMapping>
            {
                Mapping("Name", "title", "characterName", "string"),
                Mapping("HP", "number", "hp", "int"),
                Mapping("Attack", "number", "attack", "int"),
                Mapping("Defense", "number", "defense", "int"),
                Mapping("Speed", "number", "speed", "float"),
                Mapping("Class", "select", "className", "string"),
                Mapping("Description", "rich_text", "description", "string"),
                Mapping("Icon", "files", "icon", "string"),
            });
        }

        [MenuItem("Assets/Create/Unition/Template — Item Data", false, 1101)]
        private static void CreateItemTemplate()
        {
            CreateFromTemplate("ItemSyncProfile", "Items", new List<NotionPropertyMapping>
            {
                Mapping("Name", "title", "itemName", "string"),
                Mapping("Type", "select", "itemType", "string"),
                Mapping("Rarity", "select", "rarity", "string"),
                Mapping("Value", "number", "value", "int"),
                Mapping("Stackable", "checkbox", "stackable", "bool"),
                Mapping("Description", "rich_text", "description", "string"),
                Mapping("Icon", "files", "icon", "string"),
            });
        }

        [MenuItem("Assets/Create/Unition/Template — Quest Data", false, 1102)]
        private static void CreateQuestTemplate()
        {
            CreateFromTemplate("QuestSyncProfile", "Quests", new List<NotionPropertyMapping>
            {
                Mapping("Name", "title", "questName", "string"),
                Mapping("Description", "rich_text", "description", "string"),
                Mapping("Objective", "rich_text", "objective", "string"),
                Mapping("Reward XP", "number", "rewardXP", "int"),
                Mapping("Status", "status", "status", "string"),
                Mapping("Prerequisites", "relation", "prerequisites", "string"),
            });
        }

        [MenuItem("Assets/Create/Unition/Template — Localization", false, 1103)]
        private static void CreateLocalizationTemplate()
        {
            CreateFromTemplate("LocalizationSyncProfile", "Localization", new List<NotionPropertyMapping>
            {
                Mapping("Key", "title", "key", "string"),
                Mapping("EN", "rich_text", "en", "string"),
                Mapping("ES", "rich_text", "es", "string"),
                Mapping("FR", "rich_text", "fr", "string"),
            });
        }

        private static void CreateFromTemplate(string fileName, string outputSubfolder, List<NotionPropertyMapping> mappings)
        {
            var profile = ScriptableObject.CreateInstance<NotionSyncProfile>();
            profile.OutputPath = $"Assets/Data/{outputSubfolder}/";
            profile.NamingProperty = "Name";
            profile.OutputFormat = NotionOutputFormat.ScriptableObject;
            profile.SyncMode = NotionSyncMode.Manual;
            profile.ConflictResolution = NotionConflictResolution.NotionWins;

            foreach (var mapping in mappings)
            {
                profile.PropertyMappings.Add(mapping);
            }

            var path = GetSelectedFolder() + $"/{fileName}.asset";
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = profile;
        }

        private static NotionPropertyMapping Mapping(string notionName, string notionType, string targetField, string targetType)
        {
            return new NotionPropertyMapping
            {
                NotionPropertyName = notionName,
                NotionPropertyType = notionType,
                TargetFieldName = targetField,
                TargetFieldType = targetType,
                Enabled = true,
            };
        }

        private static string GetSelectedFolder()
        {
            var selected = Selection.activeObject;
            if (selected != null)
            {
                var path = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(path))
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        return path;
                    }
                    return System.IO.Path.GetDirectoryName(path);
                }
            }
            return "Assets";
        }
    }
}
