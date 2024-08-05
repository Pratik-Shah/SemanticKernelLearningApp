using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace LearningApp;

public class AzureResourceInformationQuery
{
        
        private ArmClient _armClient;        

        public AzureResourceInformationQuery()
        {
            _armClient = new ArmClient(new DefaultAzureCredential());        
        }
        
        [KernelFunction, Description("Execute the provided resource graph query from user")]
        async public Task<string> ExecuteAzureResourceGraphQuery(
            [Description("Resource graph query")] string resource_graph_query            
        )
        {
            Console.WriteLine("Identified plugin: AzureGraphQueryExecutor");
            Console.WriteLine($"Identified Query: {resource_graph_query}");

            var tenant = _armClient.GetTenants().First();

            ResourceQueryContent queryContent = new ResourceQueryContent(resource_graph_query);

            var result = await ResourceGraphExtensions.GetResourcesAsync(tenant, queryContent);
                        
            return result.GetRawResponse().Content.ToString();
        }

}