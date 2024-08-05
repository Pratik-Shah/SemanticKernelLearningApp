using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
namespace LearningApp
{
    /// <summary>
    /// Represents a class that demonstrates the usage of AgentsGroupChat feature of Semantic Kerne.
    /// </summary>
    public class AgentsGroupChatExecutor
    {

        private sealed record AgentSelectionResult(string name, string reason);
        
        private const string QueryExecutorName = "QueryExecutor";
        string QueryExecutorInstructions = "You are an expert in generating azure resource graph query and execution of the query. Dont ask user for additional input as you have sufficient permission to create and execute the resource graph query. You reply back with the result of the query execution along with resourceids.";        
        string QueryExecutorDescription = "Create and Execute Azure Resource Graph query to get resource information.";
        
        private const string RequestCoordinatorName = "RequestCoordinator";
        string RequestCoordinatorInstructions = @"You are a Coordinator responsible to handle only azure resource specific queries.
You are capable to handle information query and tagging of azure resources.
For the provided user query you must first check is the query is for resource information or tagging of the resource.
You answer only to queries related to Azure resources with help of QueryExecutor and ResourceTagger Agents whereever applicable.
You are responsible for coordinating between QueryExecutor and ResourceTagger agents replies to achieve user goal.
If not tags are provided, you must ask the user to provide the tags and end the conversation.
You must always check if the user query is related to Azure resources. If not, you must politely decline.
If no appropiate Agents can be found, let the user know you only provide responses using Agents.
Finally you must end the conversation by stating  including phrase ""GOAL_IS_ACHIEVED"".";

        string RequestCoordinatorDescription = "Coordinate between QueryExecutor and ResourceTagger agents to achieve user goal.";
        
        private const string ResourceTaggerName = "ResourceTagger";    
        string ResourceTaggerInstructions = "You are expert in tagging azure resources.Your goal is to tag azure resource with provided key and value. You must always check if resourceid and tag the resource are provided in the user request. If not ask the RequestCoordinatorAgent to confirm the resourceid of the resource to be tagged by providing required details to be queried.";
        string ResourceTaggerDescription = "Add tags to an azure resource based on resourceid and provided key and value.";

        #pragma warning disable SKEXP0110, SKEXP0001
        private const string InnerSelectionInstructions =
        $$$"""
        Select which participant will take the next turn based on the conversation history.
        
        Only choose from these participants:
        - {{{ResourceTaggerName}}}
        - {{{QueryExecutorName}}}
        - {{{RequestCoordinatorName}}}
                
        Choose the next participant according to the action of the most recent participant:
        - After user input, it is {{{RequestCoordinatorName}}} turn.
        - After {{{RequestCoordinatorName}}} if additional information is required for tagging, its {{{QueryExecutorName}}}'s turn.
        - After {{{ResourceTaggerName}}} if requires additional information for tagging, its {{{QueryExecutorName}}}'s turn.
        - After {{{ResourceTaggerName}}} completes tagging request successfully, it is {{{RequestCoordinatorName}}}'s turn.
        - After {{{QueryExecutorName}}} completes the query execution and there is no requirement for tagging a resource from user input, it is {{{RequestCoordinatorName}}}'s turn.
        
        Respond in json format.  The JSON schema can include only:
        {
            "name": "string (the name of the assistant selected for the next turn)",
            "reason": "string (the reason for the participant was selected)"
        }
        
        History:
        {{${{{KernelFunctionSelectionStrategy.DefaultHistoryVariableName}}}}}
        """;

        internal async Task MultiChatAgentInAgentGroupChat()
        {
            Console.WriteLine("Enter your query here...");
            var input = Console.ReadLine();

            var azureSettings = new AzureSettings
            {
                Endpoint = Environment.GetEnvironmentVariable("AZURE_OAI_ENDPOINT")!,
                ApiKey = Environment.GetEnvironmentVariable("AZURE_OAI_API_KEY")!,
                Deployment = Environment.GetEnvironmentVariable("AZURE_OAI_DEPLOYMENT")!
            };

            var resourceTaggerKernel = BuildKernel(azureSettings);
            // Adding the plugin to the kernel
            resourceTaggerKernel.ImportPluginFromObject(new AzureResourceAction());

            var queryExecuterKernel = BuildKernel(azureSettings);
            // Adding the plugin to the kernel
            queryExecuterKernel.ImportPluginFromObject(new AzureResourceInformationQuery());

            var requestCoordinatorKernel = BuildKernel(azureSettings);

            OpenAIPromptExecutionSettings ExecutionSettings = new() { ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions, Temperature = 0 };
            var queryExecutorAgent = CreateAgent(QueryExecutorName,QueryExecutorDescription , queryExecuterKernel, ExecutionSettings, QueryExecutorInstructions);
            var resourceTaggerAgent = CreateAgent(ResourceTaggerName, ResourceTaggerDescription, resourceTaggerKernel, ExecutionSettings, ResourceTaggerInstructions);
            var requestCoordinatorAgent = CreateAgent(RequestCoordinatorName, RequestCoordinatorDescription, requestCoordinatorKernel, ExecutionSettings, RequestCoordinatorInstructions);
                            
           // setting and kernel function specific to KernelFunctionSelectionStrategy
            OpenAIPromptExecutionSettings jsonSettings = new() { ResponseFormat = ChatCompletionsResponseFormat.JsonObject };
            KernelFunction RequestCoordinatorFunction = KernelFunctionFactory.CreateFromPrompt(InnerSelectionInstructions, jsonSettings);
 
            AgentGroupChat chat =
                new(queryExecutorAgent, resourceTaggerAgent, requestCoordinatorAgent)
                {
                    ExecutionSettings =
                        new()
                        {
                            // currently using requestCoordinatorKernel for selection strategy
                            SelectionStrategy = new KernelFunctionSelectionStrategy(RequestCoordinatorFunction,requestCoordinatorKernel)
                            {
                                ResultParser =
                                            (result) =>
                                            {
                                                AgentSelectionResult? jsonResult = JsonResultTranslator.Translate<AgentSelectionResult>(result.GetValue<string>());
                                                string? agentName = string.IsNullOrWhiteSpace(jsonResult?.name) ? null : jsonResult?.name;
                                                string? reason = string.IsNullOrWhiteSpace(jsonResult?.reason) ? null : jsonResult?.reason;
                                                agentName ??= RequestCoordinatorName;
                                                Console.WriteLine($"\t>>>> Next Agent Selected Reason: {reason}");
                                                Console.WriteLine($"\t>>>> Next Agent Selected: {agentName}");
                                                return agentName;
                                            }
                            },
                            TerminationStrategy =
                                new ApprovalTerminationStrategy()
                                {
                                    Agents = [requestCoordinatorAgent],
                                    MaximumIterations = 111,
                                    AutomaticReset = true
                                }
                        }
                };

            #pragma warning disable SKEXP0110, SKEXP0001

            chat.AddChatMessage(new ChatMessageContent(Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User, input));
            Console.WriteLine($"# {Microsoft.SemanticKernel.ChatCompletion.AuthorRole.User}: '{input}'");

            await foreach (var content in chat.InvokeAsync())
            {
                Console.WriteLine($"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'");

            }

        }

        sealed class ApprovalTerminationStrategy : TerminationStrategy
        {
            // Terminate when the final message contains the term "GOAL_IS_ACHIEVED"
            protected override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
                 => Task.FromResult(history[history.Count - 1].Content?.Contains("GOAL_IS_ACHIEVED", StringComparison.OrdinalIgnoreCase) ?? false);

        }

        private Kernel BuildKernel(AzureSettings settings)
        {
            var builder = Kernel.CreateBuilder();
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information);
            });
            builder.Services.AddAzureOpenAIChatCompletion(
                deploymentName: settings.Deployment,
                endpoint: settings.Endpoint,
                modelId: settings.Deployment,
                apiKey: settings.ApiKey
            );
            return builder.Build();
        }

        private Microsoft.SemanticKernel.Agents.ChatCompletionAgent CreateAgent(string name, string description, Kernel kernel, OpenAIPromptExecutionSettings settings, string instructions)
        {
            return new Microsoft.SemanticKernel.Agents.ChatCompletionAgent
            {
                Instructions = instructions,
                Name = name,
                Description = description,
                Kernel = kernel,
                ExecutionSettings = settings
            };
        }

        private sealed class AzureSettings
        {
            public string Endpoint { get; set; }
            public string ApiKey { get; set; }
            public string Deployment { get; set; }
        }
    }

}