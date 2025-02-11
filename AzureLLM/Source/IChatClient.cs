using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureLLM.Source
{
	public interface IChatClient
	{
		public Task<string> PromptAsync(string systemMessage, string message);
		public Task PromptStreamingAsync(string systemMessage, string message, Action<string> onDeltaReceived);
	}
}