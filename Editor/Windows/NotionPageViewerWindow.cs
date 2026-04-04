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
using System.Linq;

using Newtonsoft.Json.Linq;

using Unition.Editor.Sync;
using Unition.Editor.UI;
using Unition.Models;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Unition.Editor.Windows
{
    public sealed class NotionPageViewerWindow : EditorWindow
    {
        private string _pageId;
        private Label _titleLabel;
        private ScrollView _contentScroll;
        private Label _statusLabel;
        private TextField _searchField;
        private ScrollView _searchResults;
        private long _searchVersion;

        [MenuItem("Window/Unition/Notion Page Viewer", false, 1002)]
        public static void ShowWindow()
        {
            var window = GetWindow<NotionPageViewerWindow>();
            window.titleContent = new GUIContent("Notion Page Viewer", EditorGUIUtility.IconContent("d_TextAsset Icon").image);
            window.minSize = new Vector2(500, 400);
        }

        public static void ShowPage(string pageId)
        {
            var window = GetWindow<NotionPageViewerWindow>();
            window.titleContent = new GUIContent("Notion Page Viewer");
            window.LoadPage(pageId);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = 8;
            toolbar.style.paddingRight = 8;
            toolbar.style.paddingTop = 4;
            toolbar.style.paddingBottom = 4;
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            root.Add(toolbar);

            var pageIdField = new TextField("Page ID");
            pageIdField.style.flexGrow = 1;
            pageIdField.style.marginRight = 4;
            toolbar.Add(pageIdField);

            var loadBtn = new Button(() => LoadPage(pageIdField.value)) { text = "Load" };
            loadBtn.style.width = 60;
            toolbar.Add(loadBtn);

            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.paddingLeft = 8;
            searchRow.style.paddingRight = 8;
            searchRow.style.paddingTop = 4;
            searchRow.style.paddingBottom = 4;
            root.Add(searchRow);

            var searchIcon = new Label("🔍");
            searchIcon.style.alignSelf = Align.Center;
            searchIcon.style.marginRight = 4;
            searchRow.Add(searchIcon);

            _searchField = new TextField();
            _searchField.style.flexGrow = 1;
            _searchField.RegisterValueChangedCallback(OnSearchChanged);
            searchRow.Add(_searchField);

            _searchResults = new ScrollView(ScrollViewMode.Vertical);
            _searchResults.style.maxHeight = 200;
            _searchResults.style.borderBottomWidth = 1;
            _searchResults.style.borderBottomColor = new Color(0.2f, 0.2f, 0.2f);
            _searchResults.style.display = DisplayStyle.None;
            root.Add(_searchResults);

            _titleLabel = new Label();
            _titleLabel.style.fontSize = 18;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.paddingLeft = 12;
            _titleLabel.style.paddingTop = 8;
            _titleLabel.style.paddingBottom = 4;
            _titleLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_titleLabel);

            _contentScroll = new ScrollView(ScrollViewMode.Vertical);
            _contentScroll.style.flexGrow = 1;
            _contentScroll.style.paddingLeft = 12;
            _contentScroll.style.paddingRight = 12;
            root.Add(_contentScroll);

            _statusLabel = new Label("Enter a Notion page ID and click Load.");
            _statusLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            _statusLabel.style.paddingLeft = 12;
            _statusLabel.style.paddingTop = 8;
            _contentScroll.Add(_statusLabel);
        }

        private async void OnSearchChanged(ChangeEvent<string> evt)
        {
            var query = evt.newValue?.Trim();
            if (string.IsNullOrEmpty(query))
            {
                _searchResults.style.display = DisplayStyle.None;
                _searchResults.Clear();
                return;
            }

            _searchVersion++;
            var version = _searchVersion;

            await System.Threading.Tasks.Task.Delay(300);
            if (version != _searchVersion)
            {
                return;
            }

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                return;
            }

            try
            {
                var result = await client.SearchAsync(query: query, filterObject: "page", pageSize: 20);
                if (version != _searchVersion)
                {
                    return;
                }

                _searchResults.Clear();
                _searchResults.style.display = DisplayStyle.Flex;

                foreach (var jObj in result.Results)
                {
                    try
                    {
                        var page = jObj.ToObject<NotionPage>();
                        if (page == null || page.Archived)
                        {
                            continue;
                        }

                        var title = page.GetTitle() ?? "(Untitled)";
                        var resultBtn = new Button(() =>
                        {
                            _searchField.SetValueWithoutNotify("");
                            _searchResults.style.display = DisplayStyle.None;
                            _searchResults.Clear();
                            LoadPage(page.Id);
                        })
                        { text = title, tooltip = page.Id };
                        resultBtn.style.unityTextAlign = TextAnchor.MiddleLeft;
                        _searchResults.Add(resultBtn);
                    }
                    catch
                    {
                        // skip malformed entries
                    }
                }

                if (_searchResults.childCount == 0)
                {
                    _searchResults.Add(new Label("No pages found."));
                }
            }
            catch
            {
                // ignore search errors
            }
        }

        private async void LoadPage(string pageId)
        {
            if (string.IsNullOrEmpty(pageId))
            {
                return;
            }

            _pageId = pageId.Trim();
            _contentScroll.Clear();
            _titleLabel.text = "";

            var spinner = new VisualElement();
            spinner.style.alignSelf = Align.Center;
            spinner.style.marginTop = 40;
            spinner.style.marginBottom = 8;
            spinner.style.width = 24;
            spinner.style.height = 24;
            spinner.style.borderTopWidth = 3;
            spinner.style.borderLeftWidth = 3;
            spinner.style.borderRightWidth = 3;
            spinner.style.borderBottomWidth = 3;
            spinner.style.borderTopColor = new Color(0.4f, 0.7f, 1f);
            spinner.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
            spinner.style.borderRightColor = new Color(0.3f, 0.3f, 0.3f);
            spinner.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);
            spinner.style.borderTopLeftRadius = 12;
            spinner.style.borderTopRightRadius = 12;
            spinner.style.borderBottomLeftRadius = 12;
            spinner.style.borderBottomRightRadius = 12;
            _contentScroll.Add(spinner);

            var loadingLabel = new Label("Loading page...");
            loadingLabel.style.alignSelf = Align.Center;
            loadingLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            _contentScroll.Add(loadingLabel);

            float angle = 0f;
            spinner.schedule.Execute(() =>
            {
                angle = (angle + 10f) % 360f;
                spinner.style.rotate = new Rotate(angle);
            }).Every(30);

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                _contentScroll.Clear();
                _titleLabel.text = "Error";
                _contentScroll.Add(new Label("No Notion client. Set your token first."));
                return;
            }

            try
            {
                var page = await client.GetPageAsync(_pageId);
                _contentScroll.Clear();
                _titleLabel.text = page.GetTitle() ?? "(Untitled)";

                if (page.Properties != null && page.Properties.Count > 0)
                {
                    RenderDatabaseProperties(page);
                }

                var blocks = new List<NotionBlock>();
                string cursor = null;
                do
                {
                    var result = await client.GetBlockChildrenAsync(_pageId, cursor, 100);
                    if (result.Results != null)
                    {
                        blocks.AddRange(result.Results);
                    }
                    cursor = result.HasMore ? result.NextCursor : null;
                }
                while (cursor != null);

                if (blocks.Count > 0)
                {
                    var contentHeader = CreateHeading("Page Content", 14, 0);
                    contentHeader.style.marginTop = 12;
                    _contentScroll.Add(contentHeader);
                    _contentScroll.Add(CreateDivider());
                    RenderBlocks(blocks, 0);
                }
            }
            catch (Exception ex)
            {
                _contentScroll.Clear();
                _titleLabel.text = "Error";
                _contentScroll.Add(new Label($"Failed to load page: {ex.Message}"));
            }
        }

        private void RenderDatabaseProperties(NotionPage page)
        {
            var syncEntry = FindSyncEntry(page.Id);

            if (syncEntry.profile != null)
            {
                var syncBar = new VisualElement();
                syncBar.style.flexDirection = FlexDirection.Row;
                syncBar.style.alignItems = Align.Center;
                syncBar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.25f, 0.22f));
                syncBar.style.borderTopLeftRadius = 4;
                syncBar.style.borderTopRightRadius = 4;
                syncBar.style.borderBottomLeftRadius = 4;
                syncBar.style.borderBottomRightRadius = 4;
                syncBar.style.paddingLeft = 8;
                syncBar.style.paddingRight = 8;
                syncBar.style.paddingTop = 4;
                syncBar.style.paddingBottom = 4;
                syncBar.style.marginBottom = 8;

                var dot = new VisualElement();
                dot.style.width = 8;
                dot.style.height = 8;
                dot.style.backgroundColor = new StyleColor(new Color(0.3f, 0.78f, 0.69f));
                dot.style.borderTopLeftRadius = 4;
                dot.style.borderTopRightRadius = 4;
                dot.style.borderBottomLeftRadius = 4;
                dot.style.borderBottomRightRadius = 4;
                dot.style.marginRight = 6;
                syncBar.Add(dot);

                var syncLabel = new Label($"Synced  •  {syncEntry.profile.name}");
                syncLabel.style.color = new StyleColor(new Color(0.3f, 0.78f, 0.69f));
                syncLabel.style.flexGrow = 1;
                syncBar.Add(syncLabel);

                if (syncEntry.entry != null && !string.IsNullOrEmpty(syncEntry.entry.LocalAssetGuid))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(syncEntry.entry.LocalAssetGuid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (asset != null)
                        {
                            var pingBtn = new Button(() =>
                            {
                                EditorGUIUtility.PingObject(asset);
                                Selection.activeObject = asset;
                            })
                            { text = asset.name, tooltip = assetPath };
                            pingBtn.style.marginLeft = 4;
                            syncBar.Add(pingBtn);
                        }
                    }
                }

                var browseBtn = new Button(() =>
                    NotionBrowserWindow.ShowDatabase(syncEntry.profile.DatabaseId))
                { text = "Browse", tooltip = "Open database in Notion Browser" };
                browseBtn.style.marginLeft = 4;
                syncBar.Add(browseBtn);

                _contentScroll.Add(syncBar);
            }
            else if (page.Parent != null && page.Parent.Type == "database_id"
                     && !string.IsNullOrEmpty(page.Parent.DatabaseId))
            {
                var dbId = page.Parent.DatabaseId;
                var dbBar = new VisualElement();
                dbBar.style.flexDirection = FlexDirection.Row;
                dbBar.style.alignItems = Align.Center;
                dbBar.style.backgroundColor = new StyleColor(new Color(0.2f, 0.22f, 0.28f));
                dbBar.style.borderTopLeftRadius = 4;
                dbBar.style.borderTopRightRadius = 4;
                dbBar.style.borderBottomLeftRadius = 4;
                dbBar.style.borderBottomRightRadius = 4;
                dbBar.style.paddingLeft = 8;
                dbBar.style.paddingRight = 8;
                dbBar.style.paddingTop = 4;
                dbBar.style.paddingBottom = 4;
                dbBar.style.marginBottom = 8;

                var dbLabel = new Label("Database item");
                dbLabel.style.color = new StyleColor(new Color(0.6f, 0.7f, 0.85f));
                dbLabel.style.flexGrow = 1;
                dbBar.Add(dbLabel);

                var browseBtnUnsync = new Button(() =>
                    NotionBrowserWindow.ShowDatabase(dbId))
                { text = "Browse", tooltip = "Open database in Notion Browser" };
                browseBtnUnsync.style.marginLeft = 4;
                dbBar.Add(browseBtnUnsync);

                _contentScroll.Add(dbBar);
            }

            var sorted = page.Properties
                .Where(p => p.Value.Type != "title")
                .OrderBy(p => p.Key)
                .ToList();

            foreach (var kvp in sorted)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.flexWrap = Wrap.Wrap;
                row.style.marginBottom = 4;
                row.style.paddingLeft = 4;

                var nameLabel = new Label(kvp.Key);
                nameLabel.style.width = 150;
                nameLabel.style.minWidth = 100;
                nameLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                nameLabel.style.whiteSpace = WhiteSpace.Normal;
                row.Add(nameLabel);

                var valueElement = NotionPropertyRenderer.Render(kvp.Value);
                valueElement.style.flexGrow = 1;
                valueElement.style.flexShrink = 1;
                row.Add(valueElement);

                _contentScroll.Add(row);
            }
        }

        private (NotionSyncProfile profile, NotionSyncEntry entry) FindSyncEntry(string pageId)
        {
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
                if (entry != null)
                {
                    return (profile, entry);
                }
            }
            return (null, null);
        }

        private void RenderBlocks(List<NotionBlock> blocks, int indent)
        {
            foreach (var block in blocks)
            {
                var element = RenderBlock(block, indent);
                if (element != null)
                {
                    _contentScroll.Add(element);
                }
            }
        }

        private VisualElement RenderBlock(NotionBlock block, int indent)
        {
            var text = block.GetPlainText() ?? "";

            switch (block.Type)
            {
                case "heading_1":
                    return CreateHeading(text, 20, indent);
                case "heading_2":
                    return CreateHeading(text, 16, indent);
                case "heading_3":
                    return CreateHeading(text, 14, indent);
                case "paragraph":
                    return CreateParagraph(text, indent);
                case "bulleted_list_item":
                    return CreateListItem("•", text, indent);
                case "numbered_list_item":
                    return CreateListItem("", text, indent);
                case "to_do":
                {
                    var content = block.GetContent();
                    var isChecked = content?["checked"]?.ToObject<bool>() ?? false;
                    return CreateListItem(isChecked ? "☑" : "☐", text, indent);
                }
                case "toggle":
                    return CreateListItem("▶", text, indent);
                case "quote":
                    return CreateQuote(text, indent);
                case "callout":
                    return CreateCallout(text, indent);
                case "divider":
                    return CreateDivider();
                case "code":
                    return CreateCodeBlock(text, indent);
                default:
                    if (!string.IsNullOrEmpty(text))
                    {
                        return CreateParagraph(text, indent);
                    }
                    return null;
            }
        }

        private static Label CreateHeading(string text, int fontSize, int indent)
        {
            var label = new Label(text);
            label.style.fontSize = fontSize;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            label.style.paddingLeft = indent * 16;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static Label CreateParagraph(string text, int indent)
        {
            var label = new Label(text);
            label.style.marginBottom = 4;
            label.style.paddingLeft = indent * 16;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }

        private static VisualElement CreateListItem(string bullet, string text, int indent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingLeft = indent * 16 + 8;
            row.style.marginBottom = 2;

            if (!string.IsNullOrEmpty(bullet))
            {
                var bulletLabel = new Label(bullet);
                bulletLabel.style.width = 16;
                bulletLabel.style.marginRight = 4;
                row.Add(bulletLabel);
            }

            var textLabel = new Label(text);
            textLabel.style.flexGrow = 1;
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            row.Add(textLabel);

            return row;
        }

        private static VisualElement CreateQuote(string text, int indent)
        {
            var container = new VisualElement();
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = new Color(0.5f, 0.5f, 0.5f);
            container.style.paddingLeft = indent * 16 + 12;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Italic;
            label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            label.style.whiteSpace = WhiteSpace.Normal;
            container.Add(label);

            return container;
        }

        private static VisualElement CreateCallout(string text, int indent)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.25f));
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.paddingLeft = indent * 16 + 12;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingRight = 8;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            container.Add(label);

            return container;
        }

        private static VisualElement CreateDivider()
        {
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f));
            divider.style.marginTop = 8;
            divider.style.marginBottom = 8;
            return divider;
        }

        private static VisualElement CreateCodeBlock(string text, int indent)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.18f));
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.paddingLeft = indent * 16 + 12;
            container.style.paddingTop = 8;
            container.style.paddingBottom = 8;
            container.style.paddingRight = 8;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;

            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 12;
            container.Add(label);

            return container;
        }
    }
}
