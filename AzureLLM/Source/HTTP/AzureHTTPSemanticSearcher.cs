using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AzureLLM.Source.HTTP
{
    public class AzureHTTPSemanticSearcher : ISemanticSearcher
	{
        private readonly HttpClient _httpClient;
        private readonly Uri _endpoint;
        private readonly string _indexName;
        private readonly string _apiKey;
        private readonly string _apiVersion;

        public AzureHTTPSemanticSearcher(Uri endpoint, string indexName, string apiKey, string apiVersion = "2024-11-01-preview")
        {
            _endpoint = endpoint;
            _indexName = indexName;
            _apiKey = apiKey;
            _apiVersion = apiVersion;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("api-key", _apiKey);
        }

		public async Task<List<string>> SearchAsync(string query, int maxResults = 10)
		{
			var baseEndpoint = _endpoint.ToString().TrimEnd('/');
			var searchUri = new Uri($"{baseEndpoint}/indexes/{_indexName}/docs/search?api-version={_apiVersion}");

			var requestBody = new
			{
				search = query,
				top = maxResults
			};

			var json = JsonSerializer.Serialize(requestBody);
			using var request = new HttpRequestMessage(HttpMethod.Post, searchUri);
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");

			using var response = await _httpClient.SendAsync(request);
			response.EnsureSuccessStatusCode();

			var responseString = await response.Content.ReadAsStringAsync();
			var results = new List<string>();

			using var doc = JsonDocument.Parse(responseString);
			if (doc.RootElement.TryGetProperty("value", out var value))
			{
				foreach (var item in value.EnumerateArray())
				{
					if (item.TryGetProperty("content", out var content))
					{
						results.Add(content.GetString());
					}
				}
			}

			return results;
		}
	}
}