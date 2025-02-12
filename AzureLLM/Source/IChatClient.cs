using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureLLM.Source
{
	public interface IChatClient
	{
		public void UseHistory(bool useHistory, List<(string Role, string Content)> history = null);
		public Task<string> PromptAsync(string systemPrompt, string userPrompt);
		public Task<string> PromptStreamingAsync(string systemPrompt, string userPrompt, Action<string> onDeltaReceived);
	}
}