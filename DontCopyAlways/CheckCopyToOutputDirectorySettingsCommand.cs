// <copyright file="CheckCopyToOutputDirectorySettingsCommand.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.ComponentModel.Design;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace DontCopyAlways
{
	internal sealed class CheckCopyToOutputDirectorySettingsCommand
	{
		public const int CommandId = 0x0101;

		private readonly AsyncPackage package;

		private CheckCopyToOutputDirectorySettingsCommand(AsyncPackage package, OleMenuCommandService commandService)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(DontCopyAlwaysCommand.CommandSet, CommandId);
			var menuItem = new MenuCommand(this.Execute, menuCommandID);
			commandService.AddCommand(menuItem);
		}

		public static CheckCopyToOutputDirectorySettingsCommand Instance { get; private set; }

		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Switch to the main thread - the call to AddCommand in DontCopyAlwaysCommand's constructor requires
			// the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

			Instance = new CheckCopyToOutputDirectorySettingsCommand(package, commandService);
		}

#pragma warning disable VSTHRD100 // Avoid async void methods
		private async void Execute(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			try
			{
				if (!(await this.package.GetServiceAsync(typeof(DTE)) is DTE2 dte))
				{
					return;
				}

				await OutputPane.Instance.WriteAsync(string.Empty);
				await OutputPane.Instance.WriteAsync(string.Empty);
				await OutputPane.Instance.WriteAsync($"{DateTime.Now:T} rechecking settings");
				await OutputPane.Instance.WriteAsync(string.Empty);

				var files = (Array)dte.ToolWindows.SolutionExplorer.SelectedItems;

				foreach (UIHierarchyItem selItem in files)
				{
					if (selItem.Object is Project proj && !string.IsNullOrEmpty(proj.FileName))
					{
						await DontCopyAlwaysPackage.ReportOnProjectsAsync(new[] { (proj.Name, proj.FileName) });
					}
					else
					{
						if (selItem.Object is Solution sol && !string.IsNullOrEmpty(sol.FileName))
						{
							var projs = await SolutionProjects.GetProjectsAsync(sol.Projects);
							await DontCopyAlwaysPackage.ReportOnProjectsAsync(projs);
						}
					}
				}

				await OutputPane.Instance.WriteAsync($"{DateTime.Now:T} rechecking settings complete.");
				await OutputPane.Instance.WriteAsync(string.Empty);
			}
			catch (Exception exc)
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
				await OutputPane.Instance.WriteAsync(string.Empty);
				await OutputPane.Instance.WriteAsync($"Unexpected error in {nameof(CheckCopyToOutputDirectorySettingsCommand)}:");
				await OutputPane.Instance.WriteAsync(exc.GetType().ToString());
				await OutputPane.Instance.WriteAsync(exc.Message);
				await OutputPane.Instance.WriteAsync(exc.StackTrace);
			}
		}
	}
}
