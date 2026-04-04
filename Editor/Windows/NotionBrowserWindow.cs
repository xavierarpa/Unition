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
using Unition.Editor.UI;
using Unition.Models;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Unition.Editor.Windows
{
    public sealed class NotionBrowserWindow : EditorWindow
    {
        private VisualElement _root;
        private SettingsView _settingsView;
        private VisualElement _browserView;
        private DatabaseListView _dbListView;
        private DatabaseTableView _tableView;
        private PageDetailView _detailView;
        private bool _sidebarVisible = true;
        private string _pendingDatabaseId;

        [MenuItem("Window/Unition/Notion Browser", false, 1000)]
        public static void ShowWindow()
        {
            var window = GetWindow<NotionBrowserWindow>();
            window.titleContent = new GUIContent("Notion Browser", EditorGUIUtility.IconContent("d_Linked").image);
            window.minSize = new Vector2(700, 400);
        }

        public static void ShowDatabase(string databaseId)
        {
            var window = GetWindow<NotionBrowserWindow>();
            window.titleContent = new GUIContent("Notion Browser", EditorGUIUtility.IconContent("d_Linked").image);
            window.minSize = new Vector2(700, 400);
            window._pendingDatabaseId = databaseId;
            if (window._browserView != null && UnitionCredentials.HasToken)
            {
                window.OpenDatabaseById(databaseId);
            }
        }

        private void CreateGUI()
        {
            _root = rootVisualElement;
            _root.AddToClassList("unition-root");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                GetStyleSheetPath());
            if (styleSheet != null)
            {
                _root.styleSheets.Add(styleSheet);
            }

            BuildToolbar();
            BuildSettingsView();
            BuildBrowserView();

            _root.RegisterCallback<KeyDownEvent>(OnKeyDown);

            if (UnitionCredentials.HasToken)
            {
                ShowBrowser();
            }
            else
            {
                ShowSettings();
            }
        }

        private void BuildToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("unition-toolbar");
            _root.Add(toolbar);

            var title = new Label("Unition — Notion Browser");
            title.AddToClassList("unition-toolbar__title");
            toolbar.Add(title);

            var spacer = new VisualElement();
            spacer.AddToClassList("unition-toolbar__spacer");
            toolbar.Add(spacer);

            var settingsBtn = new Button(ShowSettings) { text = "⚙", tooltip = "Open API token settings." };
            settingsBtn.style.width = 28;
            settingsBtn.style.height = 22;
            toolbar.Add(settingsBtn);

            var sidebarBtn = new Button(ToggleSidebar) { text = "☰", tooltip = "Toggle database sidebar" };
            sidebarBtn.style.width = 28;
            sidebarBtn.style.height = 22;
            toolbar.Add(sidebarBtn);
        }

        private void BuildSettingsView()
        {
            _settingsView = new SettingsView();
            _settingsView.OnConnected += () =>
            {
                ShowBrowser();
            };
            _root.Add(_settingsView);
        }

        private void BuildBrowserView()
        {
            _browserView = new VisualElement();
            _browserView.AddToClassList("unition-content");
            _root.Add(_browserView);

            _dbListView = new DatabaseListView();
            _browserView.Add(_dbListView);

            _tableView = new DatabaseTableView();
            _browserView.Add(_tableView);

            _detailView = new PageDetailView();
            _detailView.style.display = DisplayStyle.None;
            _detailView.style.position = Position.Absolute;
            _detailView.style.right = 0;
            _detailView.style.top = 0;
            _detailView.style.bottom = 0;
            _browserView.Add(_detailView);

            _detailView.OnCloseRequested += () =>
            {
                _detailView.ClearPage();
                _detailView.style.display = DisplayStyle.None;
            };

            _detailView.OnPageSaved += () =>
            {
                _tableView.Refresh();
                _detailView.RefreshCurrentPage();
            };

            _dbListView.OnDatabaseSelected += db =>
            {
                _detailView.ClearPage();
                _detailView.style.display = DisplayStyle.None;
                _tableView.LoadDatabase(db);
            };

            _tableView.OnPageSelected += page =>
            {
                _detailView.ShowPage(page);
                _detailView.style.display = DisplayStyle.Flex;
            };
        }

        private void ShowSettings()
        {
            _settingsView.style.display = DisplayStyle.Flex;
            _browserView.style.display = DisplayStyle.None;
        }

        private void ShowBrowser()
        {
            _settingsView.style.display = DisplayStyle.None;
            _browserView.style.display = DisplayStyle.Flex;

            if (_dbListView != null && UnitionCredentials.HasToken)
            {
                _dbListView.LoadDatabases();
            }

            if (!string.IsNullOrEmpty(_pendingDatabaseId))
            {
                OpenDatabaseById(_pendingDatabaseId);
                _pendingDatabaseId = null;
            }
        }

        private async void OpenDatabaseById(string databaseId)
        {
            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                return;
            }

            try
            {
                NotionDatabase db;
                if (!NotionCache.TryGetDatabase(databaseId, out db))
                {
                    db = await client.GetDatabaseAsync(databaseId);
                    NotionCache.SetDatabase(databaseId, db);
                }

                if (db != null)
                {
                    _tableView.LoadDatabase(db);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Unition] Failed to open database: {ex.Message}");
            }
        }

        private void ToggleSidebar()
        {
            _sidebarVisible = !_sidebarVisible;
            _dbListView.style.display = _sidebarVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F5)
            {
                _tableView.Refresh();
                _dbListView.LoadDatabases();
                evt.StopPropagation();
            }
            else if (evt.ctrlKey && evt.keyCode == KeyCode.S)
            {
                _detailView.SaveEdits();
                evt.StopPropagation();
            }
            else if (evt.ctrlKey && evt.keyCode == KeyCode.E)
            {
                _detailView.ToggleEditMode();
                evt.StopPropagation();
            }
        }

        private static string GetStyleSheetPath()
        {
            var guids = AssetDatabase.FindAssets("Unition t:StyleSheet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Unition.uss"))
                {
                    return path;
                }
            }
            return string.Empty;
        }
    }
}
