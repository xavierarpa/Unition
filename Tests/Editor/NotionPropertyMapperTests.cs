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

using NUnit.Framework;

using Unition.Editor.Sync;
using Unition.Models;

using UnityEngine;

namespace Unition.Editor.Tests
{
    [TestFixture]
    public sealed class NotionPropertyMapperTests
    {
        private static NotionPropertyValue CreateProp(string type, JToken value)
        {
            return new NotionPropertyValue { Type = type, Value = value };
        }

        [Test]
        public void Convert_TitleToString_ReturnsPlainText()
        {
            var array = new JArray
            {
                new JObject { ["plain_text"] = "My Title" }
            };
            var prop = CreateProp("title", array);
            var result = NotionPropertyMapper.Convert(prop, typeof(string));
            Assert.AreEqual("My Title", result);
        }

        [Test]
        public void Convert_NumberToInt_ConvertsCorrectly()
        {
            var prop = CreateProp("number", new JValue(42.0));
            var result = NotionPropertyMapper.Convert(prop, typeof(int));
            Assert.AreEqual(42, result);
        }

        [Test]
        public void Convert_NumberToFloat_ConvertsCorrectly()
        {
            var prop = CreateProp("number", new JValue(3.14));
            var result = NotionPropertyMapper.Convert(prop, typeof(float));
            Assert.AreEqual(3.14f, (float)result, 0.001f);
        }

        [Test]
        public void Convert_CheckboxToBool_ReturnsTrue()
        {
            var prop = CreateProp("checkbox", new JValue(true));
            var result = NotionPropertyMapper.Convert(prop, typeof(bool));
            Assert.AreEqual(true, result);
        }

        [Test]
        public void Convert_SelectToString_ReturnsName()
        {
            var obj = new JObject { ["name"] = "OptionA", ["color"] = "red" };
            var prop = CreateProp("select", obj);
            var result = NotionPropertyMapper.Convert(prop, typeof(string));
            Assert.AreEqual("OptionA", result);
        }

        [Test]
        public void Convert_MultiSelectToStringArray_ReturnsNames()
        {
            var array = new JArray
            {
                new JObject { ["name"] = "Tag1" },
                new JObject { ["name"] = "Tag2" }
            };
            var prop = CreateProp("multi_select", array);
            var result = (string[])NotionPropertyMapper.Convert(prop, typeof(string[]));
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("Tag1", result[0]);
            Assert.AreEqual("Tag2", result[1]);
        }

        [Test]
        public void Convert_MultiSelectToStringList_ReturnsNames()
        {
            var array = new JArray
            {
                new JObject { ["name"] = "A" },
                new JObject { ["name"] = "B" }
            };
            var prop = CreateProp("multi_select", array);
            var result = (List<string>)NotionPropertyMapper.Convert(prop, typeof(List<string>));
            Assert.AreEqual(2, result.Count);
        }

        [Test]
        public void Convert_DateToDateTime_ParsesCorrectly()
        {
            var obj = new JObject { ["start"] = "2024-06-15" };
            var prop = CreateProp("date", obj);
            var result = (DateTime)NotionPropertyMapper.Convert(prop, typeof(DateTime));
            Assert.AreEqual(2024, result.Year);
            Assert.AreEqual(6, result.Month);
            Assert.AreEqual(15, result.Day);
        }

        [Test]
        public void Convert_NullProperty_ReturnsDefault()
        {
            var result = NotionPropertyMapper.Convert(null, typeof(int));
            Assert.AreEqual(0, result);
        }

        [Test]
        public void Convert_NullValue_ReturnsDefault()
        {
            var prop = new NotionPropertyValue { Type = "number", Value = null };
            var result = NotionPropertyMapper.Convert(prop, typeof(int));
            Assert.AreEqual(0, result);
        }

        [Test]
        public void Convert_UrlToString_ReturnsUrl()
        {
            var prop = CreateProp("url", new JValue("https://example.com"));
            var result = NotionPropertyMapper.Convert(prop, typeof(string));
            Assert.AreEqual("https://example.com", result);
        }

        [Test]
        public void Convert_NumberToLong_ConvertsCorrectly()
        {
            var prop = CreateProp("number", new JValue(123456789.0));
            var result = NotionPropertyMapper.Convert(prop, typeof(long));
            Assert.AreEqual(123456789L, result);
        }
    }
}
