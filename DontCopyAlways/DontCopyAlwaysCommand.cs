// <copyright file="DontCopyAlwaysCommand.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.ComponentModel.Design;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace DontCopyAlways
{
	internal sealed class DontCopyAlwaysCommand
	{
		public const int CommandId = 0x0100;

		public static readonly Guid CommandSet = new Guid("9df46588-dd0a-4303-97c5-057ba772f356");

		private readonly AsyncPackage package;

		private DontCopyAlwaysCommand(AsyncPackage package, OleMenuCommandService commandService)
		{
			this.package = package ?? throw new ArgumentNullException(nameof(package));
			commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

			var menuCommandID = new CommandID(CommandSet, CommandId);
			var menuItem = new MenuCommand(this.Execute, menuCommandID);
			commandService.AddCommand(menuItem);
		}

		public static DontCopyAlwaysCommand Instance { get; private set; }

		private IAsyncServiceProvider ServiceProvider => this.package;

		public static async Task InitializeAsync(AsyncPackage package)
		{
			// Switch to the main thread - the call to AddCommand in DontCopyAlwaysCommand's constructor requires
			// the UI thread.
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

			var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;

			Instance = new DontCopyAlwaysCommand(package, commandService);
		}

		private const string AlwaysXml = "<CopyToOutputDirectory>Always</CopyToOutputDirectory>";
		private const string PreserveNewestXml = "<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>";

#pragma warning disable VSTHRD100 // Avoid async void methods
		private async void Execute(object sender, EventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
		{
			await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

			try
			{
				async Task UpdateProjectFileEntries(string projFilePath)
				{
					try
					{
						if (File.Exists(projFilePath) && projFilePath.EndsWith("proj"))
						{
							string fileContents = null;
							System.Text.Encoding encoding;

							using (var reader = new StreamReader(projFilePath, detectEncodingFromByteOrderMarks: true))
							{
								fileContents = reader.ReadToEnd();
								encoding = reader.CurrentEncoding;
							}

							if (!string.IsNullOrWhiteSpace(fileContents))
							{
								await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

								if (fileContents.Contains(AlwaysXml))
								{
									var newContents = fileContents.Replace(AlwaysXml, PreserveNewestXml);
									File.WriteAllText(projFilePath, newContents, encoding);

									await OutputPane.Instance.WriteAsync($"Updated '{projFilePath}'.");
								}
								else
								{
									await OutputPane.Instance.WriteAsync($"No changes made to '{projFilePath}'.");
								}
							}

							await TaskScheduler.Default;
						}
					}
					catch (Exception exc)
					{
						await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
						await OutputPane.Instance.WriteAsync(string.Empty);
						await OutputPane.Instance.WriteAsync($"Unexpected error in {nameof(UpdateProjectFileEntries)}:");
						await OutputPane.Instance.WriteAsync(exc.GetType().ToString());
						await OutputPane.Instance.WriteAsync(exc.Message);
						await OutputPane.Instance.WriteAsync(exc.StackTrace);
					}
				}

				if (!(await this.ServiceProvider.GetServiceAsync(typeof(DTE)) is DTE2 dte))
				{
					return;
				}

				var files = (Array)dte.ToolWindows.SolutionExplorer.SelectedItems;

				foreach (UIHierarchyItem selItem in files)
				{
					if (selItem.Object is Project proj && !string.IsNullOrEmpty(proj.FileName))
					{
						await UpdateProjectFileEntries(proj.FileName);
					}
					else
					{
						if (selItem.Object is Solution sol && !string.IsNullOrEmpty(sol.FileName))
						{
							foreach (var (_, fileName) in await SolutionProjects.GetProjectsAsync(sol.Projects))
							{
								await UpdateProjectFileEntries(fileName);
							}
						}
					}
				}
			}
			catch (Exception exc)
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
				await OutputPane.Instance.WriteAsync(string.Empty);
				await OutputPane.Instance.WriteAsync($"Unexpected error in {nameof(DontCopyAlwaysCommand)}:");
				await OutputPane.Instance.WriteAsync(exc.GetType().ToString());
				await OutputPane.Instance.WriteAsync(exc.Message);
				await OutputPane.Instance.WriteAsync(exc.StackTrace);
			}
		}
	}
}
