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
using System.Globalization;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using Unition.Models;

using UnityEditor;

using UnityEngine;

namespace Unition.Editor.Sync
{
    public static class NotionWriteBackEngine
    {
        public static async Task<string> PushAsync(
            ScriptableObject asset,
            NotionSyncProfile profile,
            NotionSyncEntry entry,
            CancellationToken ct = default)
        {
            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                return "No Notion client available. Check your API token.";
            }

            if (string.IsNullOrEmpty(entry.NotionPageId))
            {
                return "No Notion page ID linked to this asset.";
            }

            var properties = BuildProperties(asset, profile);
            if (properties.Count == 0)
            {
                return "No enabled mappings found to push.";
            }

            try
            {
                await client.UpdatePageAsync(entry.NotionPageId, properties, ct);
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static JObject BuildProperties(UnityEngine.Object asset, NotionSyncProfile profile)
        {
            var properties = new JObject();
            var targetType = asset.GetType();

            foreach (var mapping in profile.PropertyMappings)
            {
                if (!mapping.Enabled)
                {
                    continue;
                }

                var field = targetType.GetField(mapping.TargetFieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    continue;
                }

                var value = field.GetValue(asset);
                var prop = ConvertToNotionProperty(value, field.FieldType, mapping.NotionPropertyType);
                if (prop != null)
                {
                    properties[mapping.NotionPropertyName] = prop;
                }
            }

            return properties;
        }

        private static JObject ConvertToNotionProperty(object value, Type fieldType, string notionType)
        {
            if (value == null)
            {
                return null;
            }

            if (notionType != "relation" && IsScriptableObjectField(fieldType))
            {
                return ConvertToRelation(value, fieldType);
            }

            switch (notionType)
            {
                case "title":
                    return NotionPropertyBuilder.Title(Convert.ToString(value));

                case "rich_text":
                    return NotionPropertyBuilder.RichText(Convert.ToString(value));

                case "number":
                    if (TryConvertToDouble(value, out var number))
                    {
                        return NotionPropertyBuilder.Number(number);
                    }
                    return null;

                case "checkbox":
                    if (value is bool boolVal)
                    {
                        return NotionPropertyBuilder.Checkbox(boolVal);
                    }
                    return null;

                case "select":
                    return NotionPropertyBuilder.Select(ConvertValueToString(value, fieldType));

                case "multi_select":
                    return ConvertToMultiSelect(value, fieldType);

                case "status":
                    return NotionPropertyBuilder.Status(ConvertValueToString(value, fieldType));

                case "url":
                    var url = Convert.ToString(value);
                    if (!string.IsNullOrEmpty(url))
                    {
                        return NotionPropertyBuilder.Url(url);
                    }
                    return null;

                case "date":
                    if (value is DateTime dateVal)
                    {
                        return NotionPropertyBuilder.DateWithTime(dateVal);
                    }
                    return null;

                case "relation":
                    return ConvertToRelation(value, fieldType);

                case "email":
                    var email = Convert.ToString(value);
                    if (!string.IsNullOrEmpty(email))
                    {
                        return NotionPropertyBuilder.Email(email);
                    }
                    return null;

                case "phone_number":
                    var phone = Convert.ToString(value);
                    if (!string.IsNullOrEmpty(phone))
                    {
                        return NotionPropertyBuilder.PhoneNumber(phone);
                    }
                    return null;

                default:
                    return null;
            }
        }

        private static bool TryConvertToDouble(object value, out double result)
        {
            if (value is int intVal)
            {
                result = intVal;
                return true;
            }

            if (value is float floatVal)
            {
                result = floatVal;
                return true;
            }

            if (value is double doubleVal)
            {
                result = doubleVal;
                return true;
            }

            if (value is long longVal)
            {
                result = longVal;
                return true;
            }

            result = 0;
            return false;
        }

        private static string ConvertValueToString(object value, Type fieldType)
        {
            if (fieldType.IsEnum)
            {
                return value.ToString();
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static JObject ConvertToMultiSelect(object value, Type fieldType)
        {
            var names = new List<string>();

            if (value is string[] strArray)
            {
                names.AddRange(strArray);
            }
            else if (value is List<string> strList)
            {
                names.AddRange(strList);
            }
            else if (value is IEnumerable<string> strEnumerable)
            {
                names.AddRange(strEnumerable);
            }
            else
            {
                names.Add(Convert.ToString(value));
            }

            return NotionPropertyBuilder.MultiSelect(names.ToArray());
        }

        private static JObject ConvertToRelation(object value, Type fieldType)
        {
            if (value is ScriptableObject so)
            {
                var pageId = FindPageIdForAsset(so);
                if (!string.IsNullOrEmpty(pageId))
                {
                    return NotionPropertyBuilder.Relation(pageId);
                }
            }
            else if (value is string strId && !string.IsNullOrEmpty(strId))
            {
                return NotionPropertyBuilder.Relation(strId);
            }
            else if (value is System.Collections.IList list && list.Count > 0)
            {
                var pageIds = new List<string>();
                foreach (var item in list)
                {
                    if (item is ScriptableObject soItem)
                    {
                        var pageId = FindPageIdForAsset(soItem);
                        if (!string.IsNullOrEmpty(pageId))
                        {
                            pageIds.Add(pageId);
                        }
                    }
                }

                if (pageIds.Count > 0)
                {
                    return NotionPropertyBuilder.Relation(pageIds.ToArray());
                }
            }

            return null;
        }

        private static bool IsScriptableObjectField(Type fieldType)
        {
            if (typeof(ScriptableObject).IsAssignableFrom(fieldType))
            {
                return true;
            }
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                return typeof(ScriptableObject).IsAssignableFrom(fieldType.GetGenericArguments()[0]);
            }
            if (fieldType.IsArray)
            {
                return typeof(ScriptableObject).IsAssignableFrom(fieldType.GetElementType());
            }
            return false;
        }

        private static string FindPageIdForAsset(ScriptableObject asset)
        {
            var assetPath = AssetDatabase.GetAssetPath(asset);
            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrEmpty(assetGuid))
            {
                return null;
            }

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
                        return entry.NotionPageId;
                    }
                }
            }

            return null;
        }
    }
}
