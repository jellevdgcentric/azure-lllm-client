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

		public async Task<string> PromptAsync(string systemPrompt, string userPrompt)
        {
            var prompts = BuildPromptWithHistory(systemPrompt, userPrompt);

            ClientResult<ChatCompletion> response = await _chatClient.CompleteChatAsync(prompts);
            ChatMessageContent contentResult = response.Value.Content;
            string reply = string.Concat(contentResult.Select(part => part.Text));

            if (_useHistory)
            {
                _history.Add((Roles.USER_ROLE, userPrompt));
                _history.Add((Roles.ASSISTANT_ROLE, reply));
            }

            return reply;
        }

        public async Task<string> PromptStreamingAsync(string systemPrompt, string userPrompt, Action<string> onDeltaReceived)
        {
            var messages = BuildPromptWithHistory(systemPrompt, userPrompt);

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
                _history.Add((Roles.USER_ROLE, userPrompt));
                _history.Add((Roles.ASSISTANT_ROLE, entireResponse.ToString()));
            }

            return entireResponse.ToString();
        }

        private List<ChatMessage> BuildPromptWithHistory(string systemPrompt, string message)
        {
            var messages = new List<ChatMessage>();

            if (_useHistory)
            {
                foreach (var (role, content) in _history)
                {
                    if (role == Roles.SYSTEM_ROLE)
                        messages.Add(new SystemChatMessage(content));
                    else if (role == Roles.USER_ROLE)
                        messages.Add(new UserChatMessage(content));
                    else if (role == Roles.ASSISTANT_ROLE)
                        messages.Add(new AssistantChatMessage(content));
                }
            }

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new SystemChatMessage(systemPrompt));
            }

            messages.Add(new UserChatMessage(message));

            return messages;
        }
    }
}