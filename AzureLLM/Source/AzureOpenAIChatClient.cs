using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Linq;
using System.ClientModel;
using AzureLLM.Source;

namespace MyApp.Services
{
	public class AzureOpenAIChatClient : IChatClient
	{
		private readonly ChatClient _chatClient;

		public AzureOpenAIChatClient(Uri endpoint, string deploymentName, string apiKey)
		{
			var azureOpenAIClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey), new AzureOpenAIClientOptions());

			_chatClient = azureOpenAIClient.GetChatClient(deploymentName);
		}

		public async Task<string> PromptAsync(string systemMessage, string message)
		{
			var messages = new List<ChatMessage>
			{
				new SystemChatMessage(systemMessage),
				new UserChatMessage(message)
			};

			ClientResult<ChatCompletion> response = await _chatClient.CompleteChatAsync(messages);
			ChatMessageContent content = response.Value.Content;
			return string.Concat(content.Select(part => part.Text));
		}

		public async Task<string> PromptStreamingAsync(string systemMessage, string message, Action<string> onDeltaReceived)
		{
			var messages = new List<ChatMessage>
			{
				new SystemChatMessage(systemMessage),
				new UserChatMessage(message)
			};

			StringBuilder entireResponse = new StringBuilder();
			await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(messages))
			{
				foreach (ChatMessageContentPart part in update.ContentUpdate)
				{
					entireResponse.Append(part.Text);
					onDeltaReceived?.Invoke(part.Text);
				}
			}

			return entireResponse.ToString();
		}
	}
}