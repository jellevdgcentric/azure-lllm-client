using System;
using System.Threading.Tasks;
using AzureLLM.Source;
using Microsoft.Extensions.Configuration;
using AzureLLM.Source.SDK;
using AzureLLM.Source.HTTP;
using System.Collections.Generic;
using System.Linq;

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
            string llmDeploymentName = azureLLMConfig["DeploymentName"];
            string llmApiKey = azureLLMConfig["ApiKey"];

            Uri searchEndpoint = new Uri(azureSearchConfig["Endpoint"]);
            string searchVectorDatabaseName = azureSearchConfig["VectorDatabaseName"];
            string searchApiKey = azureSearchConfig["ApiKey"];

            IChatClient azureChatClient = new AzureChatClient(llmEndpoint, llmDeploymentName, llmApiKey);
            azureChatClient.UseHistory(true);
            ISemanticSearcher semanticSearcher = new AzureSemanticSearcher(searchEndpoint, searchVectorDatabaseName, searchApiKey);

            await GPTAssistant(azureChatClient);

            //await RacingAssistant(azureChatClient, semanticSearcher);
            //await GrandmaAndGrandsonWhatsappConversation(llmEndpoint, llmDeploymentName, llmApiKey);
        }

        private static async Task GPTAssistant(IChatClient azureChatClient)
        {
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine("Hello I am a GPT assistant. Can I help you?");

            azureChatClient.UseHistory(true);

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Black;
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

                Console.ForegroundColor = ConsoleColor.DarkBlue;
                await Prompt(azureChatClient, "You are a GPT assistant and try to answer the user its questions.", userInput);
            }
        }

        private static async Task<string> Prompt(IChatClient azureChatClient, string systemPrompt, string userInput)
        {
            if (UseStreaming)
            {
                Console.Write("AI: ");
                string response = await azureChatClient.PromptStreamingAsync(string.Empty, userInput, Console.Write);
                Console.WriteLine();
                return response;
            }
            else
            {
                Console.Write("AI: ");
                string response = await azureChatClient.PromptAsync(string.Empty, userInput);
                Console.WriteLine(response);
                return response;
            }
        }

        private static async Task PromptWithSemanticSearch(IChatClient azureChatClient, ISemanticSearcher semanticSearcher, string systemPrompt, string userInput)
        {
            List<string> semanticSearchResult = await semanticSearcher.SearchAsync(userInput);
            string formattedSearchResults = "### Retrieved Information:\n" +
                string.Join("\n", semanticSearchResult.Select((result, index) => $"{index + 1}. {result}")) +
                "\n\n### User Query:\n" +
                userInput +
                "\n\nBased on the above information, provide a concise and relevant answer to the user's query. Respond in the language based on the user query.";

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            if (UseStreaming)
            {
                Console.Write("AI: ");
                await azureChatClient.PromptStreamingAsync(string.Empty, formattedSearchResults, (response) =>
                {
                    Console.Write(response);
                });
                Console.WriteLine();
            }
            else
            {
                Console.Write("AI: ");
                string response = await azureChatClient.PromptAsync(string.Empty, formattedSearchResults);
                Console.WriteLine(response);
            }
        }

		private static async Task RacingAssistant(IChatClient azureChatClient, ISemanticSearcher semanticSearcher)
		{
			Console.ForegroundColor = ConsoleColor.DarkGreen;
			Console.WriteLine("Hello I am your iRacing assistant for today. Do you have any questions?");

			while (true)
			{
				Console.ForegroundColor = ConsoleColor.Black;
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

				Console.ForegroundColor = ConsoleColor.DarkGreen;
				await PromptWithSemanticSearch(azureChatClient, semanticSearcher, "You are an iRacing virtual assistant and try to help the user answer questions.", userInput);
			}
		}

		private static async Task GrandmaAndGrandsonWhatsappConversation(Uri llmEndpoint, string llmDeploymentName, string llmApiKey)
		{
			IChatClient grandmaClient = new AzureChatClient(llmEndpoint, llmDeploymentName, llmApiKey);
			IChatClient grandsonClient = new AzureChatClient(llmEndpoint, llmDeploymentName, llmApiKey);

			string systemPromptGrandma = "You are an 85 year old grandma called Gertrude, and you're talking to your grandson called Charles. Do not end or lead to an ending of the conversation.";
			string systemPromptGrandson = "You are a 15 year old grandson called Charles, and you're talking to your grandma called Gertrude. Do not end or lead to an ending of the conversation.";

			string messageFromGrandma = "Hi Charles, how are you today?";

			grandmaClient.UseHistory(true, new List<(string, string)> { ("user", messageFromGrandma) });
			grandsonClient.UseHistory(true);

			Console.ForegroundColor = ConsoleColor.DarkMagenta;
			Console.WriteLine("Gertrude: " + messageFromGrandma);
			string responseFromGrandson = await grandsonClient.PromptAsync(systemPromptGrandson, messageFromGrandma);
			Console.ForegroundColor = ConsoleColor.DarkRed;
			Console.WriteLine("Charles: " + responseFromGrandson);

			while (true)
			{
				Console.ReadLine();

				string responseFromGrandma = await grandmaClient.PromptAsync(systemPromptGrandma, responseFromGrandson);

				Console.ForegroundColor = ConsoleColor.DarkMagenta;
				Console.WriteLine("Gertrude: " + responseFromGrandma);

				string responseFromGrandsonNew = await grandsonClient.PromptAsync(systemPromptGrandson, responseFromGrandma);
				Console.ForegroundColor = ConsoleColor.DarkRed;
				Console.WriteLine("Charles: " + responseFromGrandsonNew);

				responseFromGrandson = responseFromGrandsonNew;
			}
		}
	}
}