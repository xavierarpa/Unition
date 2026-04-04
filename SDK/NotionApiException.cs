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

using Unition.Http;

namespace Unition
{
    public class NotionApiException : Exception
    {
        public int StatusCode { get; }
        public string NotionCode { get; }

        public NotionApiException(string message, int statusCode, string notionCode = null)
            : base(message)
        {
            StatusCode = statusCode;
            NotionCode = notionCode;
        }

        public static NotionApiException FromResponse(NotionHttpResponse response)
        {
            string apiMessage = null;
            string code = null;

            try
            {
                var error = JObject.Parse(response.Body);
                apiMessage = error["message"]?.ToString();
                code = error["code"]?.ToString();
            }
            catch
            {
            }

            var friendly = GetFriendlyMessage(response.StatusCode, apiMessage);
            return new NotionApiException(friendly, response.StatusCode, code);
        }

        private static string GetFriendlyMessage(int statusCode, string apiMessage)
        {
            var detail = !string.IsNullOrEmpty(apiMessage) ? $" — {apiMessage}" : "";
            switch (statusCode)
            {
                case 400: return $"Bad request{detail}";
                case 401: return $"Invalid or expired API token. Check your Notion integration token{detail}";
                case 403: return $"Access denied. Ensure the integration has access to the resource{detail}";
                case 404: return $"Resource not found. The database or page may have been deleted{detail}";
                case 409: return $"Conflict. The resource was modified by another request{detail}";
                case 429: return $"Rate limited by Notion API. Retrying automatically{detail}";
                default:
                    if (statusCode >= 500)
                    {
                        return $"Notion server error ({statusCode}). Try again later{detail}";
                    }
                    return apiMessage ?? $"Request failed with status {statusCode}";
            }
        }

        public bool IsRateLimited => StatusCode == 429;
        public bool IsUnauthorized => StatusCode == 401;
        public bool IsNotFound => StatusCode == 404;
        public bool IsValidationError => StatusCode == 400;
        public bool IsServerError => StatusCode >= 500;
    }
}
