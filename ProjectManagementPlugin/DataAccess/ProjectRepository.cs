// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ProjectRepository.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Text.Json;

namespace ProjectManagementPlugin.DataAccess
{
    public class ProjectRepository : IProjectRepository
    {
        /// <summary>
        /// Gets project info
        /// </summary>
        /// <param name="projectId">Project Id</param>
        /// <returns>Project details</returns>
        public async Task<string> GetProjectDetails(string projectId)
        {
            var result = JsonSerializer.Serialize(new { projectId, name = "test project", description = "The team will develop a new feature for the company's flagship software product, enhancing its data analytics capabilities. This project involves integrating third-party APIs and ensuring compliance with data privacy regulations." });
            return await Task.FromResult(result);
        }
    }
}