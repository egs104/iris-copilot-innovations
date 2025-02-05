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
        Output: ['3696573']

        2. Need: list of project ids of last user ask
        Input: Give me a 20 word description of project 1234567 and 1234253
        Output: ['1234567', '1234253']

        3. Need: list of project ids of last user ask
        Input: User: Give me info on project
        Output: Missing project id, please provide the project id

        4. Need: list of project ids of last user ask
        Input: ........
               AI: {'projectId': '12345', 'name': 'test project'}
               User: Give me info on project 12345
        Output: ['12345']";

        /// <summary>
        /// Gets project details using external APIs/SDKs. This is a Native function
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns></returns>
        [KernelFunction("get_project_details")]
        [Description("Get details of the projectId")]
        public async Task<string> GetProjectDetails(FunctionResult chatVariables)
        {
            this.semanticKernel.Plugins.TryGetFunction("SemanticPlugins", "EntityExtractorFunction", out KernelFunction entityExtractor);

            KernelArguments eeContext = new KernelArguments();
            eeContext["Example"] = EntityExtractorProjectExamples;
            eeContext["Need"] = "list of project ids of last user ask";
            eeContext["Input"] = chatVariables.Metadata["ChatTranscript"];

            var extractorResult = await this.semanticKernel.InvokeAsync(entityExtractor, eeContext);
            var projectIdsList = JsonSerializer.Deserialize<List<string>>(extractorResult.GetValue<string>());

            var pluginResponse = new StringBuilder();
            foreach(var projectId in projectIdsList)
            {
                var projectDetails = await this.projectService.GetProjectDetails(projectId);
                pluginResponse.AppendLine(projectDetails);
            }
            
            return pluginResponse.ToString();
        }
    }
}