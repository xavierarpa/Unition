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

namespace Unition
{
    public static class NotionBlockBuilder
    {
        public static JObject Paragraph(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "paragraph",
                ["paragraph"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject Heading1(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "heading_1",
                ["heading_1"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject Heading2(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "heading_2",
                ["heading_2"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject Heading3(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "heading_3",
                ["heading_3"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject BulletedListItem(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "bulleted_list_item",
                ["bulleted_list_item"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject NumberedListItem(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "numbered_list_item",
                ["numbered_list_item"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject ToDo(string text, bool isChecked = false)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "to_do",
                ["to_do"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text),
                    ["checked"] = isChecked
                }
            };
        }

        public static JObject Code(string code, string language = "plain text")
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "code",
                ["code"] = new JObject
                {
                    ["rich_text"] = RichTextArray(code),
                    ["language"] = language
                }
            };
        }

        public static JObject Divider()
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "divider",
                ["divider"] = new JObject()
            };
        }

        public static JObject Quote(string text)
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "quote",
                ["quote"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text)
                }
            };
        }

        public static JObject Callout(string text, string emoji = "💡")
        {
            return new JObject
            {
                ["object"] = "block",
                ["type"] = "callout",
                ["callout"] = new JObject
                {
                    ["rich_text"] = RichTextArray(text),
                    ["icon"] = new JObject
                    {
                        ["type"] = "emoji",
                        ["emoji"] = emoji
                    }
                }
            };
        }

        private static JArray RichTextArray(string text)
        {
            return new JArray
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = new JObject
                    {
                        ["content"] = text ?? ""
                    }
                }
            };
        }
    }
}
