using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;
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
            // Switch to UI thread to work with VS services
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Initialize the file save listener and keep it alive
            _documentSaveListener = new DocumentSaveListener(this);
            await _documentSaveListener.InitializeAsync();

            // Show "Commit Intent Detector is now active!" as InfoBar
            try
            {
                var shell = await GetServiceAsync(typeof(SVsShell)) as IVsShell;
                if (shell != null)
                {
                    object obj;
                    shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out obj);
                    var infoBarHost = obj as IVsInfoBarHost;

                    if (infoBarHost != null)
                    {
                        // Create InfoBar model
                        var infoBarModel = new InfoBarModel(
                            new[] { new InfoBarTextSpan("Commit Intent Detector is now active!") },
                            Array.Empty<InfoBarActionItem>(),
                            KnownMonikers.StatusInformation,
                            isCloseButtonVisible: true
                        );

                        // Create InfoBar UI
                        var factory = await GetServiceAsync(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
                        var infoBarUI = factory.CreateInfoBar(infoBarModel);

                        // Add to host
                        infoBarHost.AddInfoBar(infoBarUI);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Failed to show InfoBar: {ex.Message}");
            }
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