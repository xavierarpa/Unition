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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

using Unition.Models;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.Sync
{
    public static class NotionPropertyMapper
    {
        public static object Convert(NotionPropertyValue property, Type targetType)
        {
            if (property == null || property.Value == null)
            {
                return GetDefault(targetType);
            }

            if (targetType == typeof(string))
            {
                return ConvertToString(property);
            }

            if (targetType == typeof(int))
            {
                return ConvertToInt(property);
            }

            if (targetType == typeof(long))
            {
                return ConvertToLong(property);
            }

            if (targetType == typeof(float))
            {
                return ConvertToFloat(property);
            }

            if (targetType == typeof(double))
            {
                var number = property.AsNumber();
                return number ?? 0.0;
            }

            if (targetType == typeof(bool))
            {
                return property.AsCheckbox();
            }

            if (targetType == typeof(DateTime))
            {
                return property.AsDate() ?? DateTime.MinValue;
            }

            if (targetType == typeof(string[]))
            {
                return ConvertToStringArray(property);
            }

            if (targetType == typeof(List<string>))
            {
                return ConvertToStringList(property);
            }

            if (targetType.IsEnum)
            {
                return ConvertToEnum(property, targetType);
            }

            if (typeof(ScriptableObject).IsAssignableFrom(targetType))
            {
                return ConvertToScriptableObjectRef(property, targetType);
            }

            if (targetType == typeof(Sprite) || targetType == typeof(Texture2D))
            {
                return ConvertToAssetFromFiles(property, targetType);
            }

            if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = targetType.GetGenericArguments()[0];
                if (typeof(ScriptableObject).IsAssignableFrom(elementType))
                {
                    return ConvertToScriptableObjectList(property, elementType);
                }
            }

            if (targetType.IsArray)
            {
                var elementType = targetType.GetElementType();
                if (typeof(ScriptableObject).IsAssignableFrom(elementType))
                {
                    return ConvertToScriptableObjectArray(property, elementType);
                }
            }

            return GetDefault(targetType);
        }

        private static string ConvertToString(NotionPropertyValue property)
        {
            switch (property.Type)
            {
                case "title":
                case "rich_text":
                    return property.AsPlainText() ?? string.Empty;
                case "number":
                    var number = property.AsNumber();
                    return number.HasValue ? number.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
                case "checkbox":
                    return property.AsCheckbox().ToString();
                case "select":
                case "status":
                    var select = property.AsSelect();
                    return select?.Name ?? string.Empty;
                case "url":
                    return property.AsUrl() ?? string.Empty;
                case "date":
                    var date = property.AsDate();
                    return date.HasValue ? date.Value.ToString("o") : string.Empty;
                case "formula":
                    return property.AsFormula() ?? string.Empty;
                case "relation":
                    var ids = property.AsRelationIds();
                    return ids.Count > 0 ? ids[0] : string.Empty;
                case "email":
                    return property.AsEmail() ?? string.Empty;
                case "phone_number":
                    return property.AsPhoneNumber() ?? string.Empty;
                case "rollup":
                    return property.AsRollupDisplay() ?? string.Empty;
                case "created_by":
                case "last_edited_by":
                    return property.AsUserName() ?? string.Empty;
                case "created_time":
                case "last_edited_time":
                    return property.AsTimestamp() ?? string.Empty;
                case "people":
                    var names = property.AsPeopleNames();
                    return names.Count > 0 ? names[0] : string.Empty;
                default:
                    return property.AsPlainText() ?? string.Empty;
            }
        }

        private static int ConvertToInt(NotionPropertyValue property)
        {
            switch (property.Type)
            {
                case "number":
                    var number = property.AsNumber();
                    return number.HasValue ? (int)number.Value : 0;
                case "checkbox":
                    return property.AsCheckbox() ? 1 : 0;
                case "formula":
                    var formula = property.AsFormula();
                    if (formula != null && int.TryParse(formula, out var parsed))
                    {
                        return parsed;
                    }
                    return 0;
                default:
                    var text = property.AsPlainText();
                    if (text != null && int.TryParse(text, out var textParsed))
                    {
                        return textParsed;
                    }
                    return 0;
            }
        }

        private static float ConvertToFloat(NotionPropertyValue property)
        {
            switch (property.Type)
            {
                case "number":
                    var number = property.AsNumber();
                    return number.HasValue ? (float)number.Value : 0f;
                case "formula":
                    var formula = property.AsFormula();
                    if (formula != null && float.TryParse(formula, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                    return 0f;
                default:
                    var text = property.AsPlainText();
                    if (text != null && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var textParsed))
                    {
                        return textParsed;
                    }
                    return 0f;
            }
        }

        private static string[] ConvertToStringArray(NotionPropertyValue property)
        {
            if (property.Type == "multi_select")
            {
                var options = property.AsMultiSelect();
                var result = new string[options.Count];
                for (int i = 0; i < options.Count; i++)
                {
                    result[i] = options[i].Name;
                }
                return result;
            }

            if (property.Type == "relation")
            {
                return property.AsRelationIds().ToArray();
            }

            if (property.Type == "people")
            {
                return property.AsPeopleNames().ToArray();
            }

            var text = property.AsPlainText();
            if (!string.IsNullOrEmpty(text))
            {
                return new[] { text };
            }

            return Array.Empty<string>();
        }

        private static List<string> ConvertToStringList(NotionPropertyValue property)
        {
            if (property.Type == "multi_select")
            {
                var options = property.AsMultiSelect();
                var result = new List<string>(options.Count);
                for (int i = 0; i < options.Count; i++)
                {
                    result.Add(options[i].Name);
                }
                return result;
            }

            if (property.Type == "relation")
            {
                return property.AsRelationIds();
            }

            if (property.Type == "people")
            {
                return property.AsPeopleNames();
            }

            var text = property.AsPlainText();
            if (!string.IsNullOrEmpty(text))
            {
                return new List<string> { text };
            }

            return new List<string>();
        }

        private static object ConvertToEnum(NotionPropertyValue property, Type enumType)
        {
            string text = null;

            switch (property.Type)
            {
                case "select":
                case "status":
                    var select = property.AsSelect();
                    text = select?.Name;
                    break;
                default:
                    text = property.AsPlainText();
                    break;
            }

            if (string.IsNullOrEmpty(text))
            {
                return Enum.ToObject(enumType, 0);
            }

            var cleaned = text.Replace(" ", "").Replace("-", "").Replace("_", "");

            foreach (var name in Enum.GetNames(enumType))
            {
                if (string.Equals(name, cleaned, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse(enumType, name);
                }
            }

            if (int.TryParse(text, out var intVal))
            {
                return Enum.ToObject(enumType, intVal);
            }

            return Enum.ToObject(enumType, 0);
        }

        private static long ConvertToLong(NotionPropertyValue property)
        {
            switch (property.Type)
            {
                case "number":
                    var number = property.AsNumber();
                    return number.HasValue ? (long)number.Value : 0L;
                case "formula":
                    var formula = property.AsFormula();
                    if (formula != null && long.TryParse(formula, out var parsed))
                    {
                        return parsed;
                    }
                    return 0L;
                default:
                    var text = property.AsPlainText();
                    if (text != null && long.TryParse(text, out var textParsed))
                    {
                        return textParsed;
                    }
                    return 0L;
            }
        }

        private static UnityEngine.Object ConvertToScriptableObjectRef(NotionPropertyValue property, Type targetType)
        {
            if (property.Type != "relation")
            {
                return null;
            }

            var ids = property.AsRelationIds();
            if (ids.Count == 0)
            {
                return null;
            }

            var targetPageId = ids[0];

            var guids = AssetDatabase.FindAssets($"t:{targetType.Name}");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, targetType);
                if (asset == null)
                {
                    continue;
                }

                var profiles = AssetDatabase.FindAssets("t:NotionSyncProfile");
                foreach (var profileGuid in profiles)
                {
                    var profilePath = AssetDatabase.GUIDToAssetPath(profileGuid);
                    var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(profilePath);
                    if (profile == null)
                    {
                        continue;
                    }

                    var entry = profile.FindEntryByPageId(targetPageId);
                    if (entry != null && entry.LocalAssetGuid == guid)
                    {
                        return asset;
                    }
                }
            }

            return null;
        }

        private static object ConvertToScriptableObjectList(NotionPropertyValue property, Type elementType)
        {
            if (property.Type != "relation")
            {
                return null;
            }

            var ids = property.AsRelationIds();
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (IList)Activator.CreateInstance(listType);

            if (ids.Count == 0)
            {
                return list;
            }

            var profiles = LoadAllSyncProfiles();
            foreach (var pageId in ids)
            {
                var asset = ResolveScriptableObjectByPageId(pageId, elementType, profiles);
                if (asset != null)
                {
                    list.Add(asset);
                }
            }

            return list;
        }

        private static object ConvertToScriptableObjectArray(NotionPropertyValue property, Type elementType)
        {
            var list = ConvertToScriptableObjectList(property, elementType);
            if (list == null)
            {
                return null;
            }

            var ilist = (IList)list;
            var array = Array.CreateInstance(elementType, ilist.Count);
            ilist.CopyTo(array, 0);
            return array;
        }

        private static UnityEngine.Object ResolveScriptableObjectByPageId(string pageId, Type targetType, List<NotionSyncProfile> profiles)
        {
            foreach (var profile in profiles)
            {
                var entry = profile.FindEntryByPageId(pageId);
                if (entry != null && !string.IsNullOrEmpty(entry.LocalAssetGuid))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath(assetPath, targetType);
                        if (asset != null)
                        {
                            return asset;
                        }
                    }
                }
            }

            return null;
        }

        private static List<NotionSyncProfile> LoadAllSyncProfiles()
        {
            var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            var profiles = new List<NotionSyncProfile>(profileGuids.Length);
            foreach (var guid in profileGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(path);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
            return profiles;
        }

        private static UnityEngine.Object ConvertToAssetFromFiles(NotionPropertyValue property, Type targetType)
        {
            if (property.Type != "files")
            {
                return null;
            }

            var fileUrl = ExtractFirstFileUrl(property);
            if (string.IsNullOrEmpty(fileUrl))
            {
                return null;
            }

            var fileName = Path.GetFileName(new Uri(fileUrl).AbsolutePath);
            var guids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(fileName));
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath(path, targetType);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        private static string ExtractFirstFileUrl(NotionPropertyValue property)
        {
            if (property.Value == null || property.Value.Type != Newtonsoft.Json.Linq.JTokenType.Array)
            {
                return null;
            }

            foreach (var item in property.Value)
            {
                var type = item["type"]?.ToString();
                if (type == "external")
                {
                    var url = item["external"]?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        return url;
                    }
                }
                else if (type == "file")
                {
                    var url = item["file"]?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        return url;
                    }
                }
            }

            return null;
        }

        private static object GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }

            if (type == typeof(string))
            {
                return string.Empty;
            }

            return null;
        }
    }
}
