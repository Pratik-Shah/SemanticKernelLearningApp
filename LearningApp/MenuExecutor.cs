using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.Experimental.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Planning.Handlebars;

namespace LearningApp
{


    /// <summary>
    /// Represents a class that demonstrates the usage of Azure OpenAI Chat Completion.
    /// </summary>
    public class MenuExecutor
    {
     
        /// <summary>
        /// Gets the kernel instance.
        /// </summary>
        private readonly Kernel kernelInstance;

        /// <summary>
        /// Initializes a new instance of the <see cref="MenuExecutor"/> class.
        /// </summary>
        public MenuExecutor()
        {
            // Get environment variables
            var AZURE_OAI_ENDPOINT = Environment.GetEnvironmentVariable("AZURE_OAI_ENDPOINT")!;
            var AZURE_OAI_API_KEY = Environment.GetEnvironmentVariable("AZURE_OAI_API_KEY")!;
            var AZURE_OAI_DEPLOYMENT = Environment.GetEnvironmentVariable("AZURE_OAI_DEPLOYMENT")!;

            var builder = Kernel.CreateBuilder();

            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole().SetMinimumLevel(LogLevel.Information);
            });

            // Add Azure OpenAI Chat Completion service to the kernel
            builder.Services.AddAzureOpenAIChatCompletion(
                deploymentName: AZURE_OAI_DEPLOYMENT,
                endpoint: AZURE_OAI_ENDPOINT,
                modelId: AZURE_OAI_DEPLOYMENT,
                apiKey: AZURE_OAI_API_KEY
            );

            // Build the kernel
            kernelInstance = builder.Build();
            AddSemanticKernelPlugins();
        }

        private void AddSemanticKernelPlugins()
        {
            // Native Plugin
            kernelInstance.ImportPluginFromObject(new AzureResourceInformationQuery());
            kernelInstance.ImportPluginFromObject(new AzureResourceAction());
            // Prompt Plugin. Also known as Sematnic Plugin
            kernelInstance.ImportPluginFromPromptDirectory(AppConstants.PluginPath);

        }

        public void DisplayKernelPlugins()
        {
            Console.WriteLine($"Total Plugins: {kernelInstance.Plugins.Count}");
            Console.WriteLine("Plugin Name\tFunction Count");

            foreach (var plugin in kernelInstance.Plugins)
            {
                Console.WriteLine($"{plugin.Name}\t{plugin.FunctionCount}");
                foreach (var function in plugin.GetFunctionsMetadata())
                {
                    Console.WriteLine($"\tFunction Name: {function.Name}");
                    Console.WriteLine($"\tFunction Description: {function.Description}");
                }
            }
        }

        /// <summary>
        /// Executes the kernel with a prompt and prints the result to the console.
        /// </summary>
        /// <param name="kernel">The kernel to execute.</param>
        public async Task KernelInvocation(string userInput)
        {
            var result = await kernelInstance.InvokePromptAsync(userInput, new KernelArguments(
                new OpenAIPromptExecutionSettings()
                {
                    ToolCallBehavior = ToolCallBehavior.EnableKernelFunctions
                }
            ));

            foreach (var metadata in result.Metadata!)
            {
                Console.WriteLine("Metadata Name: " + metadata.Key);
                Console.WriteLine("Metadata Value: " + metadata.Value);
                if (metadata.Value is List<ChatCompletionsFunctionToolCall> functionCalls)
                {
                    foreach (var functionCall in functionCalls)
                    {
                        Console.WriteLine($"Function Name: {functionCall.Name}");
                        Console.WriteLine($"Function Arguments: {string.Join(", ", functionCall.Arguments)}");
                    }
                }
            }

            Console.WriteLine(result);
        }

        // <summary>
        // Executes the Semantic Kernel with Azure OpenAI.
        // </summary>
        // <param name="userInput">The user input.</param>
        // <returns>A task representing the asynchronous operation.</returns>        
        internal async Task ExecuteSemanticKernelWithAzureOpenAI(string userInput)
        {

            OpenAIPromptExecutionSettings settings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0
            };

            var chat = kernelInstance.GetRequiredService<IChatCompletionService>();

            ChatHistory history = [];

            history.AddSystemMessage(@"You are a Azure Resource Management Agent.
            You are responsible for managing Azure resources and responding to user queries.
            You must only reply to user queries related to Azure resources.
            For non Azure resource related queries, you must politely decline.");
            history.AddUserMessage(userInput);

            var response = await chat.GetChatMessageContentAsync(
                history,
                kernel: kernelInstance,
                executionSettings: settings
            );

            foreach (var metadata in response.Metadata)
            {
                Console.WriteLine("Metadata Name: " + metadata.Key);
                Console.WriteLine("Metadata Value: " + metadata.Value);
                if (metadata.Value is List<ChatCompletionsFunctionToolCall> functionCalls)
                {
                    foreach (var functionCall in functionCalls)
                    {
                        Console.WriteLine($"Function Name: {functionCall.Name}");
                        Console.WriteLine($"Function Arguments: {string.Join(", ", functionCall.Arguments)}");
                    }
                }
            }
            Console.WriteLine(response);
        }

        internal async Task ExecuteSemanticKernelWithStepWisePlannerAzureOpenAI(string userInput)
        {
            // Native Plugin            
#pragma warning disable SKEXP0060
            var planner = new FunctionCallingStepwisePlanner(

                new FunctionCallingStepwisePlannerOptions()
                {
                    MaxIterations = 10,
                    ExecutionSettings = new OpenAIPromptExecutionSettings()
                    {
                        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                        Temperature = 0,
                        ChatSystemPrompt = "You are a Azure Resource Management Agent. You are responsible for managing Azure resources and responding to user queries. You must only reply to user queries related to Azure resources. For non Azure resource related queries, you must politely decline."
                    }
                }
            );


            var result = await planner.ExecuteAsync(kernelInstance, userInput);

            foreach (var chat in result.ChatHistory!)
            {
                Console.WriteLine(chat);
            }
            Console.WriteLine($"Final Answer {result.FinalAnswer}");
            Console.WriteLine($"Number of iteration {result.Iterations}");

        }

        internal async Task ExecuteSemanticKernelWithHandlebarsPlannerAzureOpenAI(string userinput)
        {
            var planner = new HandlebarsPlanner(new HandlebarsPlannerOptions()
            {
                ExecutionSettings = new OpenAIPromptExecutionSettings()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0,
                    ChatSystemPrompt = "You are a Azure Resource Management Agent. You are responsible for managing Azure resources and responding to user queries. You must only reply to user queries related to Azure resources. For non Azure resource related queries, you must politely decline."
                },
                AllowLoops = true
            });

            var plan = await planner.CreatePlanAsync(kernelInstance, userinput);

            File.WriteAllText($"{AppConstants.BasePath}\\Planner\\plan.html", plan.ToString());

            Console.WriteLine($"Plan generated successfully. Please check the plan at {AppConstants.BasePath}\\Planner\\plan.html");


        }

        internal async Task SingleAgentExecution(string userInput)
        {
#pragma warning disable SKEXP0101 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            AgentBuilder agentBuilder = new AgentBuilder();

            KernelPlugin azure_plugin = KernelPluginFactory.CreateFromObject(new AzureResourceInformationQuery());

            var singleAgent = await agentBuilder.WithAzureOpenAIChatCompletion(
            Environment.GetEnvironmentVariable("AZURE_OAI_ENDPOINT")!,
            Environment.GetEnvironmentVariable("AZURE_OAI_DEPLOYMENT")!,
            Environment.GetEnvironmentVariable("AZURE_OAI_API_KEY")!)
            .WithInstructions("Execute azure resource graph query.")
            .WithName("QueryExecutor")
            .WithPlugin(azure_plugin)
            .WithDescription("Execute Azure Resource Graph query.")
            .BuildAsync();

            IAgentThread _agentsThread = await singleAgent.NewThreadAsync();

            try
            {
                OpenAIPromptExecutionSettings settings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 0
                };

                var responseMessages =
                    await _agentsThread.InvokeAsync(singleAgent, userInput, new(settings)).ToArrayAsync();

                DisplayMessages(responseMessages, singleAgent);
            }
            finally
            {
                await CleanUpAsync(_agentsThread, singleAgent);
            }
        }

        private void DisplayMessages(IEnumerable<IChatMessage> messages, IAgent? agent = null)
        {
            foreach (var message in messages)
            {
                DisplayMessage(message, agent);
            }
        }

        private void DisplayMessage(IChatMessage message, IAgent? agent = null)
        {

            if (agent != null)
            {
                Console.WriteLine($"# {message.Role}:  {message.Content}");
            }
            else
            {
                Console.WriteLine($"# {message.Role}: {message.Content}");
            }
        }
        private async Task CleanUpAsync(IAgentThread _agentsThread, IAgent _agent)
        {
            if (_agentsThread != null)
            {
                _agentsThread.DeleteAsync();
                _agentsThread = null;
            }

            if (_agent != null)
            {
                _agent.DeleteAsync();
            }
        }
        internal async Task AgentDelegationExecution()
        {
            Console.WriteLine("Bootstrapping the agents...");
#pragma warning disable SKEXP0101 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            AgentBuilder agentBuilder = new AgentBuilder();

            KernelPlugin azure_query_plugin = KernelPluginFactory.CreateFromObject(new AzureResourceInformationQuery());

            KernelPlugin action_plugin = KernelPluginFactory.CreateFromObject(new AzureResourceAction());

            var queryExecutorAgent = await new AgentBuilder().WithAzureOpenAIChatCompletion(
            Environment.GetEnvironmentVariable("AZURE_OAI_ENDPOINT")!,
            Environment.GetEnvironmentVariable("AZURE_OAI_DEPLOYMENT")!,
            Environment.GetEnvironmentVariable("AZURE_OAI_API_KEY")!)
            .WithInstructions(@"You are an expert in generating azure resource graph query and execution of the query. You reply back with the result of the query execution along with resourceids.")
            .WithName("QueryExecutor")
            .WithPlugin(azure_query_plugin)
            .WithDescription("Create and Execute Azure Resource Graph query to get resource information.")
            .BuildAsync();

            var resourceTaggerAgent = await new AgentBuilder().WithAzureOpenAIChatCompletion(
            Environment.GetEnvironmentVariable("AZURE_OAI_ENDPOINT")!,
            Environment.GetEnvironmentVariable("AZURE_OAI_DEPLOYMENT")!,
            Environment.GetEnvironmentVariable("AZURE_OAI_API_KEY")!)
            .WithInstructions(@"You are expert in tagging azure resources.Your goal is to tag azure resource with provided key and value. You must always check if resourceid and tag the resource are provided in the user request. If not ask the RequestCoordinatorAgent to confirm the resourceid of the resource to be tagged by providing required details to be queried.            
            ")
            .WithName("ResourceTagger")
            .WithPlugin(action_plugin)
            .WithDescription("Add tags to an azure resource based on resourceid and provided key and value.")
            .BuildAsync();


            string instructions = @"You are a Coordinator responsible to handle only azure resource specific queries.
You answer only to queries related to Azure resources with help of QueryExecutor and ResourceTagger whereever applicable.
You are responsible for coordinating between QueryExecutor and ResourceTagger agents replies to achieve user goal.
You must always check if the user query is related to Azure resources. If not, you must politely decline.
If no appropiate tool can be found, let the user know you only provide responses using tools.
When responding always have the below format:
REQUEST: <request goes here>
<AGENTNAME>: <response goes here>
On completion of the goal you must ensure to report the entire chain of history to the user in ascending order.

[example]:
REQUEST: Create a query to list all the VMs in the subscription.
QueryExecutor : Here is the query to list all the VMs in the subscription.
ResourceTagger : Tagging is done successfully.
RequestCoordinator : The goal is achieved.
";

            var requestCoordinatorAgent =
                        await new AgentBuilder()
                            .WithAzureOpenAIChatCompletion(
                             Environment.GetEnvironmentVariable("AZURE_OAI_ENDPOINT")!,
                             Environment.GetEnvironmentVariable("AZURE_OAI_DEPLOYMENT")!,
                             Environment.GetEnvironmentVariable("AZURE_OAI_API_KEY")!)
                            .WithInstructions(instructions)
                            .WithName("RequestCoordinator")
                            .WithDescription("Coordinate between QueryExecutor and ResourceTagger agents to achieve user goal.")
                            .WithPlugin(queryExecutorAgent.AsPlugin())
                            .WithPlugin(resourceTaggerAgent.AsPlugin())
                            .BuildAsync();

            var messages = new int[]
            {
                1,2,3
            };

            // note that threads aren't attached to specific agents
            Console.WriteLine("Bootstrapping completed....");
            IAgentThread _agentsThread = await requestCoordinatorAgent.NewThreadAsync();

            try
            {


                foreach (var message in messages)
                {
                    Console.WriteLine("Enter your query here...");
                    var msg = Console.ReadLine();

                    var responseMessages =
                    await _agentsThread.InvokeAsync(requestCoordinatorAgent, msg!).ToArrayAsync();
                    Console.WriteLine($"Response from the agents {responseMessages.Count()}");
                    DisplayMessages(responseMessages, null);
                }
            }
            finally
            {
                await CleanUpAsync(_agentsThread, requestCoordinatorAgent);
                await queryExecutorAgent.DeleteAsync();
                await resourceTaggerAgent.DeleteAsync();
            }
        }
    }
}