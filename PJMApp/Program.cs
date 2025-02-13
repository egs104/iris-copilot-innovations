// Import packages
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PJMApp;
using ProjectManagementPlugin.DataAccess;
using ProjectManagementPlugin.Domain;
using ProjectManagementPlugin;

// Populate values from your OpenAI deployment
var modelId = "gpt-35-turbo-16k";
var endpoint = "";
var apiKey = "";

// Create a kernel with Azure OpenAI chat completion
var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

// Add enterprise components
builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

// Add a plugin (the LightsPlugin class is defined below)
kernel.Plugins.AddFromType<LightsPlugin>("Lights");

kernel.ImportPluginFromObject(new ProjectNativePlugin(kernel, new ProjectService(new ProjectRepository())), "ProjectPlugin");
kernel.ImportPluginFromPromptDirectory("D:\\repos\\iris-copilot-innovations\\ProjectManagementPlugin\\SemanticPlugins");

// Take out the function and invoke - Done by Iris Copilot Platform's Orchestrator
// kernel.Plugins.TryGetFunction("_GLOBAL_FUNCTIONS_","GetProjectDetails", out KernelFunction getProjectDetails);

// Enable planning
OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
{
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Create a history store the conversation
var history = new ChatHistory();

// Set the ChatHistory in the Data property
kernel.Data["ChatHistory"] = history;

// Initiate a back-and-forth chat
string? userInput;
do
{
    // Collect user input
    Console.Write("User > ");
    userInput = Console.ReadLine();

    // Add user input
    history.AddUserMessage(userInput);

    // Get the response from the AI
    var result = await chatCompletionService.GetChatMessageContentAsync(
        history,
        executionSettings: openAIPromptExecutionSettings,
        kernel: kernel);

    // Print the results
    Console.WriteLine("Assistant > " + result);

    // Add the message from the agent to the chat history
    history.AddMessage(result.Role, result.Content ?? string.Empty);
} while (userInput is not null);