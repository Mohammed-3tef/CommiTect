using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CommitIntentDetector
{
    /// <summary>
    /// File filtering utilities
    /// </summary>
    internal static class FileFilter
    {
        private static readonly HashSet<string> BinaryFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
            ".pdf", ".zip", ".tar", ".gz", ".7z", ".rar",
            ".exe", ".dll", ".so", ".dylib",
            ".mp3", ".mp4", ".avi", ".mov", ".wmv",
            ".woff", ".woff2", ".ttf", ".eot",
            ".bin", ".dat", ".db", ".sqlite"
        };

        private static readonly string[] IgnorePatterns = new[]
        {
            "\\node_modules\\",
            "\\.git\\",
            "\\.vs\\",
            "\\dist\\",
            "\\build\\",
            "\\.next\\",
            "\\coverage\\",
            "\\.nyc_output\\",
            "\\bin\\",
            "\\obj\\",
            "\\packages\\"
        };

        public static bool ShouldProcessFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            // Check file extension
            var extension = Path.GetExtension(filePath);
            if (BinaryFileExtensions.Contains(extension))
            {
                return false;
            }

            // Check ignored directories
            var normalizedPath = filePath.Replace('/', '\\');
            if (IgnorePatterns.Any(pattern =>
                normalizedPath.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return false;
            }

            return true;
        }
    }
}