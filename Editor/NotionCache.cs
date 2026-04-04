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

using Unition.Models;

namespace Unition.Editor
{
    public static class NotionCache
    {
        private static readonly Dictionary<string, (NotionDatabase db, DateTime expiry)> _dbCache = new();
        private static readonly Dictionary<string, (List<NotionPage> pages, DateTime expiry)> _rowCache = new();

        private const int DefaultDbTtlMinutes = 5;
        private const int DefaultRowTtlMinutes = 2;

        private static TimeSpan DbTtl = TimeSpan.FromMinutes(DefaultDbTtlMinutes);
        private static TimeSpan RowTtl = TimeSpan.FromMinutes(DefaultRowTtlMinutes);

        public static void SetTtl(int dbTtlMinutes, int rowTtlMinutes)
        {
            DbTtl = TimeSpan.FromMinutes(dbTtlMinutes);
            RowTtl = TimeSpan.FromMinutes(rowTtlMinutes);
        }

        public static bool TryGetDatabase(string databaseId, out NotionDatabase database)
        {
            if (_dbCache.TryGetValue(databaseId, out var entry) && DateTime.UtcNow < entry.expiry)
            {
                database = entry.db;
                return true;
            }

            database = null;
            return false;
        }

        public static void SetDatabase(string databaseId, NotionDatabase database)
        {
            _dbCache[databaseId] = (database, DateTime.UtcNow + DbTtl);
        }

        public static bool TryGetRows(string databaseId, out List<NotionPage> pages)
        {
            if (_rowCache.TryGetValue(databaseId, out var entry) && DateTime.UtcNow < entry.expiry)
            {
                pages = entry.pages;
                return true;
            }

            pages = null;
            return false;
        }

        public static void SetRows(string databaseId, List<NotionPage> pages)
        {
            _rowCache[databaseId] = (pages, DateTime.UtcNow + RowTtl);
        }

        public static void InvalidateDatabase(string databaseId)
        {
            _dbCache.Remove(databaseId);
        }

        public static void InvalidateRows(string databaseId)
        {
            _rowCache.Remove(databaseId);
        }

        public static void InvalidateAll()
        {
            _dbCache.Clear();
            _rowCache.Clear();
        }
    }
}
