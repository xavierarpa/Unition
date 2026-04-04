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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Unition.Serialization;

namespace Unition.Models
{
    [JsonConverter(typeof(NotionPropertyValueConverter))]
    public sealed class NotionPropertyValue
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public JToken Value { get; set; }

        public string AsPlainText()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            if (Value.Type == JTokenType.Array)
            {
                var richTexts = Value.ToObject<List<NotionRichText>>();
                return NotionRichText.ToPlainText(richTexts);
            }

            if (Value.Type == JTokenType.String)
            {
                return Value.ToString();
            }

            return Value.ToString();
        }

        public double? AsNumber()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }
            return Value.ToObject<double>();
        }

        public bool AsCheckbox()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return false;
            }
            return Value.ToObject<bool>();
        }

        public NotionSelectOption AsSelect()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }
            return Value.ToObject<NotionSelectOption>();
        }

        public List<NotionSelectOption> AsMultiSelect()
        {
            if (Value == null || Value.Type != JTokenType.Array)
            {
                return new List<NotionSelectOption>();
            }
            return Value.ToObject<List<NotionSelectOption>>();
        }

        public List<string> AsRelationIds()
        {
            if (Value == null || Value.Type != JTokenType.Array)
            {
                return new List<string>();
            }

            var ids = new List<string>();
            foreach (var item in Value)
            {
                var id = item["id"]?.ToString();
                if (id != null)
                {
                    ids.Add(id);
                }
            }
            return ids;
        }

        public string AsUrl()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }
            return Value.ToString();
        }

        public DateTime? AsDate()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            var start = Value["start"]?.ToString();
            if (start != null && DateTime.TryParse(start, out var date))
            {
                return date;
            }
            return null;
        }

        public string AsStatus()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }
            return Value["name"]?.ToString();
        }

        public string AsFormula()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            var formulaType = Value["type"]?.ToString();
            if (formulaType != null && Value[formulaType] != null)
            {
                return Value[formulaType].ToString();
            }
            return null;
        }

        public string AsEmail()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            return Value.ToString();
        }

        public string AsPhoneNumber()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            return Value.ToString();
        }

        public List<NotionUser> AsPeople()
        {
            if (Value == null || Value.Type != JTokenType.Array)
            {
                return new List<NotionUser>();
            }

            return Value.ToObject<List<NotionUser>>();
        }

        public List<string> AsPeopleNames()
        {
            var people = AsPeople();
            var names = new List<string>(people.Count);
            foreach (var person in people)
            {
                if (!string.IsNullOrEmpty(person.Name))
                {
                    names.Add(person.Name);
                }
            }
            return names;
        }

        public string AsRollupDisplay()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            var rollupType = Value["type"]?.ToString();
            if (rollupType == null)
            {
                return Value.ToString();
            }

            var inner = Value[rollupType];
            if (inner == null || inner.Type == JTokenType.Null)
            {
                return null;
            }

            if (inner.Type == JTokenType.Array)
            {
                var parts = new List<string>();
                foreach (var item in inner)
                {
                    parts.Add(item.ToString());
                }
                return string.Join(", ", parts);
            }

            return inner.ToString();
        }

        public string AsUserName()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            return Value["name"]?.ToString();
        }

        public string AsTimestamp()
        {
            if (Value == null || Value.Type == JTokenType.Null)
            {
                return null;
            }

            return Value.ToString();
        }
    }
}
