namespace LearningApp;

public class AppConstants
{    
    public static string BasePath { get; } = Directory.GetCurrentDirectory();
    public static string PluginPath { get; } =  $"{BasePath}\\Plugins\\SemanticPlugin";
    public static string EnvPath = $"{BasePath}\\.env";
    public const string InvalidSelectionResponse = "Invalid option, please select a valid option from the menu..";
    public static string appSettingsPath = $"{BasePath}\\appsettings.json";

    public const string Menu = @"
    Please select the option to execute:    
    1. Perform Kernel invocation 
    2. Display Kernel Plugins
    3. Execute ChatCompletion with Plugin
    4. Execute StepWise Planner
    5. Execute Handlebars Planner
    6. AI Agent Execution
    7. Agent Delegation Execution
    8. AgentGroupChat Execution
    9. Exit
    ";
    

}