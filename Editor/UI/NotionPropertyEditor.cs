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

using Unition.Models;

using UnityEngine.UIElements;

namespace Unition.Editor.UI
{
    public static class NotionPropertyEditor
    {
        public static VisualElement CreateField(
            string propertyName,
            NotionPropertyValue prop,
            Dictionary<string, JObject> editedProperties)
        {
            if (prop == null || prop.Type == null)
            {
                return new Label("—");
            }

            switch (prop.Type)
            {
                case "title":
                {
                    var text = prop.AsPlainText() ?? "";
                    var field = new TextField { value = text };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Title(evt.newValue);
                    });
                    return field;
                }

                case "rich_text":
                {
                    var text = prop.AsPlainText() ?? "";
                    var field = new TextField { value = text, multiline = true };
                    field.style.minHeight = 40;
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.RichText(evt.newValue);
                    });
                    return field;
                }

                case "number":
                {
                    var num = prop.AsNumber();
                    var field = new FloatField { value = (float)(num ?? 0) };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Number(evt.newValue);
                    });
                    return field;
                }

                case "checkbox":
                {
                    var val = prop.AsCheckbox();
                    var toggle = new Toggle { value = val };
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Checkbox(evt.newValue);
                    });
                    return toggle;
                }

                case "select":
                {
                    var sel = prop.AsSelect();
                    var field = new TextField { value = sel?.Name ?? "" };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Select(evt.newValue);
                    });
                    return field;
                }

                case "status":
                {
                    var status = prop.AsStatus() ?? "";
                    var field = new TextField { value = status };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Status(evt.newValue);
                    });
                    return field;
                }

                case "url":
                {
                    var url = prop.AsUrl() ?? "";
                    var field = new TextField { value = url };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Url(evt.newValue);
                    });
                    return field;
                }

                case "email":
                {
                    var email = prop.AsEmail() ?? "";
                    var field = new TextField { value = email };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.Email(evt.newValue);
                    });
                    return field;
                }

                case "phone_number":
                {
                    var phone = prop.AsPhoneNumber() ?? "";
                    var phoneField = new TextField { value = phone };
                    phoneField.RegisterValueChangedCallback(evt =>
                    {
                        editedProperties[propertyName] = NotionPropertyBuilder.PhoneNumber(evt.newValue);
                    });
                    return phoneField;
                }

                case "date":
                {
                    var dateStr = prop.AsDate()?.ToString("yyyy-MM-dd") ?? "";
                    var field = new TextField { value = dateStr };
                    field.RegisterValueChangedCallback(evt =>
                    {
                        if (DateTime.TryParse(evt.newValue, out var date))
                        {
                            editedProperties[propertyName] = NotionPropertyBuilder.Date(date);
                        }
                    });
                    return field;
                }

                case "multi_select":
                {
                    var options = prop.AsMultiSelect();
                    var container = new VisualElement();
                    container.style.flexDirection = FlexDirection.Row;
                    container.style.flexWrap = Wrap.Wrap;
                    container.style.alignItems = Align.Center;

                    var currentNames = options.Select(o => o.Name).ToList();

                    foreach (var name in currentNames)
                    {
                        container.Add(CreateRemovablePill(name, currentNames, propertyName, editedProperties, container));
                    }

                    var addField = new TextField { value = "" };
                    addField.style.width = 80;
                    addField.RegisterCallback<KeyDownEvent>(evt =>
                    {
                        if (evt.keyCode == UnityEngine.KeyCode.Return && !string.IsNullOrWhiteSpace(addField.value))
                        {
                            var newName = addField.value.Trim();
                            if (!currentNames.Contains(newName))
                            {
                                currentNames.Add(newName);
                                container.Insert(container.childCount - 1,
                                    CreateRemovablePill(newName, currentNames, propertyName, editedProperties, container));
                                editedProperties[propertyName] = NotionPropertyBuilder.MultiSelect(currentNames.ToArray());
                            }
                            addField.value = "";
                            evt.StopPropagation();
                        }
                    });
                    container.Add(addField);

                    return container;
                }

                case "relation":
                {
                    var ids = prop.AsRelationIds();
                    var container = new VisualElement();

                    var currentIds = new List<string>(ids);

                    for (int i = 0; i < currentIds.Count; i++)
                    {
                        var idx = i;
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.marginBottom = 2;

                        var field = new TextField { value = currentIds[idx] };
                        field.style.flexGrow = 1;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            currentIds[idx] = evt.newValue;
                            editedProperties[propertyName] = NotionPropertyBuilder.Relation(currentIds.ToArray());
                        });
                        row.Add(field);

                        var removeBtn = new Button(() =>
                        {
                            currentIds.RemoveAt(idx);
                            editedProperties[propertyName] = NotionPropertyBuilder.Relation(currentIds.ToArray());
                            container.Remove(row);
                        }) { text = "✕" };
                        removeBtn.style.width = 24;
                        row.Add(removeBtn);

                        container.Add(row);
                    }

                    var addBtn = new Button(() =>
                    {
                        var newIdx = currentIds.Count;
                        currentIds.Add("");
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.marginBottom = 2;

                        var field = new TextField { value = "" };
                        field.style.flexGrow = 1;
                        field.RegisterValueChangedCallback(evt =>
                        {
                            currentIds[newIdx] = evt.newValue;
                            editedProperties[propertyName] = NotionPropertyBuilder.Relation(currentIds.ToArray());
                        });
                        row.Add(field);

                        var removeBtn = new Button(() =>
                        {
                            currentIds.RemoveAt(newIdx);
                            editedProperties[propertyName] = NotionPropertyBuilder.Relation(currentIds.ToArray());
                            container.Remove(row);
                        }) { text = "✕" };
                        removeBtn.style.width = 24;
                        row.Add(removeBtn);

                        container.Insert(container.childCount - 1, row);
                    }) { text = "+ Add Relation" };
                    container.Add(addBtn);

                    return container;
                }

                default:
                {
                    return NotionPropertyRenderer.Render(prop);
                }
            }
        }

        private static VisualElement CreateRemovablePill(
            string name,
            List<string> currentNames,
            string propertyName,
            Dictionary<string, JObject> editedProperties,
            VisualElement container)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.backgroundColor = new UnityEngine.Color(0.25f, 0.25f, 0.3f, 1f);
            pill.style.borderTopLeftRadius = 10;
            pill.style.borderTopRightRadius = 10;
            pill.style.borderBottomLeftRadius = 10;
            pill.style.borderBottomRightRadius = 10;
            pill.style.paddingLeft = 8;
            pill.style.paddingRight = 4;
            pill.style.paddingTop = 2;
            pill.style.paddingBottom = 2;
            pill.style.marginRight = 4;
            pill.style.marginBottom = 2;

            var label = new Label(name);
            label.style.fontSize = 11;
            pill.Add(label);

            var removeBtn = new Button(() =>
            {
                currentNames.Remove(name);
                editedProperties[propertyName] = NotionPropertyBuilder.MultiSelect(currentNames.ToArray());
                container.Remove(pill);
            }) { text = "✕" };
            removeBtn.style.width = 18;
            removeBtn.style.height = 18;
            removeBtn.style.fontSize = 9;
            removeBtn.style.marginLeft = 2;
            removeBtn.style.paddingLeft = 0;
            removeBtn.style.paddingRight = 0;
            removeBtn.style.paddingTop = 0;
            removeBtn.style.paddingBottom = 0;
            removeBtn.style.borderTopWidth = 0;
            removeBtn.style.borderBottomWidth = 0;
            removeBtn.style.borderLeftWidth = 0;
            removeBtn.style.borderRightWidth = 0;
            removeBtn.style.backgroundColor = new UnityEngine.Color(0, 0, 0, 0);
            pill.Add(removeBtn);

            return pill;
        }
    }
}
