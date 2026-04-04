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
using UnityEngine.UIElements;

namespace Unition.Editor.UI
{
    public sealed class PageDetailView : VisualElement
    {
        public event Action OnCloseRequested;
        public event Action OnPageSaved;

        private readonly Label _title;
        private readonly VisualElement _propertiesContainer;
        private readonly Button _openButton;
        private readonly Button _editToggleButton;
        private readonly Button _saveButton;
        private readonly VisualElement _emptyState;
        private readonly VisualElement _syncBar;
        private readonly Label _syncLabel;
        private readonly Button _syncAssetButton;

        private NotionPage _page;
        private bool _editMode;
        private Dictionary<string, JObject> _editedProperties = new Dictionary<string, JObject>();
        private bool _isResizing;
        private float _resizeStartX;
        private float _resizeStartWidth;

        public PageDetailView()
        {
            AddToClassList("unition-detail-panel");

            var resizeHandle = new VisualElement();
            resizeHandle.style.position = Position.Absolute;
            resizeHandle.style.left = -3;
            resizeHandle.style.top = 0;
            resizeHandle.style.bottom = 0;
            resizeHandle.style.width = 6;
            resizeHandle.RegisterCallback<PointerEnterEvent>(_ =>
                resizeHandle.style.backgroundColor = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 0.3f)));
            resizeHandle.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (!_isResizing)
                {
                    resizeHandle.style.backgroundColor = StyleKeyword.None;
                }
            });
            resizeHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0) { return; }
                _isResizing = true;
                _resizeStartX = evt.position.x;
                _resizeStartWidth = resolvedStyle.width;
                resizeHandle.CapturePointer(evt.pointerId);
                evt.StopPropagation();
            });
            resizeHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_isResizing) { return; }
                var delta = _resizeStartX - evt.position.x;
                var newWidth = Mathf.Max(200f, Mathf.Min(_resizeStartWidth + delta, 600f));
                style.width = newWidth;
                evt.StopPropagation();
            });
            resizeHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!_isResizing) { return; }
                _isResizing = false;
                resizeHandle.ReleasePointer(evt.pointerId);
                resizeHandle.style.backgroundColor = StyleKeyword.None;
            });
            Add(resizeHandle);

            var closeBtn = new Button(() => OnCloseRequested?.Invoke()) { text = "\u2715", tooltip = "Close detail panel" };
            closeBtn.AddToClassList("unition-detail__close-btn");
            Add(closeBtn);

            _emptyState = new VisualElement();
            _emptyState.AddToClassList("unition-empty");
            var emptyLabel = new Label("Click a row to see details.");
            emptyLabel.AddToClassList("unition-empty__text");
            _emptyState.Add(emptyLabel);
            Add(_emptyState);

            _title = new Label();
            _title.AddToClassList("unition-detail__title");
            _title.style.display = DisplayStyle.None;
            Add(_title);

            _openButton = new Button(OnOpenClicked) { text = "Open in Notion ↗" };
            _openButton.AddToClassList("unition-detail__open-btn");
            _openButton.style.display = DisplayStyle.None;
            Add(_openButton);

            _syncBar = new VisualElement();
            _syncBar.style.flexDirection = FlexDirection.Column;
            _syncBar.style.backgroundColor = new StyleColor(new Color(0.18f, 0.25f, 0.22f));
            _syncBar.style.borderTopLeftRadius = 4;
            _syncBar.style.borderTopRightRadius = 4;
            _syncBar.style.borderBottomLeftRadius = 4;
            _syncBar.style.borderBottomRightRadius = 4;
            _syncBar.style.paddingLeft = 8;
            _syncBar.style.paddingRight = 8;
            _syncBar.style.paddingTop = 3;
            _syncBar.style.paddingBottom = 3;
            _syncBar.style.marginLeft = 8;
            _syncBar.style.marginRight = 8;
            _syncBar.style.marginBottom = 4;
            _syncBar.style.display = DisplayStyle.None;
            Add(_syncBar);

            var syncRow = new VisualElement();
            syncRow.style.flexDirection = FlexDirection.Row;
            syncRow.style.alignItems = Align.Center;
            _syncBar.Add(syncRow);

            var syncDot = new VisualElement();
            syncDot.style.width = 8;
            syncDot.style.height = 8;
            syncDot.style.backgroundColor = new StyleColor(new Color(0.3f, 0.78f, 0.69f));
            syncDot.style.borderTopLeftRadius = 4;
            syncDot.style.borderTopRightRadius = 4;
            syncDot.style.borderBottomLeftRadius = 4;
            syncDot.style.borderBottomRightRadius = 4;
            syncDot.style.marginRight = 6;
            syncRow.Add(syncDot);

            _syncLabel = new Label();
            _syncLabel.style.color = new StyleColor(new Color(0.3f, 0.78f, 0.69f));
            _syncLabel.style.fontSize = 11;
            _syncLabel.style.flexGrow = 1;
            syncRow.Add(_syncLabel);

            _syncAssetButton = new Button { text = "" };
            _syncAssetButton.style.marginTop = 4;
            _syncAssetButton.style.fontSize = 11;
            _syncAssetButton.style.flexGrow = 1;
            _syncBar.Add(_syncAssetButton);

            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.FlexEnd;
            buttonRow.style.marginTop = 4;
            buttonRow.style.marginBottom = 4;
            buttonRow.style.display = DisplayStyle.None;
            Add(buttonRow);

            _editToggleButton = new Button(OnEditToggle) { text = "✏ Edit" };
            _editToggleButton.style.marginRight = 4;
            buttonRow.Add(_editToggleButton);

            _saveButton = new Button(OnSaveClicked) { text = "Save to Notion", tooltip = "Push edited properties back to Notion." };
            _saveButton.AddToClassList("unition-btn-primary");
            _saveButton.SetEnabled(false);
            buttonRow.Add(_saveButton);

            _propertiesContainer = new ScrollView(ScrollViewMode.Vertical);
            _propertiesContainer.style.flexGrow = 1;
            _propertiesContainer.style.display = DisplayStyle.None;
            Add(_propertiesContainer);
        }

        public void ShowPage(NotionPage page)
        {
            _page = page;
            _editMode = false;
            _editedProperties.Clear();

            _emptyState.style.display = DisplayStyle.None;
            _title.style.display = DisplayStyle.Flex;
            _openButton.style.display = DisplayStyle.Flex;
            _propertiesContainer.style.display = DisplayStyle.Flex;

            _editToggleButton.parent.style.display = DisplayStyle.Flex;
            _editToggleButton.text = "✏ Edit";
            _saveButton.SetEnabled(false);

            _title.text = page.GetTitle() ?? "(Untitled)";

            UpdateSyncBar(page.Id);

            RebuildProperties();
        }

        public void ClearPage()
        {
            _page = null;
            _editMode = false;
            _editedProperties.Clear();

            _emptyState.style.display = DisplayStyle.Flex;
            _title.style.display = DisplayStyle.None;
            _openButton.style.display = DisplayStyle.None;
            _syncBar.style.display = DisplayStyle.None;
            _propertiesContainer.style.display = DisplayStyle.None;
            _propertiesContainer.Clear();

            if (_editToggleButton.parent != null)
            {
                _editToggleButton.parent.style.display = DisplayStyle.None;
            }
        }

        private void RebuildProperties()
        {
            _propertiesContainer.Clear();

            if (_page?.Properties == null)
            {
                return;
            }

            foreach (var kvp in _page.Properties)
            {
                var section = new VisualElement();
                section.AddToClassList("unition-detail__section");

                var propName = new Label(kvp.Key);
                propName.AddToClassList("unition-detail__prop-name");
                section.Add(propName);

                VisualElement rendered;
                if (_editMode)
                {
                    rendered = NotionPropertyEditor.CreateField(kvp.Key, kvp.Value, _editedProperties);
                }
                else
                {
                    rendered = NotionPropertyRenderer.Render(kvp.Value);
                }

                rendered.AddToClassList("unition-detail__prop-value");
                section.Add(rendered);

                _propertiesContainer.Add(section);
            }
        }

        private void OnEditToggle()
        {
            _editMode = !_editMode;
            _editToggleButton.text = _editMode ? "✏ View" : "✏ Edit";
            _saveButton.SetEnabled(_editMode);
            _editedProperties.Clear();
            RebuildProperties();
        }

        private async void OnSaveClicked()
        {
            if (_page == null || _editedProperties.Count == 0)
            {
                return;
            }

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                Debug.LogError("[Unition] No Notion client available.");
                return;
            }

            _saveButton.SetEnabled(false);
            _saveButton.text = "Saving...";

            try
            {
                var merged = new JObject();
                foreach (var kvp in _editedProperties)
                {
                    merged[kvp.Key] = kvp.Value;
                }

                await client.UpdatePageAsync(_page.Id, merged);

                foreach (var kvp in _editedProperties)
                {
                    if (_page.Properties.TryGetValue(kvp.Key, out var prop))
                    {
                        var type = prop.Type;
                        if (kvp.Value.TryGetValue(type, out var newValue))
                        {
                            prop.Value = newValue;
                        }
                    }
                }

                _editedProperties.Clear();
                _editMode = false;
                _editToggleButton.text = "\u270F Edit";
                _saveButton.SetEnabled(false);
                _saveButton.text = "Saved \u2713";
                RebuildProperties();
                OnPageSaved?.Invoke();

                schedule.Execute(() =>
                {
                    _saveButton.text = "Save to Notion";
                }).ExecuteLater(2000);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Unition] Failed to save page: {ex.Message}");
                _saveButton.text = "Error!";
                _saveButton.SetEnabled(true);

                schedule.Execute(() =>
                {
                    _saveButton.text = "Save to Notion";
                }).ExecuteLater(2000);
            }
        }

        public bool HasPage => _page != null;

        public bool IsEditMode => _editMode;

        public bool HasPendingEdits => _editedProperties.Count > 0;

        public void ToggleEditMode()
        {
            if (_page != null)
            {
                OnEditToggle();
            }
        }

        public void SaveEdits()
        {
            if (_page != null && _editMode && _editedProperties.Count > 0)
            {
                OnSaveClicked();
            }
        }

        private void OnOpenClicked()
        {
            if (_page?.Url != null)
            {
                Application.OpenURL(_page.Url);
            }
        }

        private void UpdateSyncBar(string pageId)
        {
            _syncBar.style.display = DisplayStyle.None;
            _syncAssetButton.style.display = DisplayStyle.None;

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
                if (entry == null)
                {
                    continue;
                }

                _syncBar.style.display = DisplayStyle.Flex;
                _syncLabel.text = $"Synced  •  {profile.name}";

                if (!string.IsNullOrEmpty(entry.LocalAssetGuid))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(entry.LocalAssetGuid);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                        if (asset != null)
                        {
                            _syncAssetButton.style.display = DisplayStyle.Flex;
                            _syncAssetButton.text = asset.name;
                            _syncAssetButton.tooltip = assetPath;
                            _syncAssetButton.clickable = new Clickable(() =>
                            {
                                EditorGUIUtility.PingObject(asset);
                                Selection.activeObject = asset;
                            });
                        }
                    }
                }

                return;
            }
        }

        public string CurrentPageId => _page?.Id;

        public async void RefreshCurrentPage()
        {
            if (_page == null)
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
                var freshPage = await client.GetPageAsync(_page.Id);
                if (freshPage != null)
                {
                    ShowPage(freshPage);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Unition] Failed to refresh page: {ex.Message}");
            }
        }
    }
}
