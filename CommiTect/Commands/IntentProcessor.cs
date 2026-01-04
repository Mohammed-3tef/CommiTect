using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Imaging;

namespace CommiTect
{
    /// <summary>
    /// Intent processing and notification utilities
    /// </summary>
    internal class IntentProcessor
    {
        public async Task ProcessAndDisplayAsync(string intent, CommiTectPackage package)
        {
            System.Diagnostics.Debug.WriteLine($"[CommiTect] ProcessAndDisplayAsync called with intent: {intent}");

            var (type, message) = ParseIntent(intent);
            System.Diagnostics.Debug.WriteLine($"[CommiTect] Parsed - Type: {type}, Message: {message}");

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            System.Diagnostics.Debug.WriteLine("[CommiTect] Switched to UI thread");

            var displayMessage = !string.IsNullOrEmpty(message)
                ? $"{type}: {message}"
                : $"Intent: {intent}";

            System.Diagnostics.Debug.WriteLine($"[CommiTect] Display message: {displayMessage}");

            try
            {
                // Get the info bar host (main window)
                var shell = await package.GetServiceAsync(typeof(SVsShell)) as IVsShell;
                if (shell != null)
                {
                    object obj;
                    shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out obj);
                    var infoBarHost = obj as IVsInfoBarHost;

                    if (infoBarHost != null)
                    {
                        System.Diagnostics.Debug.WriteLine("[CommiTect] Creating info bar...");

                        // Create info bar model
                        var infoBarModel = new InfoBarModel(
                            new[]
                            {
                                new InfoBarTextSpan(displayMessage)
                            },
                            new[]
                            {
                                new InfoBarHyperlink("Copy to Clipboard")
                            },
                            KnownMonikers.StatusInformation,
                            isCloseButtonVisible: true);

                        // Create info bar UI element
                        var factory = await package.GetServiceAsync(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
                        var infoBarUI = factory.CreateInfoBar(infoBarModel);

                        // Handle button click
                        uint cookie = 0;
                        infoBarUI.Advise(new InfoBarEvents(displayMessage), out cookie);

                        // Add to host
                        infoBarHost.AddInfoBar(infoBarUI);

                        System.Diagnostics.Debug.WriteLine("[CommiTect] Info bar displayed successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CommiTect] Could not get info bar host, falling back to status bar");
                        // Fallback: just show in status bar
                        var statusBar = await package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
                        statusBar?.SetText($"✓ Commit Intent: {displayMessage}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommiTect] Error showing notification: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CommiTect] Stack trace: {ex.StackTrace}");

                // Fallback to status bar
                try
                {
                    var statusBar = await package.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
                    statusBar?.SetText($"✓ {displayMessage}");
                }
                catch { }
            }
        }

        private (string Type, string Message) ParseIntent(string intent)
        {
            if (string.IsNullOrWhiteSpace(intent))
            {
                return ("Unknown", string.Empty);
            }

            var lines = intent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            string intentType = string.Empty;
            string intentMessage = string.Empty;

            foreach (var line in lines)
            {
                if (line.StartsWith("Intent:", StringComparison.OrdinalIgnoreCase))
                {
                    intentType = line.Substring("Intent:".Length).Trim();
                }
                else if (line.StartsWith("Message:", StringComparison.OrdinalIgnoreCase))
                {
                    intentMessage = line.Substring("Message:".Length).Trim();
                }
            }

            // Fallback if parsing fails
            if (string.IsNullOrEmpty(intentType))
            {
                intentType = "Intent";
                intentMessage = intent;
            }

            return (intentType, intentMessage);
        }

        // Helper class to handle info bar events
        private class InfoBarEvents : IVsInfoBarUIEvents
        {
            private readonly string _message;

            public InfoBarEvents(string message)
            {
                _message = message;
            }

            public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                if (actionItem.Text == "Copy to Clipboard")
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[CommiTect] Copying to clipboard...");
                        Clipboard.SetText(_message);

                        // Close the info bar after copying
                        infoBarUIElement.Close();

                        System.Diagnostics.Debug.WriteLine("[CommiTect] Copied successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CommiTect] Failed to copy: {ex.Message}");
                    }
                }
            }

            public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
            {
                // Info bar was closed
                System.Diagnostics.Debug.WriteLine("[CommiTect] Info bar closed");
            }
        }
    }
}