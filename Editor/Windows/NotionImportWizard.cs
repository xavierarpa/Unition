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
using System.Reflection;
using System.Threading;

using Newtonsoft.Json.Linq;

using Unition.Attributes;
using Unition.Editor.Sync;
using Unition.Models;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Unition.Editor.Windows
{
    public sealed class NotionImportWizard : EditorWindow
    {
        private enum WizardStep
        {
            SelectDatabase = 0,
            SelectTargetType = 1,
            MapProperties = 2,
            Configure = 3
        }

        private VisualElement _root;
        private VisualElement _stepContent;
        private Label _stepLabel;
        private Button _backBtn;
        private Button _nextBtn;

        private WizardStep _currentStep;
        private CancellationTokenSource _cts;

        // Step 1 data
        private List<NotionDatabase> _databases;
        private NotionDatabase _selectedDatabase;
        private string _searchFilter = string.Empty;

        // Step 2 data
        private List<Type> _availableTypes;
        private Type _selectedType;
        private NotionOutputFormat _outputFormat = NotionOutputFormat.ScriptableObject;

        // Step 3 data
        private List<NotionPropertyMapping> _mappings = new List<NotionPropertyMapping>();
        private List<FieldCreationEntry> _fieldsToCreate = new List<FieldCreationEntry>();

        private sealed class FieldCreationEntry
        {
            public bool Selected;
            public FieldInfo Field;
            public string NotionName;
            public string NotionType;
        }

        // Step 4 data
        private string _outputPath = "Assets/Data/";
        private string _filesDownloadPath = "Assets/Notion/Downloads/";
        private string _profileName = "NewSyncProfile";
        private NotionSyncMode _syncMode = NotionSyncMode.Manual;
        private string _namingProperty = "Name";

        [MenuItem("Window/Unition/Notion Import Wizard", false, 1002)]
        public static void ShowWizard()
        {
            var window = GetWindow<NotionImportWizard>();
            window.titleContent = new GUIContent("Notion Import Wizard", EditorGUIUtility.IconContent("d_Import").image);
            window.minSize = new Vector2(550, 450);
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private static UnityEngine.Object LoadFolderAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var trimmed = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(trimmed))
            {
                return AssetDatabase.LoadAssetAtPath<DefaultAsset>(trimmed);
            }

            return null;
        }

        private void CreateGUI()
        {
            _root = rootVisualElement;
            _root.AddToClassList("unition-root");

            LoadStyleSheet();
            BuildLayout();
            SetStep(WizardStep.SelectDatabase);
        }

        private void LoadStyleSheet()
        {
            var guids = AssetDatabase.FindAssets("Unition t:StyleSheet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("Unition.uss"))
                {
                    var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    if (sheet != null)
                    {
                        _root.styleSheets.Add(sheet);
                    }
                    break;
                }
            }
        }

        private void BuildLayout()
        {
            var toolbar = new VisualElement();
            toolbar.AddToClassList("unition-toolbar");
            _root.Add(toolbar);

            _stepLabel = new Label();
            _stepLabel.AddToClassList("unition-toolbar__title");
            toolbar.Add(_stepLabel);

            _stepContent = new VisualElement();
            _stepContent.style.flexGrow = 1;
            _stepContent.style.paddingLeft = 12;
            _stepContent.style.paddingRight = 12;
            _stepContent.style.paddingTop = 8;
            _root.Add(_stepContent);

            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.paddingLeft = 12;
            footer.style.paddingRight = 12;
            footer.style.paddingBottom = 8;
            footer.style.paddingTop = 4;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            _root.Add(footer);

            _backBtn = new Button(OnBack) { text = "← Back" };
            _backBtn.style.width = 80;
            footer.Add(_backBtn);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            footer.Add(spacer);

            _nextBtn = new Button(OnNext) { text = "Next →" };
            _nextBtn.AddToClassList("unition-btn-primary");
            _nextBtn.style.width = 120;
            footer.Add(_nextBtn);
        }

        private void SetStep(WizardStep step)
        {
            _currentStep = step;
            _stepContent.Clear();

            _backBtn.SetEnabled(step != WizardStep.SelectDatabase);

            switch (step)
            {
                case WizardStep.SelectDatabase:
                    _stepLabel.text = "Step 1/4 — Select Database";
                    _nextBtn.text = "Next →";
                    BuildStepSelectDatabase();
                    break;
                case WizardStep.SelectTargetType:
                    _stepLabel.text = "Step 2/4 — Select Target Type";
                    _nextBtn.text = "Next →";
                    BuildStepSelectType();
                    break;
                case WizardStep.MapProperties:
                    _stepLabel.text = "Step 3/4 — Map Properties";
                    _nextBtn.text = "Next →";
                    BuildStepMapProperties();
                    break;
                case WizardStep.Configure:
                    _stepLabel.text = "Step 4/4 — Configure Output";
                    _nextBtn.text = "Create Profile ✓";
                    BuildStepConfigure();
                    break;
            }
        }

        private void OnBack()
        {
            if (_currentStep > WizardStep.SelectDatabase)
            {
                SetStep(_currentStep - 1);
            }
        }

        private void OnNext()
        {
            switch (_currentStep)
            {
                case WizardStep.SelectDatabase:
                    if (_selectedDatabase == null)
                    {
                        EditorUtility.DisplayDialog("Unition", "Please select a database.", "OK");
                        return;
                    }
                    SetStep(WizardStep.SelectTargetType);
                    break;

                case WizardStep.SelectTargetType:
                    if (_selectedType == null && _outputFormat == NotionOutputFormat.ScriptableObject)
                    {
                        EditorUtility.DisplayDialog("Unition", "Please select a target type.", "OK");
                        return;
                    }
                    GenerateAutoMappings();
                    SetStep(WizardStep.MapProperties);
                    break;

                case WizardStep.MapProperties:
                    SetStep(WizardStep.Configure);
                    break;

                case WizardStep.Configure:
                    CreateProfile();
                    break;
            }
        }

        // ── Step 1: Select Database ─────────────────────────────────────

        private void BuildStepSelectDatabase()
        {
            if (!UnitionCredentials.HasToken)
            {
                var notice = new Label("No API token configured. Set it in Window > Unition > Notion Browser first.");
                notice.AddToClassList("unition-empty");
                _stepContent.Add(notice);
                return;
            }

            var searchField = new TextField("Search");
            searchField.value = _searchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue;
                FilterDatabaseList();
            });
            _stepContent.Add(searchField);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "db-scroll";
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 4;
            _stepContent.Add(scroll);

            if (_databases == null)
            {
                LoadDatabases(scroll);
            }
            else
            {
                PopulateDatabaseList(scroll);
            }
        }

        private async void LoadDatabases(ScrollView scroll)
        {
            var loading = new Label("Loading databases...");
            loading.AddToClassList("unition-loading");
            scroll.Add(loading);

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                var client = UnitionEditorClient.Client;
                _databases = new List<NotionDatabase>();

                string cursor = null;
                do
                {
                    var result = await client.SearchAsync(filterObject: "database", startCursor: cursor, pageSize: 100, ct: _cts.Token);
                    foreach (var item in result.Results)
                    {
                        var db = item.ToObject<NotionDatabase>(Newtonsoft.Json.JsonSerializer.Create(Unition.Serialization.NotionJsonSettings.Create()));
                        if (db != null && !db.Archived)
                        {
                            _databases.Add(db);
                        }
                    }
                    cursor = result.HasMore ? result.NextCursor : null;
                }
                while (cursor != null);

                scroll.Clear();
                PopulateDatabaseList(scroll);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                scroll.Clear();
                var error = new Label($"Error: {ex.Message}");
                error.style.color = new Color(1f, 0.3f, 0.3f, 1f);
                scroll.Add(error);
            }
        }

        private void PopulateDatabaseList(ScrollView scroll)
        {
            scroll.Clear();

            var dbs = _databases;
            if (!string.IsNullOrEmpty(_searchFilter))
            {
                dbs = dbs.Where(d => d.PlainTitle != null &&
                    d.PlainTitle.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            if (dbs.Count == 0)
            {
                scroll.Add(new Label("No databases found."));
                return;
            }

            foreach (var db in dbs)
            {
                var row = new Button(() => SelectDatabase(db));
                row.AddToClassList("unition-db-item");
                row.text = $"{db.PlainTitle ?? db.Id}";
                if (_selectedDatabase != null && _selectedDatabase.Id == db.Id)
                {
                    row.AddToClassList("unition-db-item--selected");
                }
                scroll.Add(row);
            }
        }

        private void SelectDatabase(NotionDatabase db)
        {
            _selectedDatabase = db;
            var scroll = _stepContent.Q<ScrollView>("db-scroll");
            if (scroll != null)
            {
                PopulateDatabaseList(scroll);
            }
        }

        private void FilterDatabaseList()
        {
            var scroll = _stepContent.Q<ScrollView>("db-scroll");
            if (scroll != null && _databases != null)
            {
                PopulateDatabaseList(scroll);
            }
        }

        // ── Step 2: Select Target Type ──────────────────────────────────

        private void BuildStepSelectType()
        {
            var formatField = new EnumField("Output Format", _outputFormat);
            formatField.RegisterValueChangedCallback(evt =>
            {
                _outputFormat = (NotionOutputFormat)evt.newValue;
                SetStep(WizardStep.SelectTargetType);
            });
            _stepContent.Add(formatField);

            if (_outputFormat == NotionOutputFormat.JSON)
            {
                var info = new Label("JSON output does not require a target type. Property mappings will define the JSON structure.");
                info.style.whiteSpace = WhiteSpace.Normal;
                info.style.marginTop = 8;
                info.style.color = new Color(0.6f, 0.8f, 0.6f, 1f);
                _stepContent.Add(info);
                return;
            }

            var label = new Label("Select a ScriptableObject type:");
            label.style.marginTop = 8;
            label.style.marginBottom = 4;
            _stepContent.Add(label);

            _availableTypes = FindScriptableObjectTypes();

            var searchField = new TextField("Filter");
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.name = "type-scroll";
            scroll.style.flexGrow = 1;
            scroll.style.marginTop = 4;

            searchField.RegisterValueChangedCallback(evt => PopulateTypeList(scroll, evt.newValue));
            _stepContent.Add(searchField);
            _stepContent.Add(scroll);

            PopulateTypeList(scroll, string.Empty);
        }

        private void PopulateTypeList(ScrollView scroll, string filter)
        {
            scroll.Clear();
            var types = _availableTypes;
            if (!string.IsNullOrEmpty(filter))
            {
                types = types.Where(t => t.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.FullName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            foreach (var type in types)
            {
                var row = new Button(() =>
                {
                    _selectedType = type;
                    PopulateTypeList(scroll, filter);
                });
                row.AddToClassList("unition-db-item");
                row.text = type.FullName;
                if (_selectedType == type)
                {
                    row.AddToClassList("unition-db-item--selected");
                }
                scroll.Add(row);
            }
        }

        private static List<Type> FindScriptableObjectTypes()
        {
            var types = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    continue;
                }

                var asmName = assembly.GetName().Name;
                if (IsExcludedAssembly(asmName))
                {
                    continue;
                }

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.IsAbstract || type.IsGenericType)
                        {
                            continue;
                        }
                        if (!typeof(ScriptableObject).IsAssignableFrom(type) || type == typeof(ScriptableObject))
                        {
                            continue;
                        }
                        if (type.Namespace != null && IsExcludedNamespace(type.Namespace))
                        {
                            continue;
                        }
                        types.Add(type);
                    }
                }
                catch (ReflectionTypeLoadException) { }
            }
            types.Sort((a, b) => string.Compare(a.FullName, b.FullName, StringComparison.Ordinal));
            return types;
        }

        private static bool IsExcludedAssembly(string name)
        {
            if (name.StartsWith("Unity.", StringComparison.Ordinal))
            {
                return true;
            }
            if (name.StartsWith("UnityEngine.", StringComparison.Ordinal))
            {
                return true;
            }
            if (name.StartsWith("UnityEditor.", StringComparison.Ordinal))
            {
                return true;
            }
            if (name == "UnityEngine" || name == "UnityEditor")
            {
                return true;
            }
            if (name.StartsWith("System", StringComparison.Ordinal))
            {
                return true;
            }
            if (name.StartsWith("Mono.", StringComparison.Ordinal))
            {
                return true;
            }
            if (name.StartsWith("mscorlib", StringComparison.Ordinal))
            {
                return true;
            }
            if (name.StartsWith("netstandard", StringComparison.Ordinal))
            {
                return true;
            }
            if (name.StartsWith("nunit", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        private static bool IsExcludedNamespace(string ns)
        {
            if (ns.StartsWith("UnityEngine.", StringComparison.Ordinal))
            {
                return true;
            }
            if (ns.StartsWith("UnityEditor.", StringComparison.Ordinal))
            {
                return true;
            }
            if (ns == "UnityEngine" || ns == "UnityEditor")
            {
                return true;
            }
            return false;
        }

        // ── Step 3: Map Properties ──────────────────────────────────────

        private void BuildStepMapProperties()
        {
            var info = new Label($"Mapping: {_selectedDatabase?.PlainTitle ?? "?"} → {(_selectedType?.Name ?? "JSON")}");
            info.style.unityFontStyleAndWeight = FontStyle.Bold;
            info.style.marginBottom = 8;
            _stepContent.Add(info);

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.marginBottom = 4;
            _stepContent.Add(headerRow);

            AddHeaderCell(headerRow, "✓", 24);
            AddHeaderCell(headerRow, "Notion Property", 0, 1);
            AddHeaderCell(headerRow, "Type", 80);
            AddHeaderCell(headerRow, "→", 20);
            AddHeaderCell(headerRow, "Target Field", 0, 1);
            AddHeaderCell(headerRow, "Field Type", 80);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            _stepContent.Add(scroll);

            var targetFields = GetTargetFields();

            for (int i = 0; i < _mappings.Count; i++)
            {
                scroll.Add(BuildMappingRow(_mappings[i], targetFields));
            }

            if (_selectedType != null)
            {
                BuildFieldCreationSection(scroll, targetFields);
            }
        }

        private VisualElement BuildMappingRow(NotionPropertyMapping mapping, List<string> targetFields)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.minHeight = 22;

            var toggle = new Toggle();
            toggle.value = mapping.Enabled;
            toggle.style.width = 20;
            toggle.RegisterValueChangedCallback(evt => mapping.Enabled = evt.newValue);
            row.Add(toggle);

            var notionName = new Label(mapping.NotionPropertyName);
            notionName.style.flexGrow = 1;
            notionName.style.overflow = Overflow.Hidden;
            row.Add(notionName);

            var notionType = new Label(mapping.NotionPropertyType);
            notionType.style.width = 80;
            notionType.style.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            notionType.style.fontSize = 11;
            row.Add(notionType);

            var arrow = new Label("→");
            arrow.style.width = 20;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(arrow);

            if (targetFields.Count > 0)
            {
                var fieldChoices = new List<string>(targetFields);
                fieldChoices.Insert(0, "(skip)");
                var fieldIndex = fieldChoices.IndexOf(mapping.TargetFieldName);
                if (fieldIndex < 0)
                {
                    fieldIndex = 0;
                }

                var dropdown = new PopupField<string>(fieldChoices, fieldIndex);
                dropdown.style.flexGrow = 1;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    mapping.TargetFieldName = evt.newValue == "(skip)" ? string.Empty : evt.newValue;
                    mapping.Enabled = evt.newValue != "(skip)";
                });
                row.Add(dropdown);
            }
            else
            {
                var fieldName = new TextField();
                fieldName.value = mapping.TargetFieldName;
                fieldName.style.flexGrow = 1;
                fieldName.RegisterValueChangedCallback(evt => mapping.TargetFieldName = evt.newValue);
                row.Add(fieldName);
            }

            var fieldType = new Label(mapping.TargetFieldType);
            fieldType.style.width = 80;
            fieldType.style.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            fieldType.style.fontSize = 11;
            row.Add(fieldType);

            return row;
        }

        private void GenerateAutoMappings()
        {
            _mappings.Clear();

            if (_selectedDatabase?.Properties == null)
            {
                return;
            }

            var targetFields = GetTargetFieldMap();

            foreach (var kvp in _selectedDatabase.Properties)
            {
                var schema = kvp.Value;
                var mapping = new NotionPropertyMapping
                {
                    NotionPropertyName = kvp.Key,
                    NotionPropertyType = schema.Type,
                    Enabled = true
                };

                var matchedField = FindBestFieldMatch(kvp.Key, targetFields);
                if (matchedField != null)
                {
                    if (schema.Type != "relation" && IsScriptableObjectFieldType(matchedField.FieldType))
                    {
                        mapping.TargetFieldName = string.Empty;
                        mapping.TargetFieldType = SuggestFieldType(schema.Type);
                        mapping.Enabled = false;
                    }
                    else
                    {
                        mapping.TargetFieldName = matchedField.Name;
                        mapping.TargetFieldType = matchedField.FieldType.Name;
                    }
                }
                else
                {
                    mapping.TargetFieldName = string.Empty;
                    mapping.TargetFieldType = SuggestFieldType(schema.Type);
                    mapping.Enabled = false;
                }

                _mappings.Add(mapping);
            }
        }

        private FieldInfo FindBestFieldMatch(string notionName, Dictionary<string, FieldInfo> fields)
        {
            // Check [NotionProperty] attributes first
            foreach (var kvp in fields)
            {
                var attr = kvp.Value.GetCustomAttribute<NotionPropertyAttribute>();
                if (attr != null && string.Equals(attr.PropertyName, notionName, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            // Exact name match (case-insensitive)
            var normalized = notionName.Replace(" ", "").Replace("_", "").Replace("-", "").ToLowerInvariant();
            foreach (var kvp in fields)
            {
                var fieldNormalized = kvp.Key.Replace("_", "").ToLowerInvariant();
                if (fieldNormalized == normalized)
                {
                    return kvp.Value;
                }
            }

            // Partial match
            foreach (var kvp in fields)
            {
                var fieldNormalized = kvp.Key.Replace("_", "").ToLowerInvariant();
                if (fieldNormalized.Contains(normalized) || normalized.Contains(fieldNormalized))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        private Dictionary<string, FieldInfo> GetTargetFieldMap()
        {
            var result = new Dictionary<string, FieldInfo>();
            if (_selectedType == null)
            {
                return result;
            }

            var fields = _selectedType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.DeclaringType == typeof(ScriptableObject) ||
                    field.DeclaringType == typeof(UnityEngine.Object) ||
                    field.DeclaringType == typeof(MonoBehaviour))
                {
                    continue;
                }
                result[field.Name] = field;
            }
            return result;
        }

        private List<string> GetTargetFields()
        {
            if (_selectedType == null)
            {
                return new List<string>();
            }

            return GetTargetFieldMap().Keys.ToList();
        }

        private static string SuggestFieldType(string notionType)
        {
            switch (notionType)
            {
                case "title":
                case "rich_text":
                case "url":
                case "email":
                case "phone_number":
                    return "string";
                case "number":
                    return "float";
                case "checkbox":
                    return "bool";
                case "select":
                case "status":
                    return "string";
                case "multi_select":
                    return "string[]";
                case "date":
                    return "string";
                case "relation":
                    return "string";
                case "formula":
                    return "string";
                default:
                    return "string";
            }
        }

        private static void AddHeaderCell(VisualElement parent, string text, float width, float flex = 0)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            if (width > 0)
            {
                label.style.width = width;
            }
            if (flex > 0)
            {
                label.style.flexGrow = flex;
            }
            parent.Add(label);
        }

        // ── Create Notion Properties from C# Fields ─────────────────────

        private void BuildFieldCreationSection(ScrollView scroll, List<string> targetFields)
        {
            var mappedFields = new HashSet<string>();
            foreach (var m in _mappings)
            {
                if (!string.IsNullOrEmpty(m.TargetFieldName))
                {
                    mappedFields.Add(m.TargetFieldName);
                }
            }

            var unmappedFields = new List<FieldInfo>();
            var fieldMap = GetTargetFieldMap();
            foreach (var kvp in fieldMap)
            {
                if (!mappedFields.Contains(kvp.Key))
                {
                    unmappedFields.Add(kvp.Value);
                }
            }

            if (unmappedFields.Count == 0)
            {
                return;
            }

            _fieldsToCreate.Clear();
            foreach (var field in unmappedFields)
            {
                _fieldsToCreate.Add(new FieldCreationEntry
                {
                    Selected = false,
                    Field = field,
                    NotionName = FormatFieldAsNotionName(field.Name),
                    NotionType = CSharpTypeToNotionType(field.FieldType)
                });
            }

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            separator.style.marginTop = 8;
            separator.style.marginBottom = 4;
            scroll.Add(separator);

            var sectionLabel = new Label("Create Notion properties from C# fields:");
            sectionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionLabel.style.marginBottom = 4;
            sectionLabel.style.color = new Color(0.6f, 0.8f, 0.6f, 1f);
            scroll.Add(sectionLabel);

            var createHeaderRow = new VisualElement();
            createHeaderRow.style.flexDirection = FlexDirection.Row;
            createHeaderRow.style.marginBottom = 2;
            scroll.Add(createHeaderRow);

            AddHeaderCell(createHeaderRow, "✓", 24);
            AddHeaderCell(createHeaderRow, "C# Field", 0, 1);
            AddHeaderCell(createHeaderRow, "C# Type", 80);
            AddHeaderCell(createHeaderRow, "→", 20);
            AddHeaderCell(createHeaderRow, "Notion Name", 0, 1);
            AddHeaderCell(createHeaderRow, "Notion Type", 80);

            foreach (var entry in _fieldsToCreate)
            {
                scroll.Add(BuildFieldCreationRow(entry));
            }

            var createBtn = new Button(() => CreateSelectedPropertiesInNotion())
            {
                text = "Create Selected in Notion",
                tooltip = "Creates the selected properties in the Notion database, then refreshes mappings."
            };
            createBtn.AddToClassList("unition-btn-primary");
            createBtn.style.marginTop = 8;
            createBtn.style.alignSelf = Align.FlexEnd;
            scroll.Add(createBtn);
        }

        private VisualElement BuildFieldCreationRow(FieldCreationEntry entry)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 2;
            row.style.minHeight = 22;

            var toggle = new Toggle();
            toggle.value = entry.Selected;
            toggle.style.width = 20;
            toggle.RegisterValueChangedCallback(evt => entry.Selected = evt.newValue);
            row.Add(toggle);

            var fieldLabel = new Label(entry.Field.Name);
            fieldLabel.style.flexGrow = 1;
            fieldLabel.style.overflow = Overflow.Hidden;
            row.Add(fieldLabel);

            var typeLabel = new Label(entry.Field.FieldType.Name);
            typeLabel.style.width = 80;
            typeLabel.style.color = new Color(0.6f, 0.7f, 0.8f, 1f);
            typeLabel.style.fontSize = 11;
            row.Add(typeLabel);

            var arrow = new Label("→");
            arrow.style.width = 20;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(arrow);

            var nameField = new TextField();
            nameField.value = entry.NotionName;
            nameField.style.flexGrow = 1;
            nameField.RegisterValueChangedCallback(evt => entry.NotionName = evt.newValue);
            row.Add(nameField);

            var notionTypeChoices = new List<string>
            {
                "rich_text", "number", "checkbox", "select", "multi_select",
                "url", "email", "phone_number", "date", "relation"
            };
            var typeIndex = notionTypeChoices.IndexOf(entry.NotionType);
            if (typeIndex < 0)
            {
                typeIndex = 0;
            }
            var typeDropdown = new PopupField<string>(notionTypeChoices, typeIndex);
            typeDropdown.style.width = 100;
            typeDropdown.RegisterValueChangedCallback(evt => entry.NotionType = evt.newValue);
            row.Add(typeDropdown);

            return row;
        }

        private async void CreateSelectedPropertiesInNotion()
        {
            var selected = _fieldsToCreate.FindAll(e => e.Selected);
            if (selected.Count == 0)
            {
                EditorUtility.DisplayDialog("Unition", "Select at least one field to create.", "OK");
                return;
            }

            var client = UnitionEditorClient.Client;
            if (client == null)
            {
                EditorUtility.DisplayDialog("Unition", "No Notion client available.", "OK");
                return;
            }

            var properties = new JObject();
            foreach (var entry in selected)
            {
                if (entry.NotionType == "relation")
                {
                    var targetDbId = FindRelationDatabaseId(entry.Field.FieldType);
                    properties[entry.NotionName] = new JObject
                    {
                        ["relation"] = new JObject
                        {
                            ["database_id"] = targetDbId,
                            ["single_property"] = new JObject()
                        }
                    };
                }
                else
                {
                    properties[entry.NotionName] = new JObject
                    {
                        [entry.NotionType] = new JObject()
                    };
                }
            }

            EditorUtility.DisplayProgressBar("Unition", "Creating properties in Notion...", 0.5f);

            try
            {
                var updatedDb = await client.UpdateDatabaseAsync(_selectedDatabase.Id, properties);
                _selectedDatabase = updatedDb;

                NotionCache.InvalidateDatabase(_selectedDatabase.Id);

                GenerateAutoMappings();
                SetStep(WizardStep.MapProperties);

                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Unition",
                    $"Created {selected.Count} properties in Notion.\nMappings have been refreshed.", "OK");
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Unition", $"Failed to create properties: {ex.Message}", "OK");
            }
        }

        private static string CSharpTypeToNotionType(Type type)
        {
            if (type == typeof(string))
            {
                return "rich_text";
            }
            if (type == typeof(int) || type == typeof(float) || type == typeof(double) || type == typeof(long))
            {
                return "number";
            }
            if (type == typeof(bool))
            {
                return "checkbox";
            }
            if (type.IsEnum)
            {
                return "select";
            }
            if (type.IsArray && type.GetElementType() == typeof(string))
            {
                return "multi_select";
            }
            if (type == typeof(List<string>))
            {
                return "multi_select";
            }
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return "relation";
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                if (typeof(ScriptableObject).IsAssignableFrom(elementType))
                {
                    return "relation";
                }
            }
            if (type.IsArray && typeof(ScriptableObject).IsAssignableFrom(type.GetElementType()))
            {
                return "relation";
            }
            return "rich_text";
        }

        private string FindRelationDatabaseId(Type fieldType)
        {
            var elementType = fieldType;
            if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = fieldType.GetGenericArguments()[0];
            }
            else if (fieldType.IsArray)
            {
                elementType = fieldType.GetElementType();
            }

            if (elementType == _selectedType)
            {
                return _selectedDatabase.Id;
            }

            var profileGuids = AssetDatabase.FindAssets("t:NotionSyncProfile");
            foreach (var guid in profileGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var profile = AssetDatabase.LoadAssetAtPath<NotionSyncProfile>(path);
                if (profile != null && profile.TargetTypeName == elementType.FullName)
                {
                    return profile.DatabaseId;
                }
            }

            return _selectedDatabase.Id;
        }

        private static bool IsScriptableObjectFieldType(Type type)
        {
            if (typeof(ScriptableObject).IsAssignableFrom(type))
            {
                return true;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return typeof(ScriptableObject).IsAssignableFrom(type.GetGenericArguments()[0]);
            }
            if (type.IsArray)
            {
                return typeof(ScriptableObject).IsAssignableFrom(type.GetElementType());
            }
            return false;
        }

        private static string FormatFieldAsNotionName(string fieldName)
        {
            if (fieldName.StartsWith("<") && fieldName.Contains(">k__BackingField"))
            {
                fieldName = fieldName.Substring(1, fieldName.IndexOf('>') - 1);
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < fieldName.Length; i++)
            {
                var c = fieldName[i];
                if (c == '_')
                {
                    continue;
                }
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(fieldName[i - 1]))
                {
                    sb.Append(' ');
                }
                sb.Append(i == 0 || sb.Length == 0 ? char.ToUpper(c) : c);
            }
            return sb.ToString();
        }

        // ── Step 4: Configure ───────────────────────────────────────────

        private void BuildStepConfigure()
        {
            var nameField = new TextField("Profile Name");
            nameField.value = _profileName;
            nameField.tooltip = "Name for the Sync Profile asset. Used as the file name in the project.";
            nameField.RegisterValueChangedCallback(evt => _profileName = evt.newValue);
            _stepContent.Add(nameField);

            var pathRow = new VisualElement();
            pathRow.style.flexDirection = FlexDirection.Row;
            pathRow.style.alignItems = Align.Center;
            pathRow.tooltip = "Folder where synced assets (ScriptableObjects or JSON) will be saved.";

            var pathLabel = new Label("Output Path");
            pathLabel.style.width = 120;
            pathRow.Add(pathLabel);

            var outputFolder = LoadFolderAsset(_outputPath);
            var pathObjField = new UnityEditor.UIElements.ObjectField();
            pathObjField.objectType = typeof(DefaultAsset);
            pathObjField.value = outputFolder;
            pathObjField.style.flexGrow = 1;
            pathObjField.RegisterValueChangedCallback(evt =>
            {
                var path = AssetDatabase.GetAssetPath(evt.newValue);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    _outputPath = path + "/";
                }
                else if (evt.newValue == null)
                {
                    _outputPath = "Assets/Data/";
                }
                else
                {
                    pathObjField.SetValueWithoutNotify(evt.previousValue);
                }
            });
            pathRow.Add(pathObjField);
            _stepContent.Add(pathRow);

            var filesRow = new VisualElement();
            filesRow.style.flexDirection = FlexDirection.Row;
            filesRow.style.alignItems = Align.Center;
            filesRow.tooltip = "Folder where attached files (images, PDFs, etc.) from Notion 'files' properties are downloaded.";

            var filesLabel = new Label("Files Path");
            filesLabel.style.width = 120;
            filesRow.Add(filesLabel);

            var filesFolder = LoadFolderAsset(_filesDownloadPath);
            var filesObjField = new UnityEditor.UIElements.ObjectField();
            filesObjField.objectType = typeof(DefaultAsset);
            filesObjField.value = filesFolder;
            filesObjField.style.flexGrow = 1;
            filesObjField.RegisterValueChangedCallback(evt =>
            {
                var path = AssetDatabase.GetAssetPath(evt.newValue);
                if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
                {
                    _filesDownloadPath = path + "/";
                }
                else if (evt.newValue == null)
                {
                    _filesDownloadPath = "Assets/Notion/Downloads/";
                }
                else
                {
                    filesObjField.SetValueWithoutNotify(evt.previousValue);
                }
            });
            filesRow.Add(filesObjField);
            _stepContent.Add(filesRow);

            var namingField = new TextField("Naming Property");
            namingField.value = _namingProperty;
            namingField.tooltip = "The Notion property to use as the asset file name.";
            namingField.RegisterValueChangedCallback(evt => _namingProperty = evt.newValue);
            _stepContent.Add(namingField);

            var modeField = new EnumField("Sync Mode", _syncMode);
            modeField.tooltip = "Manual: sync only when you click Sync. OnEditorFocus: sync when Unity regains focus. OnPlay: sync before entering Play mode.";
            modeField.RegisterValueChangedCallback(evt => _syncMode = (NotionSyncMode)evt.newValue);
            _stepContent.Add(modeField);

            var summaryCard = new VisualElement();
            summaryCard.AddToClassList("unition-settings-card");
            summaryCard.style.marginTop = 12;
            _stepContent.Add(summaryCard);

            var summaryTitle = new Label("Summary");
            summaryTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            summaryTitle.style.marginBottom = 4;
            summaryCard.Add(summaryTitle);

            var enabledCount = _mappings.Count(m => m.Enabled);
            summaryCard.Add(new Label($"Database: {_selectedDatabase?.PlainTitle ?? "?"}"));
            summaryCard.Add(new Label($"Target: {(_selectedType?.FullName ?? "JSON")}"));
            summaryCard.Add(new Label($"Format: {_outputFormat}"));
            summaryCard.Add(new Label($"Mappings: {enabledCount} of {_mappings.Count} enabled"));
            summaryCard.Add(new Label($"Sync Mode: {_syncMode}"));
        }

        // ── Create Profile ──────────────────────────────────────────────

        private void CreateProfile()
        {
            if (string.IsNullOrEmpty(_profileName))
            {
                EditorUtility.DisplayDialog("Unition", "Please enter a profile name.", "OK");
                return;
            }

            var profile = ScriptableObject.CreateInstance<NotionSyncProfile>();
            profile.DatabaseId = _selectedDatabase.Id;
            profile.DatabaseName = _selectedDatabase.PlainTitle;
            profile.OutputFormat = _outputFormat;
            profile.OutputPath = _outputPath;
            profile.FilesDownloadPath = _filesDownloadPath;
            profile.SyncMode = _syncMode;
            profile.NamingProperty = _namingProperty;

            if (_selectedType != null)
            {
                profile.TargetTypeName = _selectedType.FullName;
                profile.TargetAssemblyName = _selectedType.Assembly.GetName().Name;
            }

            foreach (var mapping in _mappings)
            {
                if (mapping.Enabled)
                {
                    profile.PropertyMappings.Add(mapping);
                }
            }

            var assetPath = EditorUtility.SaveFilePanelInProject(
                "Save Sync Profile",
                _profileName,
                "asset",
                "Choose where to save the Sync Profile asset.");

            if (string.IsNullOrEmpty(assetPath))
            {
                UnityEngine.Object.DestroyImmediate(profile);
                return;
            }

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();

            EditorUtility.FocusProjectWindow();
            Selection.activeObject = profile;

            EditorUtility.DisplayDialog("Unition",
                $"Sync Profile created at:\n{assetPath}\n\nOpen the Sync Manager to start syncing.",
                "OK");

            Close();
        }
    }
}
