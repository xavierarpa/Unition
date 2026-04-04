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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Unition.Models;

namespace Unition.Serialization
{
    public sealed class NotionPropertyValueConverter : JsonConverter<NotionPropertyValue>
    {
        public override NotionPropertyValue ReadJson(
            JsonReader reader,
            Type objectType,
            NotionPropertyValue existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);

            var id = obj["id"]?.ToString();
            var type = obj["type"]?.ToString();

            JToken value = null;
            if (type != null && obj.TryGetValue(type, out var typeValue))
            {
                value = typeValue;
            }

            return new NotionPropertyValue
            {
                Id = id,
                Type = type,
                Value = value
            };
        }

        public override void WriteJson(JsonWriter writer, NotionPropertyValue value, JsonSerializer serializer)
        {
            var obj = new JObject();

            if (value.Id != null)
            {
                obj["id"] = value.Id;
            }

            if (value.Type != null)
            {
                obj["type"] = value.Type;
                if (value.Value != null)
                {
                    obj[value.Type] = value.Value;
                }
            }

            obj.WriteTo(writer);
        }
    }
}
