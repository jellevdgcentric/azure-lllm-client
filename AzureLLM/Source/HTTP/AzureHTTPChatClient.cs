using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureLLM.Source.HTTP
{
    public class AzureHTTPChatClient : IChatClient
    {
        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;
        private readonly string _deploymentName;
        private readonly string _apiKey;
        private readonly string _apiVersion;
        private bool _useHistory = false;
        private readonly List<(string Role, string Content)> _history = new List<(string, string)>();

        public AzureHTTPChatClient(Uri endpoint, string deploymentName, string apiKey, string apiVersion = "2023-03-15-preview")
        {
            _endpoint = endpoint;
            _deploymentName = deploymentName;
            _apiKey = apiKey;
            _apiVersion = apiVersion;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public void UseHistory(bool useHistory, List<(string Role, string Content)> history)
        {
			if (useHistory == _useHistory && history == null)
			{
				return;
			}

			_useHistory = useHistory;
			_history.Clear();
			if (history != null)
			{
				foreach (var message in history)
				{
					_history.Add((message.Role, message.Content));
				}
			}
        }

        public async Task<string> PromptAsync(string systemMessage, string message)
        {
            var messages = BuildMessages(systemMessage, message);

            var url = new Uri(_endpoint, $"/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}");
            var requestBody = new
            {
                messages
            };
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            string reply = string.Empty;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageElement) && messageElement.TryGetProperty("content", out var contentElement))
                {
                    reply = contentElement.GetString();
                }
            }

            if (_useHistory)
            {
                _history.Add(("system", systemMessage));
                _history.Add(("user", message));
                _history.Add(("assistant", reply));
            }

            return reply;
        }

        public async Task<string> PromptStreamingAsync(string systemMessage, string message, Action<string> onDeltaReceived)
        {
            var messages = BuildMessages(systemMessage, message);

            var url = new Uri(_endpoint, $"/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}");
            var requestBody = new
            {
                messages,
                stream = true
            };
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            StringBuilder entireResponse = new StringBuilder();
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring("data: ".Length).Trim();
                    if (data == "[DONE]")
                    {
                        break;
                    }
                    using var doc = JsonDocument.Parse(data);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentElement))
                        {
                            var content = contentElement.GetString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                entireResponse.Append(content);
                                onDeltaReceived?.Invoke(content);
                            }
                        }
                    }
                }
            }

            if (_useHistory)
            {
                _history.Add(("system", systemMessage));
                _history.Add(("user", message));
                _history.Add(("assistant", entireResponse.ToString()));
            }

            return entireResponse.ToString();
        }

        private List<object> BuildMessages(string systemMessage, string message)
        {
            var messages = new List<object>();

            if (_useHistory)
            {
                foreach (var (role, content) in _history)
                {
                    messages.Add(new
                    {
                        role,
                        content = new[]
                        {
                            new { type = "text", text = content }
                        }
                    });
                }
            }

            messages.Add(new
            {
                role = "system",
                content = new[]
                {
                    new { type = "text", text = systemMessage }
                }
            });
            messages.Add(new
            {
                role = "user",
                content = new[]
                {
                    new { type = "text", text = message }
                }
            });

            return messages;
        }
    }
}