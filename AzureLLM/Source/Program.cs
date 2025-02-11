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

            await semanticSearcher.SearchAsync("ilab");

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

                if (UseStreaming)
                {
                    Console.Write("AI: ");
                    await azureChatClient.PromptStreamingAsync(userInput, (response) =>
                    {
                        Console.Write(response);
                    });
                    Console.WriteLine();
                }
                else
                {
                    Console.Write("AI: ");
                    string response = await azureChatClient.PromptAsync(userInput);
                    Console.WriteLine(response);
                }
            }
        }
    }
}