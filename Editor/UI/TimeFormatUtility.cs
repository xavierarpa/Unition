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

namespace Unition.Editor.UI
{
    internal static class TimeFormatUtility
    {
        internal static string FormatRelative(string isoTime)
        {
            if (string.IsNullOrEmpty(isoTime))
            {
                return "—";
            }
            if (DateTime.TryParse(isoTime, out var dt))
            {
                var diff = DateTime.UtcNow - dt.ToUniversalTime();
                if (diff.TotalSeconds < 0)
                {
                    return "just now";
                }
                if (diff.TotalMinutes < 1)
                {
                    return "just now";
                }
                if (diff.TotalHours < 1)
                {
                    return $"{(int)diff.TotalMinutes}m ago";
                }
                if (diff.TotalDays < 1)
                {
                    return $"{(int)diff.TotalHours}h ago";
                }
                if (diff.TotalDays < 30)
                {
                    return $"{(int)diff.TotalDays}d ago";
                }
                return dt.ToLocalTime().ToString("g");
            }
            return isoTime;
        }
    }
}
