using System;
using System.Threading.Tasks;
using AzureLLM.Source;
using Microsoft.Extensions.Configuration;
using AzureLLM.Source.SDK;
using AzureLLM.Source.HTTP;

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

            IChatClient azureChatClient = new AzureHTTPChatClient(llmEndpoint, llmDeploymentName, llmApiKey);
            AzureSemanticSearcher semanticSearcher = new AzureSemanticSearcher(searchEndpoint, searchVectorDatabaseName, searchApiKey);

            //await GPTAssistent(azureChatClient);

			// await RacingAssistent(azureChatClient, semanticSearcher);
			await MultiplePeopleConversation(llmEndpoint, llmDeploymentName, llmApiKey);
		}

		private static async Task GPTAssistent(IChatClient azureChatClient)
		{
			Console.ForegroundColor = ConsoleColor.Green;
			Console.WriteLine("Hello I am a GPT assistent. Can I help you?");

			azureChatClient.UseHistory(true);

			while (true)
			{
				Console.ForegroundColor = ConsoleColor.White;
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

				Console.ForegroundColor = ConsoleColor.Green;
				await Prompt(azureChatClient, userInput);
			}
		}

		private static async Task RacingAssistent(IChatClient azureChatClient, AzureSemanticSearcher semanticSearcher)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Hello I am your iRacing assistent for today. Do you have any questions?");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
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

                Console.ForegroundColor = ConsoleColor.Green;
                await PromptWithSemanticSearch(azureChatClient, semanticSearcher, userInput);
            }
        }

        private static async Task MultiplePeopleConversation(Uri llmEndpoint, string llmDeploymentName, string llmApiKey)
        {
            IChatClient grandmaClient = new AzureHTTPChatClient(llmEndpoint, llmDeploymentName, llmApiKey);
            IChatClient grandsonClient = new AzureHTTPChatClient(llmEndpoint, llmDeploymentName, llmApiKey);

            grandmaClient.UseHistory(true);
            grandsonClient.UseHistory(true);

            string systemPromptGrandma = "You are an 85 year old grandma called Gertrude, and you're talking to your grandson called Charles through WhatsApp. Keep your responses to a single sentence. Do not end or lead to an ending of the conversation.";
            string systemPromptGrandson = "You are a 15 year old grandson called Charles, and you're talking to your grandma called Gertrude through WhatsApp. Keep your responses to a single sentence. Do not end or lead to an ending of the conversation.";

            string messageFromGrandma = "Hi, how are you today grandson?";
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Gertrude: " + messageFromGrandma);
            string responseFromGrandson = await grandsonClient.PromptAsync(systemPromptGrandson, messageFromGrandma);
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Charles: " + responseFromGrandson);

			await Task.Delay(2000);

			while (true)
            {
                string responseFromGrandma = await grandmaClient.PromptAsync(systemPromptGrandma, responseFromGrandson);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Gertrude: " + responseFromGrandma);

                string responseFromGrandsonNew = await grandsonClient.PromptAsync(systemPromptGrandson, responseFromGrandma);
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Charles: " + responseFromGrandsonNew);

                responseFromGrandson = responseFromGrandsonNew;

                await Task.Delay(2000);
            }
        }

        private static async Task<string> Prompt(IChatClient azureChatClient, string userInput)
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