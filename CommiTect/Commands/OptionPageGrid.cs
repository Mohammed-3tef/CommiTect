using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace CommiTect
{
    /// <summary>
    /// Options page for the extension
    /// </summary>
    [ComVisible(true)]
    public class OptionPageGrid : DialogPage
    {
        [Category("General")]
        [DisplayName("Enabled")]
        [Description("Enable or disable automatic commit intent detection")]
        public bool Enabled { get; set; } = true;

        [Category("API Configuration")]
        [DisplayName("API URL")]
        [Description("The URL of the backend API endpoint for commit analysis")]
        public string ApiUrl { get; set; } = "http://commitintentdetector.runasp.net/api/Commit/analyze";

        [Category("API Configuration")]
        [DisplayName("Timeout (ms)")]
        [Description("Timeout in milliseconds for API requests")]
        public int Timeout { get; set; } = 30000;

        [Category("API Configuration")]
        [DisplayName("Allow Insecure SSL")]
        [Description("Allow insecure SSL certificates (development only)")]
        public bool AllowInsecureSSL { get; set; } = false;

        [Category("Behavior")]
        [DisplayName("Debounce Delay (ms)")]
        [Description("Delay in milliseconds before processing file saves")]
        public int DebounceDelay { get; set; } = 1000;

        [Category("UI")]
        [DisplayName("Show Status Bar")]
        [Description("Show status bar indicator during commit analysis")]
        public bool ShowStatusBar { get; set; } = true;
    }
}