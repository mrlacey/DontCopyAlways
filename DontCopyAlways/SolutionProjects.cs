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
    // Originally from https://wwwlicious.com/envdte-getting-all-projects-html/
    public static class SolutionProjects
    {
        public static DTE2 GetActiveIDE()
        {
            return Package.GetGlobalService(typeof(DTE)) as DTE2;
        }

        public static async Task<IList<(string name, string filePath)>> GetProjectsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            Projects projects = GetActiveIDE().Solution.Projects;
            var list = new List<(string, string)>();
            var item = projects.GetEnumerator();

            while (item.MoveNext())
            {
                if (!(item.Current is Project project))
                {
                    continue;
                }

                if (project.Kind == ProjectKinds.vsProjectKindSolutionFolder)
                {
                    list.AddRange(GetSolutionFolderProjects(project));
                }
                else
                {
                    list.Add((project.Name, project.FileName));
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
                else
                {
                    list.Add((subProject.Name, subProject.FileName));
                }
            }

            return list;
        }
    }
}
