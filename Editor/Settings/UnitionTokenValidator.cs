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

using UnityEditor;

using UnityEngine;

namespace Unition.Editor
{
    [InitializeOnLoad]
    internal static class UnitionTokenValidator
    {
        static UnitionTokenValidator()
        {
            if (!UnitionSettings.instance.ValidateTokenOnStartup)
            {
                return;
            }

            if (!UnitionCredentials.HasToken)
            {
                return;
            }

            EditorApplication.delayCall += ValidateToken;
        }

        private static async void ValidateToken()
        {
            try
            {
                var client = UnitionEditorClient.Client;
                if (client == null)
                {
                    return;
                }

                await client.GetCurrentUserAsync();
            }
            catch (NotionApiException ex) when (ex.IsUnauthorized)
            {
                Debug.LogWarning("[Unition] Notion API token is invalid or expired. Update it in Window > Unition > Notion Browser > ⚙");
            }
            catch (Exception)
            {
                // Network errors are expected during startup — silently ignore
            }
        }
    }
}
