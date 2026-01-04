using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Imaging;

namespace CommitIntentDetector
{
    /// <summary>
    /// Listens for document save events and triggers intent detection
    /// </summary>
    internal class DocumentSaveListener : IVsRunningDocTableEvents3, IDisposable
    {
        private readonly CommitIntentDetectorPackage _package;
        private RunningDocumentTable _runningDocumentTable;
        private Timer _debounceTimer;
        private string _pendingFilePath;
        private readonly GitService _gitService;
        private readonly ApiClient _apiClient;
        private readonly IntentProcessor _intentProcessor;
        private readonly StatusBarService _statusBarService;
        private uint _cookie;

        public DocumentSaveListener(CommitIntentDetectorPackage package)
        {
            _package = package;
            _gitService = new GitService();
            _apiClient = new ApiClient();
            _intentProcessor = new IntentProcessor();
            _statusBarService = new StatusBarService(package);
            System.Diagnostics.Debug.WriteLine("[CommitIntent] DocumentSaveListener created");
        }

        public async Task InitializeAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            _runningDocumentTable = new RunningDocumentTable(_package);
            _cookie = _runningDocumentTable.Advise(this);
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] DocumentSaveListener initialized with cookie: {_cookie}");
        }

        public int OnAfterSave(uint docCookie)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] OnAfterSave called for docCookie: {docCookie}");

            var options = _package.GetOptions();
            if (!options.Enabled)
            {
                System.Diagnostics.Debug.WriteLine("[CommitIntent] Extension is disabled");
                return VSConstants.S_OK;
            }

            var documentInfo = _runningDocumentTable.GetDocumentInfo(docCookie);
            var filePath = documentInfo.Moniker;
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] File path: {filePath}");

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine("[CommitIntent] File path is empty or doesn't exist");
                return VSConstants.S_OK;
            }

            if (!FileFilter.ShouldProcessFile(filePath))
            {
                System.Diagnostics.Debug.WriteLine("[CommitIntent] File filtered out");
                return VSConstants.S_OK;
            }

            _pendingFilePath = filePath;
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(ProcessFileCallback, null, options.DebounceDelay, Timeout.Infinite);
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] Timer scheduled with {options.DebounceDelay}ms delay");

            return VSConstants.S_OK;
        }

        private void ProcessFileCallback(object state)
        {
            System.Diagnostics.Debug.WriteLine("[CommitIntent] Timer fired, starting ProcessFileAsync");
            _ = ProcessFileAsync(); // Fire and forget
        }

        private async Task ProcessFileAsync()
        {
            var filePath = _pendingFilePath;
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] ProcessFileAsync started for: {filePath}");

            if (string.IsNullOrEmpty(filePath)) return;

            var options = _package.GetOptions();

            try
            {
                System.Diagnostics.Debug.WriteLine("[CommitIntent] Updating status bar...");
                await _statusBarService.UpdateAsync("Analyzing commit intent...");
                System.Diagnostics.Debug.WriteLine("[CommitIntent] Status bar updated");

                if (!await _gitService.IsGitRepositoryAsync(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("[CommitIntent] Not a git repository");
                    await _statusBarService.HideAsync();
                    return;
                }

                var diff = await _gitService.GetGitDiffAsync(filePath);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Diff length: {diff?.Length ?? 0}");

                if (string.IsNullOrWhiteSpace(diff))
                {
                    System.Diagnostics.Debug.WriteLine("[CommitIntent] Diff is empty");
                    await _statusBarService.HideAsync();
                    return;
                }

                var preview = diff.Length > 200 ? diff.Substring(0, 200) : diff;
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Diff preview: {preview}");

                var intent = await _apiClient.AnalyzeCommitIntentAsync(diff, options);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] API returned intent: {intent}");

                if (string.IsNullOrWhiteSpace(intent))
                {
                    System.Diagnostics.Debug.WriteLine("[CommitIntent] Intent is empty!");
                    throw new Exception("API returned empty intent");
                }

                await _intentProcessor.ProcessAndDisplayAsync(intent, _package);
                System.Diagnostics.Debug.WriteLine("[CommitIntent] ProcessAndDisplayAsync completed");

                await _statusBarService.UpdateAsync($"Intent detected!");
                await Task.Delay(3000);
                await _statusBarService.HideAsync();
                System.Diagnostics.Debug.WriteLine("[CommitIntent] Process completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Stack trace: {ex.StackTrace}");

                await _statusBarService.HideAsync();
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    var shell = await _package.GetServiceAsync(typeof(SVsShell)) as IVsShell;
                    if (shell != null)
                    {
                        object obj;
                        shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out obj);
                        var infoBarHost = obj as IVsInfoBarHost;

                        if (infoBarHost != null)
                        {
                            var infoBarModel = new InfoBarModel(
                                new[] { new InfoBarTextSpan($"Failed to detect commit intent: {ex.Message}") },
                                Array.Empty<InfoBarActionItem>(),
                                KnownMonikers.StatusInformation,
                                isCloseButtonVisible: true
                            );

                            var factory = await _package.GetServiceAsync(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
                            var infoBarUI = factory.CreateInfoBar(infoBarModel);
                            infoBarHost.AddInfoBar(infoBarUI);
                        }
                        else
                        {
                            var statusBar = await _package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
                            statusBar?.SetText($"Failed to detect commit intent: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Failed to show error InfoBar: {ex2.Message}");
                }
            }
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("[CommitIntent] Disposing DocumentSaveListener");
            _debounceTimer?.Dispose();
            if (_runningDocumentTable != null && _cookie != 0)
            {
                _runningDocumentTable.Unadvise(_cookie);
            }
        }

        // IVsRunningDocTableEvents3 required methods (no-op)
        public int OnBeforeSave(uint docCookie) => VSConstants.S_OK;
        public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
        public int OnBeforeLastDocumentUnlock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) => VSConstants.S_OK;
        public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) => VSConstants.S_OK;
        public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld,
                                            IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) => VSConstants.S_OK;
        public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) => VSConstants.S_OK;
        public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) => VSConstants.S_OK;
    }
}