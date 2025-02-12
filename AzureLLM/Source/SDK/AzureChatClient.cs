using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Linq;
using System.ClientModel;

namespace AzureLLM.Source.SDK
{
    public class AzureChatClient : IChatClient
    {
        private readonly ChatClient _chatClient;
        private bool _useHistory = false;
        private readonly List<(string Role, string Content)> _history = new List<(string, string)>();

        public AzureChatClient(Uri endpoint, string deploymentName, string apiKey)
        {
            var azureOpenAIClient = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey), new AzureOpenAIClientOptions());

            _chatClient = azureOpenAIClient.GetChatClient(deploymentName);
        }

		public void UseHistory(bool useHistory, List<(string Role, string Content)> history = null)
		{
            if (useHistory == _useHistory && history == null)
            {
                return;
            }

			_useHistory = useHistory;
			_history.Clear();
			if (history != null)
			{
				foreach (var (role, content) in history)
				{
					_history.Add((role, content));
				}
			}
		}

		public async Task<string> PromptAsync(string systemMessage, string message)
        {
            var messages = BuildMessages(systemMessage, message);

            ClientResult<ChatCompletion> response = await _chatClient.CompleteChatAsync(messages);
            ChatMessageContent contentResult = response.Value.Content;
            string reply = string.Concat(contentResult.Select(part => part.Text));

            if (_useHistory)
            {
                _history.Add(("user", message));
                _history.Add(("assistant", reply));
            }

            return reply;
        }

        public async Task<string> PromptStreamingAsync(string systemMessage, string message, Action<string> onDeltaReceived)
        {
            var messages = BuildMessages(systemMessage, message);

            StringBuilder entireResponse = new StringBuilder();
            await foreach (StreamingChatCompletionUpdate update in _chatClient.CompleteChatStreamingAsync(messages))
            {
                foreach (ChatMessageContentPart part in update.ContentUpdate)
                {
                    entireResponse.Append(part.Text);
                    onDeltaReceived?.Invoke(part.Text);
                }
            }

            if (_useHistory)
            {
                _history.Add(("user", message));
                _history.Add(("assistant", entireResponse.ToString()));
            }

            return entireResponse.ToString();
        }

        private List<ChatMessage> BuildMessages(string systemMessage, string message)
        {
            var messages = new List<ChatMessage>();

            if (_useHistory)
            {
                foreach (var (role, content) in _history)
                {
                    if (role == "system")
                        messages.Add(new SystemChatMessage(content));
                    else if (role == "user")
                        messages.Add(new UserChatMessage(content));
                    else if (role == "assistant")
                        messages.Add(new AssistantChatMessage(content));
                }
            }

            if (!string.IsNullOrEmpty(systemMessage))
            {
                messages.Add(new SystemChatMessage(systemMessage));
            }

            messages.Add(new UserChatMessage(message));

            return messages;
        }
    }
}