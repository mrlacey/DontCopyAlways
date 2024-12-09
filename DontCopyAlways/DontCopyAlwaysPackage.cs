// <copyright file="DontCopyAlwaysPackage.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Events;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace DontCopyAlways
{
	[ProvideAutoLoad(UIContextGuids.SolutionHasMultipleProjects, PackageAutoLoadFlags.BackgroundLoad)]
	[ProvideAutoLoad(UIContextGuids.SolutionHasSingleProject, PackageAutoLoadFlags.BackgroundLoad)]
	[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
	[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)] // Info on this package for Help/About
	[Guid(DontCopyAlwaysPackage.PackageGuidString)]
	[ProvideMenuResource("Menus.ctmenu", 1)]
	public sealed class DontCopyAlwaysPackage : AsyncPackage
	{
		public const string PackageGuidString = "452ef2b4-1788-49a6-8f34-b65812b45e8d";

		protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
		{
			// When initialized asynchronously, the current thread may be a background thread at this point.
			// Do any initialization that requires the UI thread after switching to the UI thread.
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

			SolutionEvents.OnAfterOpenSolution += this.SolutionEvents_OnAfterOpenSolution;
			SolutionEvents.OnAfterLoadProject += this.SolutionEvents_OnAfterLoadProject;

			await DontCopyAlwaysCommand.InitializeAsync(this);
			await CheckCopyToOutputDirectorySettingsCommand.InitializeAsync(this);

			await this.CheckAllProjectsInSolutionAsync();

			await SponsorRequestHelper.CheckIfNeedToShowAsync();

			await TrackBasicUsageAnalyticsAsync();
		}

		private static async Task TrackBasicUsageAnalyticsAsync()
		{
			try
			{
#if !DEBUG
			if (string.IsNullOrWhiteSpace(AnalyticsConfig.TelemetryConnectionString))
			{
				return;
			}

			var config = new TelemetryConfiguration
			{
				ConnectionString = AnalyticsConfig.TelemetryConnectionString,
			};

			var client = new TelemetryClient(config);

			var properties = new Dictionary<string, string>
				{
					{ "VsixVersion", Vsix.Version },
					{ "VsVersion", Microsoft.VisualStudio.Telemetry.TelemetryService.DefaultSession?.GetSharedProperty("VS.Core.ExeVersion") },
					{ "Architecture", RuntimeInformation.ProcessArchitecture.ToString() },
					{ "MsInternal", Microsoft.VisualStudio.Telemetry.TelemetryService.DefaultSession?.IsUserMicrosoftInternal.ToString() },
				};

			client.TrackEvent(Vsix.Name, properties);
#endif
			}
			catch (Exception exc)
			{
				System.Diagnostics.Debug.WriteLine(exc);
				await OutputPane.Instance.WriteAsync("Error tracking usage analytics: " + exc.Message);
			}
		}

		private void SolutionEvents_OnAfterLoadProject(object sender, LoadProjectEventArgs e)
		{
#pragma warning disable VSTHRD102 // Implement internal logic asynchronously
			ThreadHelper.JoinableTaskFactory.Run(async () =>
#pragma warning restore VSTHRD102 // Implement internal logic asynchronously
			{
				await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);

				try
				{
					e.RealHierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out object projectObj);

					if (projectObj is EnvDTE.Project project)
					{
						var projName = project.Name;
						var projFile = project.FileName;

						// Get off the UI thread so don't lock UI
						await TaskScheduler.Default;

						await ReportOnProjectsAsync(new List<(string, string)> { (projName, projFile) });
					}
				}
				catch (Exception exc)
				{
					await OutputPane.Instance.WriteAsync(string.Empty);
					await OutputPane.Instance.WriteAsync("Unexpected error when a project was loaded.");
					await OutputPane.Instance.WriteAsync("Please report these details at https://github.com/mrlacey/DontCopyAlways/issues/new");
					await OutputPane.Instance.WriteAsync(string.Empty);
					await OutputPane.Instance.WriteAsync(exc.GetType().ToString());
					await OutputPane.Instance.WriteAsync(exc.Message);
					await OutputPane.Instance.WriteAsync(exc.StackTrace);
					await OutputPane.Instance.WriteAsync(string.Empty);
					await OutputPane.Instance.ActivateAsync();
				}
			});
		}

#pragma warning disable VSTHRD100 // Avoid async void methods
		private async void SolutionEvents_OnAfterOpenSolution(object sender, OpenSolutionEventArgs e)
#pragma warning restore VSTHRD100 // Avoid async void methods
		{
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);

			try
			{
				await this.CheckAllProjectsInSolutionAsync();
			}
			catch (Exception exc)
			{
				await OutputPane.Instance.WriteAsync(string.Empty);
				await OutputPane.Instance.WriteAsync("Unexpected error when opening a solution.");
				await OutputPane.Instance.WriteAsync("Please report these details at https://github.com/mrlacey/DontCopyAlways/issues/new");
				await OutputPane.Instance.WriteAsync(string.Empty);
				await OutputPane.Instance.WriteAsync(exc.GetType().ToString());
				await OutputPane.Instance.WriteAsync(exc.Message);
				await OutputPane.Instance.WriteAsync(exc.StackTrace);
			}
		}

		private async Task CheckAllProjectsInSolutionAsync()
		{
			await this.JoinableTaskFactory.SwitchToMainThreadAsync(this.DisposalToken);

			var projs = await SolutionProjects.GetProjectsAsync();

			// Get off the UI thread so don't lock UI while solution loads
			await TaskScheduler.Default;

			await ReportOnProjectsAsync(projs);
		}

		internal static async Task ReportOnProjectsAsync(IList<(string, string)> projects)
		{
			var issuesFound = new List<(string project, List<string> files)>();

			foreach (var (name, filepath) in projects)
			{
				Debug.WriteLine(name);

				if (!Path.IsPathRooted(filepath))
				{
					// If don't have the full path to the file then can't load it.
					continue;
				}

				var attrs = File.GetAttributes(filepath);

				if (attrs.HasFlag(FileAttributes.Directory))
				{
					// If the project is based on a directory (like a website)
					// It won't have a project file we can load.
					continue;
				}

				var doc = new XmlDocument();
				doc.Load(filepath);

				var ctodElements = doc.GetElementsByTagName("CopyToOutputDirectory");

				var issuesInThisProject = new List<string>();

				for (int i = 0; i < ctodElements.Count; i++)
				{
					var element = ctodElements[i];

					if (element.InnerText.Equals("Always", StringComparison.InvariantCultureIgnoreCase))
					{
						var parent = element.ParentNode;
						var include = parent.Attributes.GetNamedItem("Include");
						if (include != null)
						{
							Debug.WriteLine($"-- {include.InnerText}");
							issuesInThisProject.Add(include.InnerText);
						}
					}
				}

				if (issuesInThisProject.Any())
				{
					issuesFound.Add((name, issuesInThisProject));
				}
			}

			if (issuesFound.Any())
			{
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
				await OutputPane.Instance.WriteAsync("Files found with the 'Copy to output directory set to 'Copy always'.");
				await OutputPane.Instance.WriteAsync("Learn more about why this matters at https://github.com/mrlacey/DontCopyAlways/blob/main/explanation.md");
				await OutputPane.Instance.WriteAsync(string.Empty);

				foreach (var (project, files) in issuesFound)
				{
					await OutputPane.Instance.WriteAsync(project);

					foreach (var file in files)
					{
						await OutputPane.Instance.WriteAsync($"- {file}");
					}

					await OutputPane.Instance.WriteAsync(string.Empty);
				}

				await OutputPane.Instance.WriteAsync("Changing this setting can save you time and money.");
				await OutputPane.Instance.WriteAsync("Show your appreciation by becoming a sponsor https://github.com/sponsors/mrlacey");
				await OutputPane.Instance.WriteAsync(string.Empty);

				await OutputPane.Instance.ActivateAsync();
			}
		}
	}
}
