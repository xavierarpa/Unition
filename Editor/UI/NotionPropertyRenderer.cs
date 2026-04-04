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

using Unition.Editor.Sync;
using Unition.Editor.Windows;
using Unition.Models;

using UnityEditor;
using UnityEngine.UIElements;

namespace Unition.Editor.UI
{
    public static class NotionPropertyRenderer
    {
        private static readonly Dictionary<string, string> ColorMap = new Dictionary<string, string>
        {
            { "red", "red" }, { "green", "green" }, { "blue", "blue" },
            { "yellow", "yellow" }, { "purple", "purple" }, { "pink", "pink" },
            { "orange", "orange" }, { "gray", "gray" }, { "brown", "brown" },
            { "default", "default" },
            { "light gray", "gray" }, { "light_gray", "gray" }
        };

        public static VisualElement Render(NotionPropertyValue prop)
        {
            if (prop == null || prop.Type == null)
            {
                return new Label("—");
            }

            switch (prop.Type)
            {
                case "title":
                case "rich_text":
                    return CreateTextLabel(prop.AsPlainText() ?? "");

                case "number":
                {
                    var num = prop.AsNumber();
                    var label = new Label(num.HasValue ? num.Value.ToString("G") : "—");
                    label.AddToClassList("unition-table__cell--number");
                    return label;
                }

                case "checkbox":
                {
                    var val = prop.AsCheckbox();
                    var label = new Label(val ? "✓" : "✗");
                    label.AddToClassList("unition-table__cell--checkbox");
                    label.style.color = val
                        ? new StyleColor(new UnityEngine.Color(0.31f, 0.79f, 0.69f, 1f))
                        : new StyleColor(new UnityEngine.Color(0.5f, 0.5f, 0.5f, 1f));
                    return label;
                }

                case "select":
                case "status":
                {
                    var opt = prop.Type == "select" ? prop.AsSelect() : null;
                    var name = prop.Type == "select"
                        ? opt?.Name
                        : prop.AsStatus();
                    var color = opt?.Color ?? "default";

                    if (string.IsNullOrEmpty(name))
                    {
                        return new Label("—");
                    }
                    return CreatePill(name, color);
                }

                case "multi_select":
                {
                    var options = prop.AsMultiSelect();
                    if (options.Count == 0)
                    {
                        return new Label("—");
                    }
                    var container = new VisualElement();
                    container.style.flexDirection = FlexDirection.Row;
                    container.style.flexWrap = Wrap.Wrap;
                    foreach (var opt in options)
                    {
                        container.Add(CreatePill(opt.Name, opt.Color));
                    }
                    return container;
                }

                case "url":
                {
                    var url = prop.AsUrl();
                    if (string.IsNullOrEmpty(url))
                    {
                        return new Label("—");
                    }
                    var link = new Label(url);
                    link.style.color = new StyleColor(new UnityEngine.Color(0.31f, 0.76f, 1f, 1f));
                    return link;
                }

                case "date":
                {
                    var date = prop.AsDate();
                    return new Label(date.HasValue ? date.Value.ToString("yyyy-MM-dd") : "—");
                }

                case "relation":
                {
                    var ids = prop.AsRelationIds();
                    if (ids.Count == 0)
                    {
                        return new Label("—");
                    }
                    var container = new VisualElement();
                    foreach (var id in ids)
                    {
                        container.Add(CreateRelationRow(id));
                    }
                    return container;
                }

                case "formula":
                {
                    var val = prop.AsFormula();
                    return new Label(val ?? "—");
                }

                case "email":
                {
                    var email = prop.AsEmail();
                    if (string.IsNullOrEmpty(email))
                    {
                        return new Label("—");
                    }
                    var emailLabel = new Label(email);
                    emailLabel.style.color = new StyleColor(new UnityEngine.Color(0.31f, 0.76f, 1f, 1f));
                    return emailLabel;
                }

                case "phone_number":
                {
                    var phone = prop.AsPhoneNumber();
                    if (string.IsNullOrEmpty(phone))
                    {
                        return new Label("—");
                    }
                    var phoneLabel = new Label(phone);
                    phoneLabel.style.color = new StyleColor(new UnityEngine.Color(0.31f, 0.76f, 1f, 1f));
                    return phoneLabel;
                }

                case "people":
                {
                    var names = prop.AsPeopleNames();
                    if (names.Count == 0)
                    {
                        return new Label("—");
                    }
                    var peopleContainer = new VisualElement();
                    peopleContainer.style.flexDirection = FlexDirection.Row;
                    peopleContainer.style.flexWrap = Wrap.Wrap;
                    foreach (var name in names)
                    {
                        peopleContainer.Add(CreatePill(name, "default"));
                    }
                    return peopleContainer;
                }

                case "rollup":
                {
                    var display = prop.AsRollupDisplay();
                    var rollupLabel = new Label(string.IsNullOrEmpty(display) ? "—" : display);
                    rollupLabel.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f));
                    rollupLabel.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Italic;
                    return rollupLabel;
                }

                case "created_by":
                case "last_edited_by":
                {
                    var userName = prop.AsUserName();
                    var userLabel = new Label(string.IsNullOrEmpty(userName) ? "—" : userName);
                    userLabel.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f));
                    return userLabel;
                }

                case "created_time":
                case "last_edited_time":
                {
                    var timestamp = prop.AsTimestamp();
                    if (string.IsNullOrEmpty(timestamp))
                    {
                        return new Label("—");
                    }
                    var timeLabel = new Label(TimeFormatUtility.FormatRelative(timestamp));
                    timeLabel.style.color = new StyleColor(new UnityEngine.Color(0.6f, 0.6f, 0.6f, 1f));
                    return timeLabel;
                }

                default:
                    return new Label(prop.Type);
            }
        }

        private static Label CreateTextLabel(string text)
        {
            var label = new Label(string.IsNullOrEmpty(text) ? "—" : text);
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            return label;
        }

        private static VisualElement CreatePill(string text, string notionColor)
        {
            var pill = new Label(text);
            pill.AddToClassList("unition-pill");

            if (!string.IsNullOrEmpty(notionColor) && ColorMap.TryGetValue(notionColor, out var cssColor))
            {
                pill.AddToClassList($"unition-pill--{cssColor}");
            }
            else
            {
                pill.AddToClassList("unition-pill--default");
            }

            return pill;
        }

        private static VisualElement CreateRelationRow(string pageId)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;

            var openBtn = new Button(() => NotionPageViewerWindow.ShowPage(pageId))
            {
                text = pageId.Length > 8 ? pageId.Substring(0, 8) + "…" : pageId,
                tooltip = "Open in Page Viewer"
            };
            openBtn.style.flexShrink = 1;
            openBtn.style.maxWidth = 200;
            row.Add(openBtn);

            LoadRelationInfoAsync(pageId, openBtn, row);

            return row;
        }

        private static async void LoadRelationInfoAsync(string pageId, Button titleButton, VisualElement row)
        {
            try
            {
                var client = UnitionEditorClient.Client;
                if (client == null)
                {
                    return;
                }

                var page = await client.GetPageAsync(pageId);
                if (page == null)
                {
                    return;
                }

                var title = page.GetTitle();
                if (!string.IsNullOrEmpty(title))
                {
                    titleButton.text = title;
                    titleButton.tooltip = $"{title}\n{pageId}";
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

                    var entry = profile.FindEntryByPageId(pageId);
                    if (entry != null && !string.IsNullOrEmpty(entry.LocalAssetGuid))
                    {
                        var assetPath = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                            if (asset != null)
                            {
                                var assetBtn = new Button(() =>
                                {
                                    EditorGUIUtility.PingObject(asset);
                                    Selection.activeObject = asset;
                                })
                                {
                                    text = asset.name,
                                    tooltip = assetPath
                                };
                                assetBtn.style.marginLeft = 4;
                                assetBtn.style.fontSize = 10;
                                row.Add(assetBtn);
                            }
                        }
                        break;
                    }
                }
            }
            catch
            {
                // silently ignore fetch errors
            }
        }
    }
}
