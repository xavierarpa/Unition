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
using System.Threading.Tasks;

using UnityEngine.UIElements;

namespace Unition.Editor.UI
{
    public sealed class SettingsView : VisualElement
    {
        public event Action OnConnected;

        private readonly TextField _tokenField;
        private readonly Button _testButton;
        private readonly Label _statusLabel;
        private readonly EnumField _sourceField;

        public SettingsView()
        {
            AddToClassList("unition-settings");

            var card = new VisualElement();
            card.AddToClassList("unition-settings__card");
            Add(card);

            var title = new Label("Unition — Connect to Notion");
            title.AddToClassList("unition-settings__title");
            card.Add(title);

            var subtitle = new Label("Enter your Notion integration token to get started.");
            subtitle.AddToClassList("unition-settings__subtitle");
            card.Add(subtitle);

            var sourceLabel = new Label("Token Source");
            sourceLabel.AddToClassList("unition-settings__label");
            card.Add(sourceLabel);

            _sourceField = new EnumField(UnitionCredentials.CurrentSource);
            _sourceField.RegisterValueChangedCallback(evt =>
            {
                UnitionCredentials.CurrentSource = (TokenSource)evt.newValue;
                UpdateTokenFieldVisibility();
            });
            card.Add(_sourceField);

            if (UnitionCredentials.HasEnvironmentToken)
            {
                var envHint = new Label("✓ NOTION_API_TOKEN environment variable detected.");
                envHint.style.color = new StyleColor(new UnityEngine.Color(0.3f, 0.78f, 0.69f));
                envHint.style.fontSize = 11;
                envHint.style.marginBottom = 4;
                card.Add(envHint);
            }

            var tokenLabel = new Label("Integration Token");
            tokenLabel.AddToClassList("unition-settings__label");
            card.Add(tokenLabel);

            _tokenField = new TextField();
            _tokenField.isPasswordField = true;
            _tokenField.value = UnitionCredentials.Token;
            _tokenField.AddToClassList("unition-settings__token-field");
            card.Add(_tokenField);

            var securityNote = new Label("⚠ Token is encrypted in EditorPrefs with a project-derived key.");
            securityNote.style.color = new StyleColor(new UnityEngine.Color(0.86f, 0.86f, 0.67f));
            securityNote.style.fontSize = 10;
            securityNote.style.marginTop = 2;
            securityNote.style.marginBottom = 6;
            card.Add(securityNote);

            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("unition-settings__button-row");
            card.Add(buttonRow);

            if (UnitionCredentials.HasToken)
            {
                var clearBtn = new Button(OnClearClicked) { text = "Clear Token" };
                clearBtn.style.marginRight = 8;
                buttonRow.Add(clearBtn);
            }

            _testButton = new Button(OnTestClicked) { text = "Test Connection" };
            buttonRow.Add(_testButton);

            _statusLabel = new Label();
            _statusLabel.AddToClassList("unition-settings__status");
            _statusLabel.style.display = DisplayStyle.None;
            card.Add(_statusLabel);
        }

        private void UpdateTokenFieldVisibility()
        {
            var isEnv = UnitionCredentials.CurrentSource == TokenSource.EnvironmentVariable;
            _tokenField.SetEnabled(!isEnv);
            if (isEnv && UnitionCredentials.HasEnvironmentToken)
            {
                _tokenField.value = "(using environment variable)";
            }
        }

        private void OnClearClicked()
        {
            UnitionCredentials.Clear();
            UnitionEditorClient.Invalidate();
            _tokenField.value = string.Empty;
            SetStatus(null, false);
        }

        private async void OnTestClicked()
        {
            string token;
            if (UnitionCredentials.CurrentSource == TokenSource.EnvironmentVariable)
            {
                token = System.Environment.GetEnvironmentVariable("NOTION_API_TOKEN")?.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    SetStatus("NOTION_API_TOKEN environment variable is not set.", false);
                    return;
                }
            }
            else
            {
                token = _tokenField.value?.Trim();
                if (string.IsNullOrEmpty(token))
                {
                    SetStatus("Please enter a token.", false);
                    return;
                }
                UnitionCredentials.Token = token;
            }

            _testButton.SetEnabled(false);
            _testButton.text = "Testing...";
            SetStatus(null, false);

            try
            {
                UnitionEditorClient.Invalidate();

                var client = UnitionEditorClient.Client;
                var user = await client.GetCurrentUserAsync();

                SetStatus($"Connected as: {user.Name ?? user.Id}", true);
                OnConnected?.Invoke();
            }
            catch (NotionApiException ex) when (ex.IsUnauthorized)
            {
                SetStatus("Invalid token. Check your integration settings in Notion.", false);
                UnitionCredentials.Clear();
                UnitionEditorClient.Invalidate();
            }
            catch (Exception ex)
            {
                SetStatus($"Connection failed: {ex.Message}", false);
            }
            finally
            {
                _testButton.SetEnabled(true);
                _testButton.text = "Test Connection";
            }
        }

        private void SetStatus(string message, bool success)
        {
            if (string.IsNullOrEmpty(message))
            {
                _statusLabel.style.display = DisplayStyle.None;
                return;
            }

            _statusLabel.text = message;
            _statusLabel.style.display = DisplayStyle.Flex;
            _statusLabel.RemoveFromClassList("unition-settings__status--success");
            _statusLabel.RemoveFromClassList("unition-settings__status--error");
            _statusLabel.AddToClassList(success
                ? "unition-settings__status--success"
                : "unition-settings__status--error");
        }
    }
}
