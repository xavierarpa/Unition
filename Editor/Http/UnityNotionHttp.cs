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
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Unition.Http;

using UnityEngine.Networking;

namespace Unition.Editor.Http
{
    public sealed class UnityNotionHttp : INotionHttp
    {
        public async Task<NotionHttpResponse> SendAsync(
            NotionHttpRequest request,
            CancellationToken cancellationToken = default)
        {
            using (var webRequest = CreateWebRequest(request))
            {
                var operation = webRequest.SendWebRequest();

                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        webRequest.Abort();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    await Task.Yield();
                }

                var responseHeaders = new Dictionary<string, string>();
                var headers = webRequest.GetResponseHeaders();
                if (headers != null)
                {
                    foreach (var kvp in headers)
                    {
                        responseHeaders[kvp.Key] = kvp.Value;
                    }
                }

                return new NotionHttpResponse
                {
                    StatusCode = (int)webRequest.responseCode,
                    Body = webRequest.downloadHandler?.text,
                    Headers = responseHeaders
                };
            }
        }

        private static UnityWebRequest CreateWebRequest(NotionHttpRequest request)
        {
            var webRequest = new UnityWebRequest(request.Url, request.Method);
            webRequest.timeout = 30;

            if (request.Body != null)
            {
                var bodyBytes = Encoding.UTF8.GetBytes(request.Body);
                webRequest.uploadHandler = new UploadHandlerRaw(bodyBytes);
            }

            webRequest.downloadHandler = new DownloadHandlerBuffer();

            foreach (var header in request.Headers)
            {
                webRequest.SetRequestHeader(header.Key, header.Value);
            }

            return webRequest;
        }
    }
}
