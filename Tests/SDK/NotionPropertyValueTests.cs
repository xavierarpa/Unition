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

using NUnit.Framework;

using Unition.Models;
using Unition.Serialization;

namespace Unition.Tests
{
    [TestFixture]
    public sealed class NotionPropertyValueTests
    {
        private static NotionPropertyValue Create(string type, JToken value)
        {
            return new NotionPropertyValue { Type = type, Value = value };
        }

        [Test]
        public void AsPlainText_RichTextArray_ReturnsConcatenated()
        {
            var array = new JArray
            {
                new JObject { ["plain_text"] = "Hello " },
                new JObject { ["plain_text"] = "World" }
            };
            var prop = Create("rich_text", array);
            Assert.AreEqual("Hello World", prop.AsPlainText());
        }

        [Test]
        public void AsPlainText_NullValue_ReturnsNull()
        {
            var prop = Create("rich_text", JValue.CreateNull());
            Assert.IsNull(prop.AsPlainText());
        }

        [Test]
        public void AsNumber_ValidNumber_ReturnsValue()
        {
            var prop = Create("number", new JValue(42.0));
            Assert.AreEqual(42.0, prop.AsNumber());
        }

        [Test]
        public void AsNumber_NullValue_ReturnsNull()
        {
            var prop = Create("number", JValue.CreateNull());
            Assert.IsNull(prop.AsNumber());
        }

        [Test]
        public void AsCheckbox_True_ReturnsTrue()
        {
            var prop = Create("checkbox", new JValue(true));
            Assert.IsTrue(prop.AsCheckbox());
        }

        [Test]
        public void AsCheckbox_NullValue_ReturnsFalse()
        {
            var prop = Create("checkbox", JValue.CreateNull());
            Assert.IsFalse(prop.AsCheckbox());
        }

        [Test]
        public void AsSelect_ValidOption_ReturnsOption()
        {
            var obj = new JObject { ["name"] = "Done", ["color"] = "green" };
            var prop = Create("select", obj);
            var select = prop.AsSelect();
            Assert.IsNotNull(select);
            Assert.AreEqual("Done", select.Name);
        }

        [Test]
        public void AsMultiSelect_ValidArray_ReturnsList()
        {
            var array = new JArray
            {
                new JObject { ["name"] = "A" },
                new JObject { ["name"] = "B" }
            };
            var prop = Create("multi_select", array);
            var list = prop.AsMultiSelect();
            Assert.AreEqual(2, list.Count);
        }

        [Test]
        public void AsRelationIds_ValidArray_ReturnsIds()
        {
            var array = new JArray
            {
                new JObject { ["id"] = "abc-123" },
                new JObject { ["id"] = "def-456" }
            };
            var prop = Create("relation", array);
            var ids = prop.AsRelationIds();
            Assert.AreEqual(2, ids.Count);
            Assert.AreEqual("abc-123", ids[0]);
        }

        [Test]
        public void AsUrl_ValidUrl_ReturnsString()
        {
            var prop = Create("url", new JValue("https://example.com"));
            Assert.AreEqual("https://example.com", prop.AsUrl());
        }

        [Test]
        public void AsDate_ValidDate_ReturnsDateTime()
        {
            var obj = new JObject { ["start"] = "2024-06-15" };
            var prop = Create("date", obj);
            var date = prop.AsDate();
            Assert.IsTrue(date.HasValue);
            Assert.AreEqual(15, date.Value.Day);
        }

        [Test]
        public void AsStatus_ValidStatus_ReturnsName()
        {
            var obj = new JObject { ["name"] = "In Progress" };
            var prop = Create("status", obj);
            Assert.AreEqual("In Progress", prop.AsStatus());
        }

        [Test]
        public void AsFormula_NumberFormula_ReturnsValue()
        {
            var obj = new JObject { ["type"] = "number", ["number"] = 99 };
            var prop = Create("formula", obj);
            Assert.AreEqual("99", prop.AsFormula());
        }
    }
}
