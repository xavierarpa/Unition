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
using Newtonsoft.Json.Linq;

using NUnit.Framework;

using Unition.Editor.Sync;

using UnityEngine;

namespace Unition.Editor.Tests
{
    [TestFixture]
    public sealed class NotionWriteBackEngineTests
    {
        private sealed class TestSO : ScriptableObject
        {
            public string title = "Test Title";
            public int count = 42;
            public float rating = 4.5f;
            public bool active = true;
        }

        [Test]
        public void BuildProperties_MapsStringToTitle()
        {
            var so = ScriptableObject.CreateInstance<TestSO>();
            so.title = "My Asset";

            var profile = ScriptableObject.CreateInstance<NotionSyncProfile>();
            profile.PropertyMappings.Add(new NotionPropertyMapping
            {
                NotionPropertyName = "Name",
                NotionPropertyType = "title",
                TargetFieldName = "title",
                TargetFieldType = "string",
                Enabled = true
            });

            var result = NotionWriteBackEngine.BuildProperties(so, profile);
            Assert.IsNotNull(result["Name"]);
            Assert.AreEqual("My Asset", result["Name"]["title"][0]["text"]["content"].ToString());

            Object.DestroyImmediate(so);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void BuildProperties_MapsIntToNumber()
        {
            var so = ScriptableObject.CreateInstance<TestSO>();
            so.count = 99;

            var profile = ScriptableObject.CreateInstance<NotionSyncProfile>();
            profile.PropertyMappings.Add(new NotionPropertyMapping
            {
                NotionPropertyName = "Count",
                NotionPropertyType = "number",
                TargetFieldName = "count",
                TargetFieldType = "int",
                Enabled = true
            });

            var result = NotionWriteBackEngine.BuildProperties(so, profile);
            Assert.AreEqual(99.0, result["Count"]["number"].ToObject<double>());

            Object.DestroyImmediate(so);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void BuildProperties_MapsBoolToCheckbox()
        {
            var so = ScriptableObject.CreateInstance<TestSO>();
            so.active = false;

            var profile = ScriptableObject.CreateInstance<NotionSyncProfile>();
            profile.PropertyMappings.Add(new NotionPropertyMapping
            {
                NotionPropertyName = "Active",
                NotionPropertyType = "checkbox",
                TargetFieldName = "active",
                TargetFieldType = "bool",
                Enabled = true
            });

            var result = NotionWriteBackEngine.BuildProperties(so, profile);
            Assert.AreEqual(false, result["Active"]["checkbox"].ToObject<bool>());

            Object.DestroyImmediate(so);
            Object.DestroyImmediate(profile);
        }

        [Test]
        public void BuildProperties_SkipsDisabledMappings()
        {
            var so = ScriptableObject.CreateInstance<TestSO>();

            var profile = ScriptableObject.CreateInstance<NotionSyncProfile>();
            profile.PropertyMappings.Add(new NotionPropertyMapping
            {
                NotionPropertyName = "Name",
                NotionPropertyType = "title",
                TargetFieldName = "title",
                TargetFieldType = "string",
                Enabled = false
            });

            var result = NotionWriteBackEngine.BuildProperties(so, profile);
            Assert.IsNull(result["Name"]);

            Object.DestroyImmediate(so);
            Object.DestroyImmediate(profile);
        }
    }
}
