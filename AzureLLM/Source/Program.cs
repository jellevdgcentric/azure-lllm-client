using System;
using System.Threading.Tasks;
using AzureLLM.Source;
using MyApp.Services;
using Microsoft.Extensions.Configuration;

namespace MyApp
{
    class Program
    {
        private static bool UseStreaming = true;

        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var azureLLMConfig = config.GetSection("AzureLLM");
            var azureSearchConfig = config.GetSection("AzureSearch");

            Uri llmEndpoint = new Uri(azureLLMConfig["Endpoint"]);
            string deploymentName = azureLLMConfig["DeploymentName"];
            string llmApiKey = azureLLMConfig["ApiKey"];

            Uri searchEndpoint = new Uri(azureSearchConfig["Endpoint"]);
            string vectorDatabaseName = azureSearchConfig["VectorDatabaseName"];
            string searchApiKey = azureSearchConfig["ApiKey"];

            IChatClient azureChatClient = new AzureOpenAIChatClient(llmEndpoint, deploymentName, llmApiKey);
            AzureSemanticSearcher semanticSearcher = new AzureSemanticSearcher(searchEndpoint, vectorDatabaseName, searchApiKey);

            Console.WriteLine("Enter your messages below. Type 'exit' to quit.");

            while (true)
			{
				Console.Write("You: ");
				string? userInput = Console.ReadLine();

				if (userInput == null || userInput.Trim().ToLower() == "exit")
				{
					break;
				}

				if (userInput.Trim() == string.Empty)
				{
					continue;
				}

				if (userInput.Trim().ToLower() == "clear")
				{
					Console.Clear();
					continue;
				}

				await PromptWithSemanticSearch(azureChatClient, semanticSearcher, userInput);
			}
		}

		private static async Task Prompt(IChatClient azureChatClient, string userInput)
		{
			if (UseStreaming)
			{
				Console.Write("AI: ");
				await azureChatClient.PromptStreamingAsync(string.Empty, userInput, (response) =>
				{
					Console.Write(response);
				});
				Console.WriteLine();
			}
			else
			{
				Console.Write("AI: ");
				string response = await azureChatClient.PromptAsync(string.Empty, userInput);
				Console.WriteLine(response);
			}
		}

		private static async Task PromptWithSemanticSearch(IChatClient azureChatClient, AzureSemanticSearcher semanticSearcher, string userInput)
		{
			List<string> semanticSearchResult = await semanticSearcher.SearchAsync(userInput);
			string semanticSerializedResult = "----- Semantic search result: " + string.Join(", ", semanticSearchResult) + "-----\n\n";
			File.WriteAllText("STEP1.txt", semanticSerializedResult);

			string promptUserWithSemanticSearch = semanticSerializedResult + userInput;

			if (UseStreaming)
			{
				Console.Write("AI: ");
				await azureChatClient.PromptStreamingAsync(string.Empty, userInput + semanticSerializedResult, (response) =>
				{
					Console.Write(response);
				});
				Console.WriteLine();
			}
			else
			{
				Console.Write("AI: ");
				string response = await azureChatClient.PromptAsync(string.Empty, userInput + semanticSerializedResult);
				Console.WriteLine(response);
			}
		}
	}
}