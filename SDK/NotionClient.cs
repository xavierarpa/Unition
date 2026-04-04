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
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Unition.Http;
using Unition.Models;
using Unition.Serialization;

namespace Unition
{
    public sealed class NotionClient
    {
        private const string BaseUrl = "https://api.notion.com/v1";
        private const int MaxRetries = 3;

        private readonly INotionHttp _http;
        private readonly string _authToken;
        private readonly string _apiVersion;
        private readonly NotionRateLimiter _rateLimiter;
        private readonly JsonSerializerSettings _jsonSettings;

        public NotionClient(INotionHttp http, string authToken, string apiVersion = "2022-06-28")
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _authToken = !string.IsNullOrEmpty(authToken)
                ? authToken
                : throw new ArgumentNullException(nameof(authToken));
            _apiVersion = apiVersion;
            _rateLimiter = new NotionRateLimiter();
            _jsonSettings = NotionJsonSettings.Create();
        }

        // ── Users ───────────────────────────────────────────────────────

        public Task<NotionUser> GetCurrentUserAsync(CancellationToken ct = default)
        {
            return GetAsync<NotionUser>("/users/me", ct);
        }

        // ── Search ──────────────────────────────────────────────────────

        public Task<NotionPaginatedList<JObject>> SearchAsync(
            string query = null,
            string filterObject = null,
            string startCursor = null,
            int? pageSize = null,
            CancellationToken ct = default)
        {
            var body = new JObject();
            if (query != null)
            {
                body["query"] = query;
            }
            if (filterObject != null)
            {
                body["filter"] = new JObject
                {
                    ["value"] = filterObject,
                    ["property"] = "object"
                };
            }
            if (startCursor != null)
            {
                body["start_cursor"] = startCursor;
            }
            if (pageSize.HasValue)
            {
                body["page_size"] = pageSize.Value;
            }
            return PostAsync<NotionPaginatedList<JObject>>("/search", body, ct);
        }

        // ── Databases ───────────────────────────────────────────────────

        public Task<NotionDatabase> GetDatabaseAsync(string databaseId, CancellationToken ct = default)
        {
            return GetAsync<NotionDatabase>($"/databases/{databaseId}", ct);
        }

        public Task<NotionDatabase> CreateDatabaseAsync(
            string parentPageId,
            JArray title,
            JObject properties,
            CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["parent"] = new JObject { ["page_id"] = parentPageId },
                ["title"] = title,
                ["properties"] = properties
            };
            return PostAsync<NotionDatabase>("/databases", body, ct);
        }

        public Task<NotionDatabase> UpdateDatabaseAsync(
            string databaseId,
            JObject properties,
            CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["properties"] = properties
            };
            return SendAsync<NotionDatabase>("PATCH", $"/databases/{databaseId}", body.ToString(), ct);
        }

        public Task<NotionPaginatedList<NotionPage>> QueryDatabaseAsync(
            string databaseId,
            JObject filter = null,
            JArray sorts = null,
            string startCursor = null,
            int? pageSize = null,
            IReadOnlyList<string> filterProperties = null,
            CancellationToken ct = default)
        {
            var body = new JObject();
            if (filter != null)
            {
                body["filter"] = filter;
            }
            if (sorts != null)
            {
                body["sorts"] = sorts;
            }
            if (startCursor != null)
            {
                body["start_cursor"] = startCursor;
            }
            if (pageSize.HasValue)
            {
                body["page_size"] = pageSize.Value;
            }

            var path = $"/databases/{databaseId}/query";
            if (filterProperties != null && filterProperties.Count > 0)
            {
                var parts = new List<string>();
                foreach (var id in filterProperties)
                {
                    parts.Add($"filter_properties={Uri.EscapeDataString(id)}");
                }
                path += "?" + string.Join("&", parts);
            }
            return PostAsync<NotionPaginatedList<NotionPage>>(path, body, ct);
        }

        // ── Pages ───────────────────────────────────────────────────────

        public Task<NotionPage> GetPageAsync(string pageId, CancellationToken ct = default)
        {
            return GetAsync<NotionPage>($"/pages/{pageId}", ct);
        }

        public Task<NotionPage> CreatePageAsync(
            string parentDatabaseId,
            JObject properties,
            CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["parent"] = new JObject { ["database_id"] = parentDatabaseId },
                ["properties"] = properties
            };
            return PostAsync<NotionPage>("/pages", body, ct);
        }

        public Task<NotionPage> UpdatePageAsync(
            string pageId,
            JObject properties,
            CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["properties"] = properties
            };
            return SendAsync<NotionPage>("PATCH", $"/pages/{pageId}", body.ToString(), ct);
        }

        public Task<NotionPage> ArchivePageAsync(string pageId, bool archived = true, CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["archived"] = archived
            };
            return SendAsync<NotionPage>("PATCH", $"/pages/{pageId}", body.ToString(), ct);
        }

        // ── Blocks ──────────────────────────────────────────────────────

        public Task<NotionPaginatedList<NotionBlock>> GetBlockChildrenAsync(
            string blockId,
            string startCursor = null,
            int? pageSize = null,
            CancellationToken ct = default)
        {
            var queryParams = new List<string>();
            if (startCursor != null)
            {
                queryParams.Add($"start_cursor={Uri.EscapeDataString(startCursor)}");
            }
            if (pageSize.HasValue)
            {
                queryParams.Add($"page_size={pageSize.Value}");
            }
            var qs = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            return GetAsync<NotionPaginatedList<NotionBlock>>($"/blocks/{blockId}/children{qs}", ct);
        }

        public Task<NotionPaginatedList<NotionBlock>> AppendBlockChildrenAsync(
            string blockId,
            JArray children,
            CancellationToken ct = default)
        {
            var body = new JObject
            {
                ["children"] = children
            };
            return SendAsync<NotionPaginatedList<NotionBlock>>("PATCH", $"/blocks/{blockId}/children", body.ToString(Formatting.None), ct);
        }

        // ── Pagination Helpers ──────────────────────────────────────────

        public async Task<List<NotionPage>> QueryAllPagesAsync(
            string databaseId,
            JObject filter = null,
            JArray sorts = null,
            IReadOnlyList<string> filterProperties = null,
            CancellationToken ct = default)
        {
            var allPages = new List<NotionPage>();
            string cursor = null;

            do
            {
                var result = await QueryDatabaseAsync(databaseId, filter, sorts, cursor, 100, filterProperties, ct);
                if (result.Results != null)
                {
                    allPages.AddRange(result.Results);
                }
                cursor = result.HasMore ? result.NextCursor : null;
            }
            while (cursor != null);

            return allPages;
        }

        // ── Internal ────────────────────────────────────────────────────

        private Task<T> GetAsync<T>(string path, CancellationToken ct)
        {
            return SendAsync<T>("GET", path, null, ct);
        }

        private Task<T> PostAsync<T>(string path, JObject body, CancellationToken ct)
        {
            return SendAsync<T>("POST", path, body.ToString(Formatting.None), ct);
        }

        private async Task<T> SendAsync<T>(string method, string path, string body, CancellationToken ct)
        {
            for (int attempt = 0; attempt <= MaxRetries; attempt++)
            {
                await _rateLimiter.WaitAsync(ct);

                var request = new NotionHttpRequest
                {
                    Url = BaseUrl + path,
                    Method = method,
                    Body = body
                };
                request.Headers["Authorization"] = $"Bearer {_authToken}";
                request.Headers["Notion-Version"] = _apiVersion;
                request.Headers["Content-Type"] = "application/json";

                var response = await _http.SendAsync(request, ct);

                if (response.IsSuccess)
                {
                    return JsonConvert.DeserializeObject<T>(response.Body, _jsonSettings);
                }

                if (response.StatusCode == 429 && attempt < MaxRetries)
                {
                    int retryAfter = 1;
                    if (response.Headers != null &&
                        response.Headers.TryGetValue("Retry-After", out var retryHeader) &&
                        int.TryParse(retryHeader, out var parsed))
                    {
                        retryAfter = parsed;
                    }
                    await _rateLimiter.WaitForRetryAfterAsync(retryAfter, ct);
                    continue;
                }

                if (response.StatusCode >= 500 && attempt < MaxRetries)
                {
                    var backoffMs = (int)Math.Pow(2, attempt) * 1000;
                    await Task.Delay(backoffMs, ct);
                    continue;
                }

                throw NotionApiException.FromResponse(response);
            }

            throw new NotionApiException("Max retries exceeded for rate-limited request", 429);
        }
    }
}
