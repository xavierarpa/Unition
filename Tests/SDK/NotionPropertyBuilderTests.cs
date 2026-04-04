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

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Unition.Tests
{
    [TestFixture]
    public sealed class NotionPropertyBuilderTests
    {
        [Test]
        public void Title_ReturnsCorrectStructure()
        {
            var result = NotionPropertyBuilder.Title("Hello");
            Assert.IsNotNull(result["title"]);
            Assert.AreEqual("Hello", result["title"][0]["text"]["content"].ToString());
        }

        [Test]
        public void Title_NullText_ReturnsEmptyString()
        {
            var result = NotionPropertyBuilder.Title(null);
            Assert.AreEqual(string.Empty, result["title"][0]["text"]["content"].ToString());
        }

        [Test]
        public void RichText_ReturnsCorrectStructure()
        {
            var result = NotionPropertyBuilder.RichText("World");
            Assert.IsNotNull(result["rich_text"]);
            Assert.AreEqual("World", result["rich_text"][0]["text"]["content"].ToString());
        }

        [Test]
        public void Number_ReturnsCorrectValue()
        {
            var result = NotionPropertyBuilder.Number(42.5);
            Assert.AreEqual(42.5, result["number"].ToObject<double>());
        }

        [Test]
        public void Checkbox_True_ReturnsTrue()
        {
            var result = NotionPropertyBuilder.Checkbox(true);
            Assert.AreEqual(true, result["checkbox"].ToObject<bool>());
        }

        [Test]
        public void Checkbox_False_ReturnsFalse()
        {
            var result = NotionPropertyBuilder.Checkbox(false);
            Assert.AreEqual(false, result["checkbox"].ToObject<bool>());
        }

        [Test]
        public void Select_ReturnsNameObject()
        {
            var result = NotionPropertyBuilder.Select("Option A");
            Assert.AreEqual("Option A", result["select"]["name"].ToString());
        }

        [Test]
        public void MultiSelect_ReturnsArrayOfNames()
        {
            var result = NotionPropertyBuilder.MultiSelect("A", "B", "C");
            var arr = (JArray)result["multi_select"];
            Assert.AreEqual(3, arr.Count);
            Assert.AreEqual("A", arr[0]["name"].ToString());
            Assert.AreEqual("B", arr[1]["name"].ToString());
            Assert.AreEqual("C", arr[2]["name"].ToString());
        }

        [Test]
        public void Url_ReturnsUrlString()
        {
            var result = NotionPropertyBuilder.Url("https://example.com");
            Assert.AreEqual("https://example.com", result["url"].ToString());
        }

        [Test]
        public void Email_ReturnsEmailString()
        {
            var result = NotionPropertyBuilder.Email("test@example.com");
            Assert.AreEqual("test@example.com", result["email"].ToString());
        }

        [Test]
        public void Date_ReturnsDateOnly()
        {
            var date = new DateTime(2024, 6, 15);
            var result = NotionPropertyBuilder.Date(date);
            Assert.AreEqual("2024-06-15", result["date"]["start"].ToString());
        }

        [Test]
        public void DateWithTime_IncludesTime()
        {
            var date = new DateTime(2024, 6, 15, 14, 30, 0, DateTimeKind.Utc);
            var result = NotionPropertyBuilder.DateWithTime(date);
            var start = result["date"]["start"].ToString();
            Assert.IsTrue(start.Contains("14:30:00"));
        }

        [Test]
        public void DateRange_HasStartAndEnd()
        {
            var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2024, 12, 31, 0, 0, 0, DateTimeKind.Utc);
            var result = NotionPropertyBuilder.DateRange(start, end);
            Assert.IsNotNull(result["date"]["start"]);
            Assert.IsNotNull(result["date"]["end"]);
        }

        [Test]
        public void Status_ReturnsNameObject()
        {
            var result = NotionPropertyBuilder.Status("In Progress");
            Assert.AreEqual("In Progress", result["status"]["name"].ToString());
        }

        [Test]
        public void Relation_ReturnsSingleId()
        {
            var result = NotionPropertyBuilder.Relation("abc-123");
            var arr = (JArray)result["relation"];
            Assert.AreEqual(1, arr.Count);
            Assert.AreEqual("abc-123", arr[0]["id"].ToString());
        }

        [Test]
        public void Relation_ReturnsMultipleIds()
        {
            var result = NotionPropertyBuilder.Relation("id1", "id2");
            var arr = (JArray)result["relation"];
            Assert.AreEqual(2, arr.Count);
        }

        [Test]
        public void Properties_CombinesMultiple()
        {
            var result = NotionPropertyBuilder.Properties(
                ("Title", NotionPropertyBuilder.Title("Test")),
                ("Count", NotionPropertyBuilder.Number(10)));
            Assert.IsNotNull(result["Title"]);
            Assert.IsNotNull(result["Count"]);
        }
    }
}
