using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace CommitIntentDetector
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideOptionPage(typeof(OptionPageGrid), "Commit Intent Detector", "General", 0, 0, true)]
    public sealed class CommitIntentDetectorPackage : AsyncPackage
    {
        public const string PackageGuidString = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        private DocumentSaveListener _documentSaveListener;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Initialize the file save listener and keep it alive
            _documentSaveListener = new DocumentSaveListener(this);
            await _documentSaveListener.InitializeAsync();

            // Show activation message
            VsShellUtilities.ShowMessageBox(
                this,
                "Commit Intent Detector is now active!",
                "Commit Intent Detector",
                Microsoft.VisualStudio.Shell.Interop.OLEMSGICON.OLEMSGICON_INFO,
                Microsoft.VisualStudio.Shell.Interop.OLEMSGBUTTON.OLEMSGBUTTON_OK,
                Microsoft.VisualStudio.Shell.Interop.OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _documentSaveListener?.Dispose();
            }
            base.Dispose(disposing);
        }

        public OptionPageGrid GetOptions()
        {
            return (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
        }
    }
}