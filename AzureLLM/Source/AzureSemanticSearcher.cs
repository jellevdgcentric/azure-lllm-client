using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;

namespace MyApp.Services
{
    public class AzureSemanticSearcher
    {
        private readonly SearchClient _searchClient;

        public AzureSemanticSearcher(Uri endpoint, string indexName, string apiKey)
        {
            var credential = new AzureKeyCredential(apiKey);
            _searchClient = new SearchClient(endpoint, indexName, credential);
        }

        public async Task<List<string>> SearchAsync(string query)
        {
            var options = new SearchOptions
            {
                IncludeTotalCount = true,
                Size = 10
            };
            options.Select.Add("content");

            var results = new List<string>();

            var response = await _searchClient.SearchAsync<SearchDocument>(query, options);

            await foreach (var result in response.Value.GetResultsAsync())
            {
                if (result.Document.ContainsKey("content"))
                {
                    results.Add(result.Document["content"]?.ToString() ?? string.Empty);
                }
            }

            return results;
        }
    }
}