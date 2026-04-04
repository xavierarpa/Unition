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

namespace Unition.Editor.Sync
{
    public sealed class NotionSyncResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Deleted { get; set; }
        public int Skipped { get; set; }
        public int PushedBack { get; set; }
        public int Conflicts { get; set; }
        public List<string> Errors { get; } = new List<string>();

        public int Total => Created + Updated + Deleted + Skipped + PushedBack + Conflicts;
        public bool HasErrors => Errors.Count > 0;
        public bool Success => !HasErrors;

        public string ToSummary()
        {
            var summary = $"Created: {Created}, Updated: {Updated}, Deleted: {Deleted}, Skipped: {Skipped}, PushedBack: {PushedBack}, Conflicts: {Conflicts}";
            if (HasErrors)
            {
                summary += $"\nErrors ({Errors.Count}):";
                for (int i = 0; i < Errors.Count && i < 10; i++)
                {
                    summary += $"\n  - {Errors[i]}";
                }
                if (Errors.Count > 10)
                {
                    summary += $"\n  ... and {Errors.Count - 10} more";
                }
            }
            return summary;
        }
    }
}
