// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Program.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace PJMApp
{
    using System.Text;
    using System.Text.Json;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.Memory;
    using ProjectManagementPlugin;
    using ProjectManagementPlugin.DataAccess;
    using ProjectManagementPlugin.Domain;

    /// <summary>
    /// Program class
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args">Commandline arguments</param>
        public static async Task Main(string[] args)
        {
            // Initialize SK and Import skills - Done by Iris Copilot Platform's Zero-Dev
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddAzureOpenAIChatCompletion("iris-selfhelp", "https://iris-openai-dev.openai.azure.com/", "INPUT_KEY_HERE");
#pragma warning disable SKEXP0010
            kernelBuilder.AddAzureOpenAITextEmbeddingGeneration("text-embedding-ada-002", "https://iris-openai-dev.openai.azure.com/", "INPUT_KEY_HERE");
#pragma warning restore SKEXP0010
            //kernelBuilder.WithMemoryStorage(new VolatileMemoryStore());
            var kernel = kernelBuilder.Build();

            kernel.ImportPluginFromObject(new ProjectNativePlugin(kernel, new ProjectService(new ProjectRepository())), "ProjectPlugin");
            kernel.ImportPluginFromPromptDirectory("D:\\repos\\iris-copilot-innovations\\ProjectManagementPlugin\\SemanticPlugins");

            // Take out the function and invoke - Done by Iris Copilot Platform's Orchestrator
            kernel.Plugins.TryGetFunction("_GLOBAL_FUNCTIONS_", "GetProjectDetails", out KernelFunction getProjectDetails);

            // Passing chat transcript - Done by Iris Copilot Platform's Orchestrator
            var chatTranscript = new StringBuilder("Hello, How can I help you?");
            Console.WriteLine($"AI: {chatTranscript}\r\nUser:");
            var userInput = Console.ReadLine();
            do
            {
                KernelArguments variables = new KernelArguments();
                chatTranscript.AppendLine($"User: {userInput}");
                variables["ChatTranscript"] = chatTranscript.ToString();

                var pluginResponse = await kernel.InvokeAsync(getProjectDetails, variables);
                chatTranscript.AppendLine($"AI: {pluginResponse.GetValue<string>()}");
                Console.WriteLine($"AI:{JsonSerializer.Serialize(pluginResponse)}\r\nUser:");
                
                userInput = Console.ReadLine();
            } 
            while (!string.Equals(userInput, "exit", StringComparison.InvariantCultureIgnoreCase));
        }
    }
}