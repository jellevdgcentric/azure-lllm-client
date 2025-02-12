using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureLLM.Source
{
	public interface ISemanticSearcher
	{
		public Task<List<string>> SearchAsync(string query, int maxResults = 10);
	}
}
