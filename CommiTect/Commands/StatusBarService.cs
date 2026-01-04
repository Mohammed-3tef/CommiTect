using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace CommiTect
{
    /// <summary>
    /// Status bar management service
    /// </summary>
    internal class StatusBarService
    {
        private readonly CommiTectPackage _package;
        private IVsStatusbar _statusBar;

        public StatusBarService(CommiTectPackage package)
        {
            _package = package;
        }

        private async Task<IVsStatusbar> GetStatusBarAsync()
        {
            if (_statusBar == null)
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _statusBar = await _package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
            }
            return _statusBar;
        }

        public async Task UpdateAsync(string text)
        {
            var options = _package.GetOptions();
            if (!options.ShowStatusBar)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var statusBar = await GetStatusBarAsync();
            if (statusBar != null)
            {
                statusBar.SetText(text);

                // Show animation if analyzing
                if (text.Contains("Analyzing"))
                {
                    object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_General;
                    statusBar.Animation(1, ref icon);
                }
                else
                {
                    object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_General;
                    statusBar.Animation(0, ref icon);
                }
            }
        }

        public async Task HideAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var statusBar = await GetStatusBarAsync();
            if (statusBar != null)
            {
                statusBar.Clear();
                object icon = (short)Microsoft.VisualStudio.Shell.Interop.Constants.SBAI_General;
                statusBar.Animation(0, ref icon);
            }
        }
    }
}