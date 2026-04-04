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
using System.Threading;
using System.Threading.Tasks;

namespace Unition.Http
{
    public sealed class NotionRateLimiter
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly int _maxRequestsPerSecond;
        private DateTime _windowStart = DateTime.MinValue;
        private int _requestsInWindow;

        public NotionRateLimiter(int maxRequestsPerSecond = 3)
        {
            _maxRequestsPerSecond = maxRequestsPerSecond;
        }

        public async Task WaitAsync(CancellationToken ct = default)
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                if ((now - _windowStart).TotalSeconds >= 1.0)
                {
                    _requestsInWindow = 0;
                    _windowStart = now;
                }

                if (_requestsInWindow >= _maxRequestsPerSecond)
                {
                    var delay = _windowStart.AddSeconds(1) - now;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, ct);
                    }
                    _requestsInWindow = 0;
                    _windowStart = DateTime.UtcNow;
                }

                _requestsInWindow++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public Task WaitForRetryAfterAsync(int retryAfterSeconds, CancellationToken ct = default)
        {
            return Task.Delay(TimeSpan.FromSeconds(retryAfterSeconds), ct);
        }
    }
}
