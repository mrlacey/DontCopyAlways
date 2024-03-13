// <copyright file="OutputPane.cs" company="Matt Lacey">
// Copyright (c) Matt Lacey. All rights reserved.
// </copyright>

using System;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace DontCopyAlways
{
    public class OutputPane
    {
        private static Guid dcaPaneGuid = new Guid("B7BC9265-F7C2-4387-A416-8FA3B78CF8CB");

        private static OutputPane instance;

        private readonly IVsOutputWindowPane pane;

        private OutputPane()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ServiceProvider.GlobalProvider.GetService(typeof(SVsOutputWindow)) is IVsOutputWindow outWindow
                && (ErrorHandler.Failed(outWindow.GetPane(ref dcaPaneGuid, out this.pane)) || this.pane == null))
            {
                if (ErrorHandler.Failed(outWindow.CreatePane(ref dcaPaneGuid, "Don't Copy Always", 1, 0)))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to create output pane.");
                    return;
                }

                if (ErrorHandler.Failed(outWindow.GetPane(ref dcaPaneGuid, out this.pane)) || (this.pane == null))
                {
                    System.Diagnostics.Debug.WriteLine("Failed to get output pane.");
                }
            }
        }

        public static OutputPane Instance => instance ?? (instance = new OutputPane());

        public async Task ActivateAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            this.pane?.Activate();
        }

        public async Task WriteAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);

            this.pane?.OutputStringThreadSafe($"{message}{Environment.NewLine}");
        }
    }
}
