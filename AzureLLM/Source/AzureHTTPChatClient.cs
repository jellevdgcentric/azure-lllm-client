using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AzureLLM.Source;

namespace MyApp.Services
{
    public class AzureHTTPChatClient : IChatClient
	{
        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;
        private readonly string _deploymentName;
        private readonly string _apiKey;
        private readonly string _apiVersion;

        public AzureHTTPChatClient(Uri endpoint, string deploymentName, string apiKey, string apiVersion = "2023-03-15-preview")
        {
            _endpoint = endpoint;
            _deploymentName = deploymentName;
            _apiKey = apiKey;
            _apiVersion = apiVersion;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

        public async Task<string> PromptAsync(string systemMessage, string message)
        {
            var url = new Uri(_endpoint, $"/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}");
            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = new[]
                        {
                            new { type = "text", text = systemMessage }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { type = "text", text = message }
                        }
                    }
                }
            };
            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            var root = doc.RootElement;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var messageElement) && messageElement.TryGetProperty("content", out var contentElement))
				{
					return contentElement.GetString();
				}
            }
            return string.Empty;
        }

        public async Task<string> PromptStreamingAsync(string systemMessage, string message, Action<string> onDeltaReceived)
        {
            var url = new Uri(_endpoint, $"/openai/deployments/{_deploymentName}/chat/completions?api-version={_apiVersion}");
            var requestBody = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = new[]
                        {
                            new { type = "text", text = systemMessage }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new { type = "text", text = message }
                        }
                    }
                },
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

            return entireResponse.ToString();
        }
    }
}