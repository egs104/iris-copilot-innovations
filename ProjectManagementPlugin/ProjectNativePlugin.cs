// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProjectNativePlugin.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace ProjectManagementPlugin
{
    using System.ComponentModel;
    using System.Text;
    using System.Text.Json;
    using Microsoft.SemanticKernel;
    using Microsoft.SemanticKernel.ChatCompletion;
    using ProjectManagementPlugin.Domain;

    public class ProjectNativePlugin
    {
        private readonly Kernel semanticKernel;
        private readonly IProjectService projectService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectNativePlugin"/> class.
        /// </summary>
        /// <param name="semanticKernel">Semantic Kernel instance</param>
        /// <param name="projectService">Project service instance</param>
        public ProjectNativePlugin(Kernel semanticKernel, IProjectService projectService) 
        {
            this.semanticKernel = semanticKernel ?? throw new ArgumentNullException(nameof(semanticKernel));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        }

        // This example would be used as few-shot examples along-side RAG Pattern
        private const string EntityExtractorProjectExamples = @"1. Need: list of project ids of last user ask
        Input: Give me info on project id 3696573
        Output: [3696573]

        2. Need: list of project ids of last user ask
        Input: Give me a 20 word description of project 1234567 and 1234253
        Output: [1234567, 1234253]

        3. Need: list of project ids of last user ask
        Input: User: Give me info on project
        Output: Missing project id, please provide the project id

        4. Need: list of project ids of last user ask
        Input: ........
               AI: {'projectId': '12345', 'name': 'test project'}
               User: Give me info on project 12345
        Output: [12345]";

        /// <summary>
        /// Gets project details using external APIs/SDKs. This is a Native function
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [KernelFunction("get_project_details")]
        [Description("Get details of the projectId")]
        public async Task<string> GetProjectDetails()
        {
            this.semanticKernel.Plugins.TryGetFunction("SemanticPlugins", "EntityExtractorFunction", out KernelFunction entityExtractor);
            List<int>? projectIdsList = await GetProjectIdsFromUserPrompt(entityExtractor);

            var pluginResponse = new StringBuilder();
            foreach (var projectId in projectIdsList)
            {
                var projectDetails = await this.projectService.GetProjectDetails(projectId.ToString());
                pluginResponse.AppendLine(projectDetails);
            }

            return pluginResponse.ToString();
        }

        /// <summary>
        /// Generates risks and action items for a given project
        /// </summary>
        /// <param name="projectId">Project Id</param>
        /// <returns>Risks and action items</returns>
        [KernelFunction("generate_risks_and_action_items")]
        [Description("Generate risks and action items for the given projectId")]
        public async Task<string> GenerateRisksAndActionItems()
        {
            this.semanticKernel.Plugins.TryGetFunction("SemanticPlugins", "EntityExtractorFunction", out KernelFunction entityExtractor);
            List<int>? projectIdsList = await GetProjectIdsFromUserPrompt(entityExtractor);

            var pluginResponse = new StringBuilder();
            foreach (var projectId in projectIdsList)
            {
                var projectDetails = await this.projectService.GetProjectDetails(projectId.ToString());
                // Assuming GenerateRisksAndActionItemsFromDetails is a method that generates risks and action items from project details
                var risksAndActionItems = GenerateRisksAndActionItemsFromDetails(projectDetails);
                pluginResponse.AppendLine(risksAndActionItems);
            }

            return pluginResponse.ToString();
        }

        private async Task<List<int>?> GetProjectIdsFromUserPrompt(KernelFunction entityExtractor)
        {
            ChatHistory chat = (ChatHistory)this.semanticKernel.Data["ChatHistory"];
            var lastUserMessage = chat.LastOrDefault(message => message.Role.Label.Equals("user", StringComparison.CurrentCultureIgnoreCase))?.Content;

            KernelArguments eeContext = new KernelArguments();
            eeContext["Example"] = EntityExtractorProjectExamples;
            eeContext["Need"] = "list of project ids of last user ask";
            eeContext["Input"] = lastUserMessage;

            var extractorResult = await this.semanticKernel.InvokeAsync(entityExtractor, eeContext);
            var projectIds = extractorResult.GetValue<string>();
            var projectIdsList = JsonSerializer.Deserialize<List<int>>(projectIds);
            return projectIdsList;
        }

        private string GenerateRisksAndActionItemsFromDetails(string projectDetails)
        {
            // Placeholder for actual implementation
            // This method should analyze the project details and generate risks and action items
            return $"Risks and Action Items for project: {projectDetails}";
        }

    }
}