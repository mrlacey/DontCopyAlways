// <copyright file="SolutionProjects.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace DontCopyAlways
{
	// Based on https://wwwlicious.com/envdte-getting-all-projects-html/
	public static class SolutionProjects
	{
		private const string WebSiteProjectKind = "{E24C65DC-7377-472b-9ABA-BC803B73C61A}";

		public static DTE2 GetActiveIDE()
		{
			return Package.GetGlobalService(typeof(DTE)) as DTE2;
		}

		public static async Task<IList<(string name, string filePath)>> GetProjectsAsync(Projects projects = null)
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			if (projects == null)
			{
				projects = GetActiveIDE().Solution.Projects;
			}

			var list = new List<(string, string)>();
			var item = projects.GetEnumerator();

			while (item.MoveNext())
			{
				// skip if Current item is not a project or if the project Kind is a Web Site project (as won't have a project file to load)
				if (!(item.Current is Project project) || project.Kind == WebSiteProjectKind)
				{
					continue;
				}

				if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
				{
					list.AddRange(GetSolutionFolderProjects(project));
				}
				else
				{
					// Projects that are not loaded with the solution won't provide access to their properties (including the FileName)
					if (project.Properties != null)
					{
						list.Add((project.Name, project.FileName));
					}
				}
			}

			return list;
		}

		private static IEnumerable<(string, string)> GetSolutionFolderProjects(Project solutionFolder)
		{
			ThreadHelper.ThrowIfNotOnUIThread();
			var list = new List<(string, string)>();

			for (var i = 1; i <= solutionFolder.ProjectItems.Count; i++)
			{
				var subProject = solutionFolder.ProjectItems.Item(i).SubProject;

				if (subProject == null)
				{
					continue;
				}

				// If this is another solution folder, do a recursive call, otherwise add
				if (subProject.Kind == ProjectKinds.vsProjectKindSolutionFolder)
				{
					list.AddRange(GetSolutionFolderProjects(subProject));
				}
				else if (subProject is Project proj
					&& proj.Kind != WebSiteProjectKind)
				{
					list.Add((subProject.Name, subProject.FileName));
				}
			}

			return list;
		}
	}
}
