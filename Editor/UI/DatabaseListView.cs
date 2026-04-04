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

using Unition.Models;

using UnityEngine;
using UnityEngine.UIElements;

namespace Unition.Editor.UI
{
    public sealed class DatabaseListView : VisualElement
    {
        public event Action<NotionDatabase> OnDatabaseSelected;

        private readonly ScrollView _scrollView;
        private readonly TextField _searchField;
        private readonly Button _refreshButton;
        private readonly VisualElement _loadingContainer;

        private List<NotionDatabase> _databases = new List<NotionDatabase>();
        private string _selectedId;
        private bool _isLoading;

        public DatabaseListView()
        {
            AddToClassList("unition-sidebar");

            var header = new VisualElement();
            header.AddToClassList("unition-db-list__header");
            Add(header);

            _searchField = new TextField();
            _searchField.AddToClassList("unition-db-list__search");
            _searchField.RegisterValueChangedCallback(_ => FilterList());
            header.Add(_searchField);

            _refreshButton = new Button(OnRefreshClicked) { text = "↻" };
            _refreshButton.style.width = 28;
            _refreshButton.style.height = 22;
            header.Add(_refreshButton);

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
            var loadingLabel = new Label("Loading databases...");
            loadingLabel.AddToClassList("unition-loading__text");
            _loadingContainer.Add(loadingLabel);
            Add(_loadingContainer);

            _scrollView = new ScrollView(ScrollViewMode.Vertical);
            _scrollView.AddToClassList("unition-db-list__scroll");
            Add(_scrollView);
        }

        public async void LoadDatabases()
        {
            var client = UnitionEditorClient.Client;
            if (client == null || _isLoading)
            {
                return;
            }

            _isLoading = true;
            _refreshButton.SetEnabled(false);
            _loadingContainer.style.display = DisplayStyle.Flex;
            _scrollView.style.display = DisplayStyle.None;

            try
            {
                _databases.Clear();
                var seenIds = new HashSet<string>();

                string cursor = null;
                do
                {
                    var result = await client.SearchAsync(
                        filterObject: "database",
                        startCursor: cursor,
                        pageSize: 100);

                    foreach (var jObj in result.Results)
                    {
                        try
                        {
                            var db = jObj.ToObject<NotionDatabase>();
                            if (db != null && !db.Archived && seenIds.Add(db.Id))
                            {
                                _databases.Add(db);
                            }
                        }
                        catch
                        {
                            // skip malformed entries
                        }
                    }

                    cursor = result.HasMore ? result.NextCursor : null;
                }
                while (cursor != null);

                SortDatabases();
                RebuildList();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Unition] Failed to load databases: {ex.Message}");
                _scrollView.Clear();
                var error = new Label($"Error: {ex.Message}");
                error.AddToClassList("unition-empty__text");
                error.style.color = new StyleColor(new Color(0.96f, 0.28f, 0.28f));
                _scrollView.Add(error);
            }
            finally
            {
                _isLoading = false;
                _loadingContainer.style.display = DisplayStyle.None;
                _scrollView.style.display = DisplayStyle.Flex;
                _refreshButton.SetEnabled(true);
            }
        }

        private void SortDatabases()
        {
            var settings = UnitionSettings.instance;
            _databases.Sort((a, b) =>
            {
                var aFav = settings.IsFavorite(a.Id);
                var bFav = settings.IsFavorite(b.Id);
                if (aFav != bFav)
                {
                    return aFav ? -1 : 1;
                }
                return string.Compare(a.PlainTitle, b.PlainTitle, StringComparison.OrdinalIgnoreCase);
            });
        }

        private void RebuildList()
        {
            _scrollView.Clear();
            var filter = _searchField.value?.ToLowerInvariant() ?? "";

            foreach (var db in _databases)
            {
                var title = db.PlainTitle ?? "(Untitled)";
                if (!string.IsNullOrEmpty(filter) &&
                    title.ToLowerInvariant().IndexOf(filter, StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                var item = CreateDatabaseItem(db, title);
                _scrollView.Add(item);
            }

            if (_scrollView.childCount == 0)
            {
                var empty = new VisualElement();
                empty.AddToClassList("unition-empty");
                var emptyLabel = new Label(
                    _databases.Count == 0
                        ? "No databases found.\nMake sure databases are shared with your integration."
                        : "No databases match the filter.");
                emptyLabel.AddToClassList("unition-empty__text");
                empty.Add(emptyLabel);
                _scrollView.Add(empty);
            }
        }

        private VisualElement CreateDatabaseItem(NotionDatabase db, string title)
        {
            var item = new VisualElement();
            item.AddToClassList("unition-db-item");

            if (db.Id == _selectedId)
            {
                item.AddToClassList("unition-db-item--selected");
            }

            var icon = new Label(db.Icon?.DisplayValue ?? "📄");
            icon.AddToClassList("unition-db-item__icon");
            item.Add(icon);

            var info = new VisualElement();
            info.AddToClassList("unition-db-item__info");
            item.Add(info);

            var nameLabel = new Label(title);
            nameLabel.AddToClassList("unition-db-item__name");
            info.Add(nameLabel);

            var propCount = db.Properties?.Count ?? 0;
            var meta = new Label($"{propCount} properties");
            meta.AddToClassList("unition-db-item__meta");
            info.Add(meta);

            var isFav = UnitionSettings.instance.IsFavorite(db.Id);
            var favBtn = new Button(() =>
            {
                UnitionSettings.instance.ToggleFavorite(db.Id);
                SortDatabases();
                RebuildList();
            })
            {
                text = isFav ? "★" : "☆"
            };
            favBtn.AddToClassList("unition-db-item__fav");
            if (isFav)
            {
                favBtn.AddToClassList("unition-db-item__fav--active");
            }
            item.Add(favBtn);

            item.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button)
                {
                    return;
                }
                _selectedId = db.Id;
                RebuildList();
                OnDatabaseSelected?.Invoke(db);
            });

            return item;
        }

        private void FilterList()
        {
            RebuildList();
        }

        private void OnRefreshClicked()
        {
            LoadDatabases();
        }
    }
}
