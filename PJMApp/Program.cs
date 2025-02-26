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
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using System.Text.Json;

// Populate values from your OpenAI deployment
var modelId = "gpt-4o-mini";
var endpoint = "";
var apiKey = "";

const string ReviewerName = "Reviewer";
const string WriterName = "Writer";

// Create a kernel with Azure OpenAI chat completion
var builder = Kernel.CreateBuilder().AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);

// Add enterprise components
// builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));

// Build the kernel
Kernel kernel = builder.Build();

Kernel toolKernel = kernel.Clone();
toolKernel.Plugins.AddFromType<ClipboardAccess>();

// Add a plugin (the LightsPlugin class is defined below)
toolKernel.Plugins.AddFromType<LightsPlugin>("Lights");

toolKernel.ImportPluginFromObject(new ProjectNativePlugin(kernel, new ProjectService(new ProjectRepository())), "ProjectPlugin");
toolKernel.ImportPluginFromPromptDirectory("D:\\repos\\iris-copilot-innovations\\ProjectManagementPlugin\\SemanticPlugins");

ChatCompletionAgent agentReviewer =
    new()
    {
        Name = ReviewerName,
        Instructions =
            """
            Your responsiblity is to review and identify how to improve user provided content.
            If the user has providing input or direction for content already provided, specify how to address this input.
            Never directly perform the correction or provide example.
            Once the content has been updated in a subsequent response, you will review the content again until satisfactory.
            Always copy satisfactory content to the clipboard using available tools and inform user.

            RULES:
            - Only identify suggestions that are specific and actionable.
            - Verify previous suggestions have been addressed.
            - Never repeat previous suggestions.
            """,
        Kernel = toolKernel,
        Arguments =
            new KernelArguments(
                new AzureOpenAIPromptExecutionSettings() 
                { 
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() 
                })
    };

ChatCompletionAgent agentWriter =
    new()
    {
        Name = WriterName,
        Instructions =
            """
            Your sole responsiblity is to rewrite content according to review suggestions.

            - Always apply all review direction.
            - Always revise the content in its entirety without explanation.
            - Never address the user.
            """,
        Kernel = kernel,
    };

KernelFunction selectionFunction =
    AgentGroupChat.CreatePromptFunctionForStrategy(
        $$$"""
        Examine the provided RESPONSE and choose the next participant.
        State only the name of the chosen participant without explanation.
        Never choose the participant named in the RESPONSE.

        Choose only from these participants:
        - {{{ReviewerName}}}
        - {{{WriterName}}}

        Always follow these rules when choosing the next participant:
        - If RESPONSE is user input, it is {{{ReviewerName}}}'s turn.
        - If RESPONSE is by {{{ReviewerName}}}, it is {{{WriterName}}}'s turn.
        - If RESPONSE is by {{{WriterName}}}, it is {{{ReviewerName}}}'s turn.

        RESPONSE:
        {{$lastmessage}}
        """,
        safeParameterNames: "lastmessage");

const string TerminationToken = "yes";

KernelFunction terminationFunction =
    AgentGroupChat.CreatePromptFunctionForStrategy(
        $$$"""
        Examine the RESPONSE and determine whether the content has been deemed satisfactory.
        If content is satisfactory, respond with a single word without explanation: {{{TerminationToken}}}.
        If specific suggestions are being provided, it is not satisfactory.
        If no correction is suggested, it is satisfactory.

        RESPONSE:
        {{$lastmessage}}
        """,
        safeParameterNames: "lastmessage");

ChatHistoryTruncationReducer historyReducer = new(1);

AgentGroupChat chat =
    new(agentReviewer, agentWriter)
    {
        ExecutionSettings = new AgentGroupChatSettings
        {
            SelectionStrategy =
                new KernelFunctionSelectionStrategy(selectionFunction, kernel)
                {
                    // Always start with the editor agent.
                    InitialAgent = agentReviewer,
                    // Save tokens by only including the final response
                    HistoryReducer = historyReducer,
                    // The prompt variable name for the history argument.
                    HistoryVariableName = "lastmessage",
                    // Returns the entire result value as a string.
                    ResultParser = (result) => result.GetValue<string>() ?? agentReviewer.Name
                },
            TerminationStrategy =
                new KernelFunctionTerminationStrategy(terminationFunction, kernel)
                {
                    // Only evaluate for editor's response
                    Agents = [agentReviewer],
                    // Save tokens by only including the final response
                    HistoryReducer = historyReducer,
                    // The prompt variable name for the history argument.
                    HistoryVariableName = "lastmessage",
                    // Limit total number of turns
                    MaximumIterations = 10,
                    // Customer result parser to determine if the response is "yes"
                    ResultParser = (result) => result.GetValue<string>()?.Contains(TerminationToken, StringComparison.OrdinalIgnoreCase) ?? false
                }
        }
    };

Console.WriteLine("Ready!");

bool isComplete = false;
do
{
    Console.WriteLine();
    Console.Write("> ");
    string input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }
    input = input.Trim();
    if (input.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
    {
        isComplete = true;
        break;
    }

    if (input.Equals("RESET", StringComparison.OrdinalIgnoreCase))
    {
        await chat.ResetAsync();
        Console.WriteLine("[Converation has been reset]");
        continue;
    }

    if (input.StartsWith("@", StringComparison.Ordinal) && input.Length > 1)
    {
        string filePath = input.Substring(1);
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Unable to access file: {filePath}");
                continue;
            }
            input = File.ReadAllText(filePath);
        }
        catch (Exception)
        {
            Console.WriteLine($"Unable to access file: {filePath}");
            continue;
        }
    }

    chat.AddChatMessage(new ChatMessageContent(AuthorRole.User, input));
    chat.IsComplete = false;

    try
    {
        await foreach (ChatMessageContent response in chat.InvokeAsync())
        {
            Console.WriteLine();
            Console.WriteLine($"{response.AuthorName.ToUpperInvariant()}:{Environment.NewLine}{response.Content}");
        }
    }
    catch (HttpOperationException exception)
    {
        Console.WriteLine(exception.Message);
        if (exception.InnerException != null)
        {
            Console.WriteLine(exception.InnerException.Message);
            if (exception.InnerException.Data.Count > 0)
            {
                Console.WriteLine(JsonSerializer.Serialize(exception.InnerException.Data, new JsonSerializerOptions() { WriteIndented = true }));
            }
        }
    }
} while (!isComplete);