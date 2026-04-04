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
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using NUnit.Framework;

namespace Unition.Tests
{
    [TestFixture]
    public sealed class NotionClientTests
    {
        private MockNotionHttp _http;
        private NotionClient _client;

        [SetUp]
        public void SetUp()
        {
            _http = new MockNotionHttp();
            _client = new NotionClient(_http, "test-token");
        }

        [Test]
        public async Task GetCurrentUserAsync_SendsCorrectRequest()
        {
            _http.Enqueue(200, "{\"id\":\"user-1\",\"object\":\"user\",\"name\":\"Test\",\"type\":\"bot\"}");

            var user = await _client.GetCurrentUserAsync();

            Assert.AreEqual(1, _http.SentRequests.Count);
            Assert.IsTrue(_http.SentRequests[0].Url.EndsWith("/users/me"));
            Assert.AreEqual("GET", _http.SentRequests[0].Method);
            Assert.AreEqual("user-1", user.Id);
        }

        [Test]
        public async Task QueryDatabaseAsync_SendsPostWithBody()
        {
            _http.Enqueue(200, "{\"object\":\"list\",\"results\":[],\"has_more\":false,\"next_cursor\":null}");

            var result = await _client.QueryDatabaseAsync("db-123", pageSize: 10);

            Assert.AreEqual(1, _http.SentRequests.Count);
            Assert.AreEqual("POST", _http.SentRequests[0].Method);
            Assert.IsTrue(_http.SentRequests[0].Url.Contains("/databases/db-123/query"));

            var body = JObject.Parse(_http.SentRequests[0].Body);
            Assert.AreEqual(10, body["page_size"]?.Value<int>());
        }

        [Test]
        public async Task QueryDatabaseAsync_WithFilterProperties_AppendsQueryString()
        {
            _http.Enqueue(200, "{\"object\":\"list\",\"results\":[],\"has_more\":false,\"next_cursor\":null}");

            var filterProps = new[] { "prop-a", "prop-b" };
            await _client.QueryDatabaseAsync("db-123", filterProperties: filterProps);

            var url = _http.SentRequests[0].Url;
            Assert.IsTrue(url.Contains("filter_properties=prop-a"));
            Assert.IsTrue(url.Contains("filter_properties=prop-b"));
        }

        [Test]
        public async Task CreatePageAsync_SendsProperties()
        {
            _http.Enqueue(200, "{\"id\":\"page-1\",\"object\":\"page\",\"properties\":{}}");

            var props = new JObject
            {
                ["Name"] = new JObject
                {
                    ["title"] = new JArray
                    {
                        new JObject { ["text"] = new JObject { ["content"] = "Test" } }
                    }
                }
            };

            var page = await _client.CreatePageAsync("db-123", props);

            Assert.AreEqual("page-1", page.Id);
            var body = JObject.Parse(_http.SentRequests[0].Body);
            Assert.IsNotNull(body["parent"]);
            Assert.AreEqual("db-123", body["parent"]["database_id"]?.ToString());
        }

        [Test]
        public async Task UpdatePageAsync_SendsPatch()
        {
            _http.Enqueue(200, "{\"id\":\"page-1\",\"object\":\"page\",\"properties\":{}}");

            var props = new JObject
            {
                ["Status"] = new JObject { ["select"] = new JObject { ["name"] = "Done" } }
            };

            await _client.UpdatePageAsync("page-1", props);

            Assert.AreEqual("PATCH", _http.SentRequests[0].Method);
            Assert.IsTrue(_http.SentRequests[0].Url.Contains("/pages/page-1"));
        }

        [Test]
        public async Task QueryAllPagesAsync_PaginatesThroughResults()
        {
            _http.Enqueue(200, "{\"object\":\"list\",\"results\":[{\"id\":\"p1\",\"object\":\"page\",\"properties\":{}}],\"has_more\":true,\"next_cursor\":\"cursor-2\"}");
            _http.Enqueue(200, "{\"object\":\"list\",\"results\":[{\"id\":\"p2\",\"object\":\"page\",\"properties\":{}}],\"has_more\":false,\"next_cursor\":null}");

            var pages = await _client.QueryAllPagesAsync("db-123");

            Assert.AreEqual(2, pages.Count);
            Assert.AreEqual("p1", pages[0].Id);
            Assert.AreEqual("p2", pages[1].Id);
            Assert.AreEqual(2, _http.SentRequests.Count);
        }

        [Test]
        public async Task GetDatabaseAsync_ReturnsDatabase()
        {
            _http.Enqueue(200, "{\"id\":\"db-1\",\"object\":\"database\",\"title\":[{\"plain_text\":\"My DB\"}],\"properties\":{}}");

            var db = await _client.GetDatabaseAsync("db-1");

            Assert.AreEqual("db-1", db.Id);
            Assert.AreEqual("GET", _http.SentRequests[0].Method);
        }

        [Test]
        public void QueryDatabaseAsync_On401_ThrowsWithMessage()
        {
            _http.Enqueue(401, "{\"object\":\"error\",\"status\":401,\"message\":\"API token is invalid.\"}");

            var ex = Assert.ThrowsAsync<System.Exception>(async () =>
            {
                await _client.QueryDatabaseAsync("db-123");
            });

            Assert.IsTrue(ex.Message.Contains("401") || ex.Message.Contains("invalid") || ex.Message.Contains("error"),
                $"Expected error message about 401, got: {ex.Message}");
        }

        [Test]
        public async Task SearchAsync_SendsQueryInBody()
        {
            _http.Enqueue(200, "{\"object\":\"list\",\"results\":[],\"has_more\":false,\"next_cursor\":null}");

            await _client.SearchAsync(query: "test", filterObject: "database");

            var body = JObject.Parse(_http.SentRequests[0].Body);
            Assert.AreEqual("test", body["query"]?.ToString());
            Assert.AreEqual("database", body["filter"]?["value"]?.ToString());
        }

        [Test]
        public async Task ArchivePageAsync_SendsArchivedFlag()
        {
            _http.Enqueue(200, "{\"id\":\"page-1\",\"object\":\"page\",\"archived\":true,\"properties\":{}}");

            await _client.ArchivePageAsync("page-1", true);

            Assert.AreEqual("PATCH", _http.SentRequests[0].Method);
            var body = JObject.Parse(_http.SentRequests[0].Body);
            Assert.AreEqual(true, body["archived"]?.Value<bool>());
        }

        [Test]
        public async Task GetBlockChildrenAsync_WithPagination_BuildsQueryString()
        {
            _http.Enqueue(200, "{\"object\":\"list\",\"results\":[],\"has_more\":false,\"next_cursor\":null}");

            await _client.GetBlockChildrenAsync("block-1", startCursor: "c1", pageSize: 25);

            var url = _http.SentRequests[0].Url;
            Assert.IsTrue(url.Contains("start_cursor=c1"));
            Assert.IsTrue(url.Contains("page_size=25"));
        }

        [Test]
        public void Client_SetsAuthorizationHeader()
        {
            _http.Enqueue(200, "{\"id\":\"u1\",\"object\":\"user\",\"name\":\"Me\",\"type\":\"bot\"}");
            _client.GetCurrentUserAsync().Wait();

            var headers = _http.SentRequests[0].Headers;
            Assert.IsTrue(headers.ContainsKey("Authorization"));
            Assert.AreEqual("Bearer test-token", headers["Authorization"]);
        }

        [Test]
        public void Client_SetsNotionVersionHeader()
        {
            _http.Enqueue(200, "{\"id\":\"u1\",\"object\":\"user\",\"name\":\"Me\",\"type\":\"bot\"}");
            _client.GetCurrentUserAsync().Wait();

            var headers = _http.SentRequests[0].Headers;
            Assert.IsTrue(headers.ContainsKey("Notion-Version"));
            Assert.AreEqual("2022-06-28", headers["Notion-Version"]);
        }
    }
}
