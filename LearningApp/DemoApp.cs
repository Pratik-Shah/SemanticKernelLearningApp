using LearningApp;

public class DemoApp
{

    // Main method
    public static async Task Main(string[] args)
    {
        Init();
        // Display the menu
        DisplayMenu();

        // Resolve and use the menuExecutor instance
        var menuExecutor = new MenuExecutor();
        var agentsGroupChatExecutor = new AgentsGroupChatExecutor();
        // Get the user input
        var option = Console.ReadLine();

        // Declare a variable to store the user input
        string? userInput;

        // Loop through the menu options
        while (true)
        {
            
            // Execute the selected option            
            switch (option)
            {                
                case "1":
                    Console.WriteLine("Enter your question here ...");
                    userInput = Console.ReadLine();
                    await menuExecutor.KernelInvocation(userInput!);
                    break;
                case "2":
                    menuExecutor.DisplayKernelPlugins();
                    break;
                case "3":
                    userInput = AskUserInput();
                    if (userInput != null)
                    {
                        await menuExecutor.ExecuteSemanticKernelWithAzureOpenAI(userInput!);
                    }
                    break;
                case "4":
                    userInput = AskUserInput();
                    if (userInput != null)
                    {
                        await menuExecutor.ExecuteSemanticKernelWithStepWisePlannerAzureOpenAI(userInput!);
                    }
                    break;
                case "5":
                    userInput = AskUserInput();
                    if (userInput != null)
                    {
                        await menuExecutor.ExecuteSemanticKernelWithHandlebarsPlannerAzureOpenAI(userInput!);
                    }
                    break;
                case "6":
                    userInput = AskUserInput();
                    if (userInput != null)
                    {
                        await menuExecutor.SingleAgentExecution(userInput!);
                    }
                    break;                    
                case "7":                                    
                        await menuExecutor.AgentDelegationExecution();                    
                    break;                    
                case "8":
                        await agentsGroupChatExecutor.MultiChatAgentInAgentGroupChat();
                    break;
                case "9":                                    
                    Console.WriteLine("Quitting the app");
                    return;
                default:
                    Console.WriteLine(AppConstants.InvalidSelectionResponse);                   
                    break;
            }

            Console.WriteLine(AppConstants.Menu);
            option = Console.ReadLine();
        }
    }

    private static string? AskUserInput()
    {
        string? userInput;
        Console.WriteLine("Enter your question here ...");
        userInput = Console.ReadLine();
        return userInput;
    }

    private static void Init()
    {
        // Load the .env file     
        DotNetEnv.Env.Load(AppConstants.EnvPath);        
    }

    private static void DisplayMenu()
    {
        Console.WriteLine($@"This is a sample App to demonstrate Plugins,Planners and Agents in Semantic Kernel.
        {AppConstants.Menu}");
    }
}
