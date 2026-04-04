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

namespace Unition
{
    public static class NotionPropertyBuilder
    {
        public static JObject Title(string text)
        {
            return new JObject
            {
                ["title"] = new JArray
                {
                    new JObject
                    {
                        ["text"] = new JObject { ["content"] = text ?? string.Empty }
                    }
                }
            };
        }

        public static JObject RichText(string text)
        {
            return new JObject
            {
                ["rich_text"] = new JArray
                {
                    new JObject
                    {
                        ["text"] = new JObject { ["content"] = text ?? string.Empty }
                    }
                }
            };
        }

        public static JObject Number(double value)
        {
            return new JObject { ["number"] = value };
        }

        public static JObject Checkbox(bool value)
        {
            return new JObject { ["checkbox"] = value };
        }

        public static JObject Select(string name)
        {
            return new JObject
            {
                ["select"] = new JObject { ["name"] = name }
            };
        }

        public static JObject MultiSelect(params string[] names)
        {
            var array = new JArray();
            foreach (var name in names)
            {
                array.Add(new JObject { ["name"] = name });
            }
            return new JObject { ["multi_select"] = array };
        }

        public static JObject Url(string url)
        {
            return new JObject { ["url"] = url };
        }

        public static JObject Email(string email)
        {
            return new JObject { ["email"] = email };
        }

        public static JObject PhoneNumber(string phone)
        {
            return new JObject { ["phone_number"] = phone };
        }

        public static JObject Date(DateTime start)
        {
            return new JObject
            {
                ["date"] = new JObject
                {
                    ["start"] = start.ToString("yyyy-MM-dd")
                }
            };
        }

        public static JObject DateWithTime(DateTime start)
        {
            return new JObject
            {
                ["date"] = new JObject
                {
                    ["start"] = start.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            };
        }

        public static JObject DateRange(DateTime start, DateTime end)
        {
            return new JObject
            {
                ["date"] = new JObject
                {
                    ["start"] = start.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ["end"] = end.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            };
        }

        public static JObject Status(string name)
        {
            return new JObject
            {
                ["status"] = new JObject { ["name"] = name }
            };
        }

        public static JObject Relation(params string[] pageIds)
        {
            var array = new JArray();
            foreach (var id in pageIds)
            {
                array.Add(new JObject { ["id"] = id });
            }
            return new JObject { ["relation"] = array };
        }

        public static JObject People(params string[] userIds)
        {
            var array = new JArray();
            foreach (var id in userIds)
            {
                array.Add(new JObject { ["object"] = "user", ["id"] = id });
            }
            return new JObject { ["people"] = array };
        }

        public static JObject Files(params (string name, string url)[] files)
        {
            var array = new JArray();
            foreach (var (name, url) in files)
            {
                array.Add(new JObject
                {
                    ["name"] = name,
                    ["type"] = "external",
                    ["external"] = new JObject { ["url"] = url }
                });
            }
            return new JObject { ["files"] = array };
        }

        public static JObject Properties(params (string name, JObject value)[] properties)
        {
            var obj = new JObject();
            foreach (var (name, value) in properties)
            {
                obj[name] = value;
            }
            return obj;
        }
    }
}
