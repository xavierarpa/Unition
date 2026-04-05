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
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json.Linq;

using Unition.Editor.Windows;
using Unition.Models;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Unition.Editor.UI
{
    public sealed class DatabaseTableView : VisualElement
    {
        public event Action<NotionPage> OnPageSelected;

        private readonly VisualElement _toolbar;
        private readonly Label _infoLabel;
        private readonly TextField _searchField;
        private readonly VisualElement _headerWrapper;
        private readonly VisualElement _headerRow;
        private readonly ScrollView _scrollView;
        private readonly Button _loadMoreButton;
        private readonly VisualElement _loadingContainer;
        private readonly VisualElement _emptyContainer;

        private NotionDatabase _database;
        private List<NotionPage> _pages = new List<NotionPage>();
        private List<string> _columnNames = new List<string>();
        private string _nextCursor;
        private bool _isLoading;
        private string _selectedPageId;
        private readonly HashSet<string> _selectedPageIds = new HashSet<string>();
        private string _sortColumn;
        private bool _sortAscending = true;
        private readonly Dictionary<string, float> _columnWidths = new Dictionary<string, float>();
        private const float DefaultColumnWidth = 150f;
        private const float MinColumnWidth = 60f;

        private static readonly HashSet<string> HiddenPropertyTypes = new HashSet<string> { "button" };
        private int _dragColumnIndex = -1;
        private int _dropTargetIndex = -1;

        public DatabaseTableView()
        {
            AddToClassList("unition-table");

            _toolbar = new VisualElement();
            _toolbar.AddToClassList("unition-table__toolbar");
            Add(_toolbar);

            _infoLabel = new Label();
            _infoLabel.AddToClassList("unition-table__info");
            _toolbar.Add(_infoLabel);

            var refreshBtn = new Button(Refresh) { text = "↻", tooltip = "Refresh current database (F5)" };
            refreshBtn.style.width = 28;
            refreshBtn.style.height = 22;
            _toolbar.Add(refreshBtn);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            _toolbar.Add(spacer);

            _searchField = new TextField();
            _searchField.AddToClassList("unition-table__search");
            _searchField.RegisterValueChangedCallback(_ => RebuildRows());
            _toolbar.Add(_searchField);

            var newPageBtn = new Button(OnNewPageClicked) { text = "+ New Page", tooltip = "Create a new page in this Notion database." };
            newPageBtn.AddToClassList("unition-btn-primary");
            _toolbar.Add(newPageBtn);

            var exportCsvBtn = new Button(ExportCsv) { text = "CSV", tooltip = "Export visible rows as CSV." };
            exportCsvBtn.style.width = 40;
            _toolbar.Add(exportCsvBtn);

            var exportJsonBtn = new Button(ExportJson) { text = "JSON", tooltip = "Export visible rows as JSON." };
            exportJsonBtn.style.width = 45;
            _toolbar.Add(exportJsonBtn);

            _headerWrapper = new VisualElement();
            _headerWrapper.style.overflow = Overflow.Hidden;
            _headerWrapper.style.flexShrink = 0;
            Add(_headerWrapper);

            _headerRow = new VisualElement();
            _headerRow.AddToClassList("unition-table__header-row");
            _headerWrapper.Add(_headerRow);

            _scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            _scrollView.AddToClassList("unition-table__scroll");
            Add(_scrollView);

            _scrollView.horizontalScroller.valueChanged += v =>
            {
                _headerRow.style.translate = new Translate(-v, 0);
            };

            _loadMoreButton = new Button(OnLoadMoreClicked) { text = "Load more..." };
            _loadMoreButton.AddToClassList("unition-load-more");
            _loadMoreButton.style.display = DisplayStyle.None;
            Add(_loadMoreButton);

            _loadingContainer = new VisualElement();
            _loadingContainer.AddToClassList("unition-loading");
            _loadingContainer.style.display = DisplayStyle.None;
            var spinner = new VisualElement();
            spinner.AddToClassList("unition-loading__spinner");
            _loadingContainer.Add(spinner);
            spinner.schedule.Execute(() =>
            {
                var angle = spinner.resolvedStyle.rotate.angle.value;
                spinner.style.rotate = new Rotate(Angle.Degrees((angle + 30f) % 360f));
            }).Every(50);
            var loadingLabel = new Label("Loading rows...");
            loadingLabel.AddToClassList("unition-loading__text");
            _loadingContainer.Add(loadingLabel);
            Add(_loadingContainer);

            _emptyContainer = new VisualElement();
            _emptyContainer.AddToClassList("unition-empty");
            _emptyContainer.style.display = DisplayStyle.None;
            var emptyLabel = new Label("Select a database from the sidebar.");
            emptyLabel.AddToClassList("unition-empty__text");
            _emptyContainer.Add(emptyLabel);
            Add(_emptyContainer);

            ShowEmpty("Select a database from the sidebar.");

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        public void Refresh()
        {
            if (_database != null)
            {
                NotionCache.InvalidateRows(_database.Id);
                LoadDatabase(_database);
            }
        }

        private float GetColumnWidth(string colName)
        {
            if (_columnWidths.TryGetValue(colName, out var w))
            {
                return w;
            }

            if (_database != null)
            {
                var key = $"Unition_ColWidth_{_database.Id}_{colName}";
                var stored = EditorPrefs.GetFloat(key, 0f);
                if (stored > 0f)
                {
                    _columnWidths[colName] = stored;
                    return stored;
                }
            }

            return DefaultColumnWidth;
        }

        private void SetColumnWidth(string colName, float width)
        {
            width = Mathf.Max(width, MinColumnWidth);
            _columnWidths[colName] = width;
            if (_database != null)
            {
                var key = $"Unition_ColWidth_{_database.Id}_{colName}";
                EditorPrefs.SetFloat(key, width);
            }
        }

        public async void LoadDatabase(NotionDatabase database)
        {
            _database = database;
            _pages.Clear();
            _nextCursor = null;
            _selectedPageId = null;
            _selectedPageIds.Clear();
            _columnWidths.Clear();
            _sortColumn = null;
            _searchField.value = "";
            _scrollView.Clear();
            _scrollView.scrollOffset = Vector2.zero;

            NotionCache.SetDatabase(database.Id, database);
            RestoreColumnOrder();
            BuildColumns();

            if (NotionCache.TryGetRows(database.Id, out var cachedPages))
            {
                _pages.AddRange(cachedPages);
                _infoLabel.text = $"{_pages.Count} rows (cached) — {_database.PlainTitle}";
                RebuildRows();
                return;
            }

            await LoadPageBatchAsync();
        }

        private void BuildColumns()
        {
            _headerRow.Clear();
            _headerRow.style.translate = new Translate(0, 0);
            _columnNames.Clear();

            if (_database?.Properties == null)
            {
                return;
            }

            var titleProp = _database.Properties
                .FirstOrDefault(p => p.Value.Type == "title");

            if (titleProp.Key != null)
            {
                _columnNames.Add(titleProp.Key);
            }

            foreach (var kvp in _database.Properties.OrderBy(p => p.Value.Id))
            {
                if (kvp.Value.Type == "title")
                {
                    continue;
                }
                if (HiddenPropertyTypes.Contains(kvp.Value.Type))
                {
                    continue;
                }
                _columnNames.Add(kvp.Key);
            }

            ApplySavedColumnOrder();

            foreach (var colName in _columnNames)
            {
                var prop = _database.Properties[colName];
                var col = colName;
                var sortIndicator = "";
                if (_sortColumn == colName)
                {
                    sortIndicator = _sortAscending ? " ▲" : " ▼";
                }

                var colWidth = GetColumnWidth(colName);

                var headerContainer = new VisualElement();
                headerContainer.AddToClassList("unition-table__header-cell");
                headerContainer.style.width = colWidth;
                headerContainer.style.flexDirection = FlexDirection.Row;
                headerContainer.style.position = Position.Relative;

                var headerLabel = new Label($"{colName} ({prop.Type}){sortIndicator}");
                headerLabel.style.flexGrow = 1;
                headerLabel.style.overflow = Overflow.Hidden;
                headerLabel.style.textOverflow = TextOverflow.Ellipsis;
                headerLabel.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_sortColumn == col && !_sortAscending)
                    {
                        _sortColumn = null;
                    }
                    else if (_sortColumn == col)
                    {
                        _sortAscending = !_sortAscending;
                    }
                    else
                    {
                        _sortColumn = col;
                        _sortAscending = true;
                    }
                    BuildColumns();
                    RebuildRows();
                });
                headerContainer.Add(headerLabel);

                var resizeHandle = new VisualElement();
                resizeHandle.style.position = Position.Absolute;
                resizeHandle.style.right = -4;
                resizeHandle.style.top = 0;
                resizeHandle.style.bottom = 0;
                resizeHandle.style.width = 8;
                resizeHandle.style.backgroundColor = new StyleColor(Color.clear);
                resizeHandle.AddToClassList("unition-table__header-cell--resizable");

                // Column drag-to-reorder
                var colIndex = _columnNames.IndexOf(colName);
                headerContainer.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0 && !resizeHandle.HasMouseCapture())
                    {
                        _dragColumnIndex = colIndex;
                    }
                });
                headerContainer.RegisterCallback<MouseEnterEvent>(_ =>
                {
                    if (_dragColumnIndex >= 0 && _dragColumnIndex != colIndex)
                    {
                        _dropTargetIndex = colIndex;
                        headerContainer.style.borderLeftWidth = 2;
                        headerContainer.style.borderLeftColor = new StyleColor(new Color(0.31f, 0.76f, 1f, 1f));
                    }
                });
                headerContainer.RegisterCallback<MouseLeaveEvent>(_ =>
                {
                    headerContainer.style.borderLeftWidth = 0;
                    if (_dropTargetIndex == colIndex)
                    {
                        _dropTargetIndex = -1;
                    }
                });
                headerContainer.RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (evt.button == 0 && _dragColumnIndex >= 0 && _dropTargetIndex >= 0 && _dragColumnIndex != _dropTargetIndex)
                    {
                        var moved = _columnNames[_dragColumnIndex];
                        _columnNames.RemoveAt(_dragColumnIndex);
                        var insertAt = _dropTargetIndex;
                        if (_dragColumnIndex < _dropTargetIndex)
                        {
                            insertAt--;
                        }
                        _columnNames.Insert(Mathf.Clamp(insertAt, 0, _columnNames.Count), moved);
                        SaveColumnOrder();
                        BuildColumns();
                        RebuildRows();
                    }
                    _dragColumnIndex = -1;
                    _dropTargetIndex = -1;
                    headerContainer.style.borderLeftWidth = 0;
                });

                float dragStartX = 0;
                float dragStartWidth = 0;
                var capturedCol = colName;
                var capturedContainer = headerContainer;

                resizeHandle.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button == 0)
                    {
                        dragStartX = evt.mousePosition.x;
                        dragStartWidth = capturedContainer.resolvedStyle.width;
                        resizeHandle.CaptureMouse();
                        evt.StopPropagation();
                    }
                });
                resizeHandle.RegisterCallback<MouseMoveEvent>(evt =>
                {
                    if (resizeHandle.HasMouseCapture())
                    {
                        var delta = evt.mousePosition.x - dragStartX;
                        var newWidth = Mathf.Max(dragStartWidth + delta, MinColumnWidth);
                        capturedContainer.style.width = newWidth;
                        evt.StopPropagation();
                    }
                });
                resizeHandle.RegisterCallback<MouseUpEvent>(evt =>
                {
                    if (resizeHandle.HasMouseCapture())
                    {
                        var delta = evt.mousePosition.x - dragStartX;
                        var newWidth = Mathf.Max(dragStartWidth + delta, MinColumnWidth);
                        SetColumnWidth(capturedCol, newWidth);
                        resizeHandle.ReleaseMouse();
                        RebuildRows();
                        evt.StopPropagation();
                    }
                });
                headerContainer.Add(resizeHandle);

                _headerRow.Add(headerContainer);
            }
        }

        private async System.Threading.Tasks.Task LoadPageBatchAsync()
        {
            var client = UnitionEditorClient.Client;
            if (client == null || _database == null)
            {
                return;
            }

            _isLoading = true;
            _loadMoreButton.style.display = DisplayStyle.None;
            _loadingContainer.style.display = DisplayStyle.Flex;
            _emptyContainer.style.display = DisplayStyle.None;

            try
            {
                var result = await client.QueryDatabaseAsync(
                    _database.Id,
                    startCursor: _nextCursor,
                    pageSize: 50);

                if (result.Results != null)
                {
                    _pages.AddRange(result.Results);
                }

                _nextCursor = result.HasMore ? result.NextCursor : null;

                _infoLabel.text = $"{_pages.Count} rows{(result.HasMore ? "+" : "")} — {_database.PlainTitle}";

                if (!result.HasMore)
                {
                    NotionCache.SetRows(_database.Id, new List<NotionPage>(_pages));
                }

                RebuildRows();

                _loadMoreButton.style.display = _nextCursor != null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Unition] Failed to query database: {ex.Message}");
                ShowEmpty($"Error: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                _loadingContainer.style.display = DisplayStyle.None;
            }
        }

        private void RebuildRows()
        {
            _scrollView.Clear();
            var filter = _searchField.value?.ToLowerInvariant() ?? "";

            var source = (IEnumerable<NotionPage>)_pages;
            if (!string.IsNullOrEmpty(_sortColumn))
            {
                source = _sortAscending
                    ? source.OrderBy(p => GetSortKey(p, _sortColumn), StringComparer.OrdinalIgnoreCase)
                    : source.OrderByDescending(p => GetSortKey(p, _sortColumn), StringComparer.OrdinalIgnoreCase);
            }

            int shown = 0;
            foreach (var page in source)
            {
                if (!string.IsNullOrEmpty(filter) && !PageMatchesFilter(page, filter))
                {
                    continue;
                }

                var row = CreateRow(page);
                _scrollView.Add(row);
                shown++;
            }

            if (shown == 0 && _pages.Count > 0)
            {
                ShowEmpty("No rows match the search filter.");
            }
            else if (shown == 0)
            {
                ShowEmpty("This database has no rows.");
            }
            else
            {
                _emptyContainer.style.display = DisplayStyle.None;
            }
        }

        private VisualElement CreateRow(NotionPage page)
        {
            var row = new VisualElement();
            row.AddToClassList("unition-table__row");

            if (_selectedPageIds.Contains(page.Id) || page.Id == _selectedPageId)
            {
                row.AddToClassList("unition-table__row--selected");
            }

            foreach (var colName in _columnNames)
            {
                var cell = new VisualElement();
                cell.AddToClassList("unition-table__cell");
                cell.style.width = GetColumnWidth(colName);

                if (page.Properties != null && page.Properties.TryGetValue(colName, out var prop))
                {
                    if (prop.Type == "relation")
                    {
                        var ids = prop.AsRelationIds();
                        if (ids.Count > 0)
                        {
                            var relContainer = new VisualElement();
                            relContainer.style.flexDirection = FlexDirection.Row;
                            relContainer.style.alignItems = Align.Center;

                            var label = new Label($"{ids.Count} relation{(ids.Count > 1 ? "s" : "")}");
                            label.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f, 1f));
                            label.style.flexGrow = 1;
                            relContainer.Add(label);

                            var viewBtn = new Button(() =>
                            {
                                NotionPageViewerWindow.ShowPage(ids[0]);
                            })
                            {
                                text = "→",
                                tooltip = $"Open first relation in Page Viewer ({ids[0].Substring(0, 8)}...)"
                            };
                            viewBtn.style.width = 22;
                            viewBtn.style.height = 18;
                            viewBtn.style.fontSize = 10;
                            viewBtn.style.paddingLeft = 2;
                            viewBtn.style.paddingRight = 2;
                            relContainer.Add(viewBtn);

                            cell.Add(relContainer);
                        }
                        else
                        {
                            cell.Add(new Label("—"));
                        }
                    }
                    else
                    {
                        var rendered = NotionPropertyRenderer.Render(prop);
                        cell.Add(rendered);
                    }
                }
                else
                {
                    cell.Add(new Label("—"));
                }

                row.Add(cell);
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.ctrlKey)
                {
                    if (_selectedPageIds.Contains(page.Id))
                    {
                        _selectedPageIds.Remove(page.Id);
                    }
                    else
                    {
                        _selectedPageIds.Add(page.Id);
                    }
                }
                else
                {
                    _selectedPageIds.Clear();
                    _selectedPageIds.Add(page.Id);
                    _selectedPageId = page.Id;
                    OnPageSelected?.Invoke(page);
                }
                RebuildRows();
            });

            row.RegisterCallback<ContextClickEvent>(evt =>
            {
                if (!_selectedPageIds.Contains(page.Id))
                {
                    _selectedPageIds.Clear();
                    _selectedPageIds.Add(page.Id);
                    _selectedPageId = page.Id;
                    RebuildRows();
                }
                ShowContextMenu(evt);
                evt.StopPropagation();
            });

            return row;
        }

        private bool PageMatchesFilter(NotionPage page, string filter)
        {
            if (page.Properties == null)
            {
                return false;
            }

            foreach (var prop in page.Properties.Values)
            {
                switch (prop.Type)
                {
                    case "title":
                    case "rich_text":
                    case "url":
                    case "email":
                    case "phone_number":
                    {
                        var text = prop.AsPlainText();
                        if (text != null && text.ToLowerInvariant().Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "select":
                    case "status":
                    {
                        var val = prop.Type == "select" ? prop.AsSelect()?.Name : prop.AsStatus();
                        if (val != null && val.ToLowerInvariant().Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "multi_select":
                    {
                        var options = prop.AsMultiSelect();
                        foreach (var opt in options)
                        {
                            if (opt.Name != null && opt.Name.ToLowerInvariant().Contains(filter))
                            {
                                return true;
                            }
                        }
                        break;
                    }
                    case "number":
                    {
                        var num = prop.AsNumber();
                        if (num.HasValue && num.Value.ToString("G").ToLowerInvariant().Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "formula":
                    {
                        var val = prop.AsFormula();
                        if (val != null && val.ToLowerInvariant().Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "date":
                    {
                        var date = prop.AsDate();
                        if (date.HasValue && date.Value.ToString("yyyy-MM-dd").Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "checkbox":
                    {
                        var val = prop.AsCheckbox();
                        var text = val ? "true" : "false";
                        if (text.Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "rollup":
                    {
                        var display = prop.AsRollupDisplay();
                        if (display != null && display.ToLowerInvariant().Contains(filter))
                        {
                            return true;
                        }
                        break;
                    }
                    case "people":
                    {
                        var names = prop.AsPeopleNames();
                        foreach (var name in names)
                        {
                            if (name != null && name.ToLowerInvariant().Contains(filter))
                            {
                                return true;
                            }
                        }
                        break;
                    }
                }
            }
            return false;
        }

        private static string GetSortKey(NotionPage page, string columnName)
        {
            if (page.Properties == null || !page.Properties.TryGetValue(columnName, out var prop))
            {
                return "";
            }

            switch (prop.Type)
            {
                case "title":
                case "rich_text":
                    return prop.AsPlainText() ?? "";
                case "number":
                    var num = prop.AsNumber();
                    return num.HasValue ? num.Value.ToString("F10").PadLeft(20) : "";
                case "checkbox":
                    return prop.AsCheckbox() ? "1" : "0";
                case "select":
                    return prop.AsSelect()?.Name ?? "";
                case "status":
                    return prop.AsStatus() ?? "";
                case "date":
                    var date = prop.AsDate();
                    return date.HasValue ? date.Value.ToString("o") : "";
                case "url":
                    return prop.AsUrl() ?? "";
                case "email":
                    return prop.AsEmail() ?? "";
                case "formula":
                    return prop.AsFormula() ?? "";
                case "rollup":
                    return prop.AsRollupDisplay() ?? "";
                default:
                    return prop.AsPlainText() ?? "";
            }
        }

        private void SaveColumnOrder()
        {
            if (_database == null)
            {
                return;
            }
            var key = $"Unition_ColOrder_{_database.Id}";
            EditorPrefs.SetString(key, string.Join("|", _columnNames));
        }

        private void RestoreColumnOrder()
        {
            _columnNames.Clear();
        }

        private void ApplySavedColumnOrder()
        {
            if (_database == null)
            {
                return;
            }
            var key = $"Unition_ColOrder_{_database.Id}";
            var saved = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(saved))
            {
                return;
            }

            var savedOrder = saved.Split('|').ToList();
            var current = new List<string>(_columnNames);
            var reordered = new List<string>();

            foreach (var col in savedOrder)
            {
                if (current.Contains(col))
                {
                    reordered.Add(col);
                    current.Remove(col);
                }
            }

            foreach (var col in current)
            {
                reordered.Add(col);
            }

            _columnNames.Clear();
            _columnNames.AddRange(reordered);
        }

        private void ShowEmpty(string message)
        {
            _emptyContainer.style.display = DisplayStyle.Flex;
            _emptyContainer.Clear();
            var label = new Label(message);
            label.AddToClassList("unition-empty__text");
            _emptyContainer.Add(label);
        }

        private void OnLoadMoreClicked()
        {
            if (!_isLoading && _nextCursor != null)
            {
                _ = LoadPageBatchAsync();
            }
        }

        private async void OnNewPageClicked()
        {
            if (_database == null)
            {
                return;
            }

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                Debug.LogError("[Unition] No Notion client available.");
                return;
            }

            var titleProp = _database.Properties
                .FirstOrDefault(p => p.Value.Type == "title");

            if (titleProp.Key == null)
            {
                Debug.LogError("[Unition] Database has no title property.");
                return;
            }

            try
            {
                var properties = new JObject
                {
                    [titleProp.Key] = NotionPropertyBuilder.Title("New Page")
                };

                var newPage = await client.CreatePageAsync(_database.Id, properties);

                if (newPage != null)
                {
                    _pages.Insert(0, newPage);
                    _infoLabel.text = $"{_pages.Count} rows — {_database.PlainTitle}";
                    RebuildRows();
                    OnPageSelected?.Invoke(newPage);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Unition] Failed to create page: {ex.Message}");
            }
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (_database == null)
            {
                return;
            }

            var objects = DragAndDrop.objectReferences;
            if (objects != null && objects.Length > 0)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        }

        private async void OnDragPerform(DragPerformEvent evt)
        {
            if (_database == null)
            {
                return;
            }

            var objects = DragAndDrop.objectReferences;
            if (objects == null || objects.Length == 0)
            {
                return;
            }

            DragAndDrop.AcceptDrag();
            evt.StopPropagation();

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                Debug.LogError("[Unition] No Notion client available.");
                return;
            }

            var titleProp = _database.Properties
                .FirstOrDefault(p => p.Value.Type == "title");
            if (titleProp.Key == null)
            {
                Debug.LogError("[Unition] Database has no title property.");
                return;
            }

            foreach (var obj in objects)
            {
                try
                {
                    var properties = new JObject
                    {
                        [titleProp.Key] = NotionPropertyBuilder.Title(obj.name)
                    };

                    var newPage = await client.CreatePageAsync(_database.Id, properties);

                    if (newPage != null)
                    {
                        _pages.Insert(0, newPage);
                        Debug.Log($"[Unition] Created page from dropped asset: {obj.name}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Unition] Failed to create page for '{obj.name}': {ex.Message}");
                }
            }

            _infoLabel.text = $"{_pages.Count} rows — {_database.PlainTitle}";
            RebuildRows();
        }

        private void ShowContextMenu(ContextClickEvent evt)
        {
            var selected = _pages.Where(p => _selectedPageIds.Contains(p.Id)).ToList();
            if (selected.Count == 0)
            {
                return;
            }

            var menu = new GenericMenu();
            var count = selected.Count;

            menu.AddItem(new GUIContent($"Open in Notion ({count})"), false, () =>
            {
                foreach (var page in selected)
                {
                    if (page.Url != null)
                    {
                        Application.OpenURL(page.Url);
                    }
                }
            });

            menu.AddSeparator("");

            menu.AddItem(new GUIContent($"Archive ({count})"), false, () => ArchivePages(selected));

            menu.AddSeparator("");

            menu.AddItem(new GUIContent("Select All"), false, () =>
            {
                _selectedPageIds.Clear();
                foreach (var page in _pages)
                {
                    _selectedPageIds.Add(page.Id);
                }
                RebuildRows();
            });

            menu.AddItem(new GUIContent("Clear Selection"), false, () =>
            {
                _selectedPageIds.Clear();
                RebuildRows();
            });

            menu.ShowAsContext();
        }

        private async void ArchivePages(List<NotionPage> pages)
        {
            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "Archive Pages",
                $"Archive {pages.Count} page(s) in Notion? This can be undone from Notion.",
                "Archive", "Cancel"))
            {
                return;
            }

            int archived = 0;
            foreach (var page in pages)
            {
                try
                {
                    await client.ArchivePageAsync(page.Id);
                    _pages.Remove(page);
                    _selectedPageIds.Remove(page.Id);
                    archived++;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Unition] Failed to archive page '{page.GetTitle()}': {ex.Message}");
                }
            }

            if (archived > 0)
            {
                _infoLabel.text = $"{_pages.Count} rows — {_database.PlainTitle}";
                RebuildRows();
            }
        }

        private void ExportCsv()
        {
            if (_database == null || _pages.Count == 0)
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export CSV", "Assets", _database.PlainTitle ?? "export", "csv");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", _columnNames.Select(EscapeCsv)));

            foreach (var page in _pages)
            {
                var values = new List<string>();
                foreach (var col in _columnNames)
                {
                    if (page.Properties != null && page.Properties.TryGetValue(col, out var prop))
                    {
                        values.Add(EscapeCsv(prop.AsPlainText() ?? prop.AsUrl() ?? prop.AsEmail() ?? ""));
                    }
                    else
                    {
                        values.Add("");
                    }
                }
                sb.AppendLine(string.Join(",", values));
            }

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Debug.Log($"[Unition] Exported {_pages.Count} rows to {path}");
        }

        private void ExportJson()
        {
            if (_database == null || _pages.Count == 0)
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel("Export JSON", "Assets", _database.PlainTitle ?? "export", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var array = new JArray();
            foreach (var page in _pages)
            {
                var obj = new JObject();
                obj["id"] = page.Id;
                if (page.Properties != null)
                {
                    foreach (var col in _columnNames)
                    {
                        if (page.Properties.TryGetValue(col, out var prop))
                        {
                            obj[col] = prop.AsPlainText() ?? prop.AsUrl() ?? prop.AsEmail() ?? "";
                        }
                    }
                }
                array.Add(obj);
            }

            File.WriteAllText(path, array.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
            Debug.Log($"[Unition] Exported {_pages.Count} rows to {path}");
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}
