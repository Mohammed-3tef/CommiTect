using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CommitIntentDetector
{
    /// <summary>
    /// Git repository utilities
    /// </summary>
    internal class GitService
    {
        private const int MaxDiffSize = 5 * 1024 * 1024; // 5MB
        private const int GitCommandTimeout = 10000; // 10 seconds

        public async Task<bool> IsGitRepositoryAsync(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Checking git in directory: {directory}");

                var result = await ExecuteGitCommandAsync("rev-parse --git-dir", directory);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Git rev-parse result: '{result}'");

                var isGit = !string.IsNullOrWhiteSpace(result);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Is git repository: {isGit}");

                return isGit;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] IsGitRepositoryAsync exception: {ex.Message}");
                return false;
            }
        }

        public async Task<string> GetGitDiffAsync(string filePath)
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Getting diff for: {filePath}");

                // Get repository root
                var repoRoot = await ExecuteGitCommandAsync("rev-parse --show-toplevel", directory);
                if (string.IsNullOrWhiteSpace(repoRoot))
                {
                    System.Diagnostics.Debug.WriteLine("[CommitIntent] Could not determine repo root");
                    return string.Empty;
                }

                repoRoot = repoRoot.Trim();
                // On Windows, git returns forward slashes, convert to backslashes
                repoRoot = repoRoot.Replace('/', Path.DirectorySeparatorChar);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Repo root: {repoRoot}");

                // Get relative path from repo root
                string relativePath;
                try
                {
                    // Make sure both paths are absolute and normalized
                    var absoluteFilePath = Path.GetFullPath(filePath);
                    var absoluteRepoRoot = Path.GetFullPath(repoRoot);

                    if (!absoluteRepoRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        absoluteRepoRoot += Path.DirectorySeparatorChar;

                    if (absoluteFilePath.StartsWith(absoluteRepoRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        relativePath = absoluteFilePath.Substring(absoluteRepoRoot.Length);
                    }
                    else
                    {
                        // Fallback to using Uri method
                        var baseUri = new Uri(absoluteRepoRoot);
                        var fullUri = new Uri(absoluteFilePath);
                        var relativeUri = baseUri.MakeRelativeUri(fullUri);
                        relativePath = Uri.UnescapeDataString(relativeUri.ToString());
                    }

                    // Git expects forward slashes
                    relativePath = relativePath.Replace('\\', '/');
                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Relative path: {relativePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Error calculating relative path: {ex.Message}");
                    return string.Empty;
                }

                // Check if file is tracked
                try
                {
                    await ExecuteGitCommandAsync($"ls-files --error-unmatch \"{relativePath}\"", repoRoot);
                    System.Diagnostics.Debug.WriteLine("[CommitIntent] File is tracked");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] File not tracked: {ex.Message}");
                    return string.Empty;
                }

                // Get diff
                var diff = await ExecuteGitCommandAsync($"diff HEAD -- \"{relativePath}\"", repoRoot);
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] Diff length: {diff.Length}");

                if (diff.Length > MaxDiffSize)
                {
                    System.Diagnostics.Debug.WriteLine("[CommitIntent] Diff too large");
                    return string.Empty;
                }

                return diff;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommitIntent] GetGitDiffAsync error: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<string> ExecuteGitCommandAsync(string arguments, string workingDirectory)
        {
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] Executing: git {arguments}");
            System.Diagnostics.Debug.WriteLine($"[CommitIntent] Working directory: {workingDirectory}");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                    }
                };

                try
                {
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    var timeoutTask = Task.Delay(GitCommandTimeout);
                    var processTask = Task.Run(() => process.WaitForExit());

                    var completedTask = await Task.WhenAny(processTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch { }
                        System.Diagnostics.Debug.WriteLine("[CommitIntent] Git command timed out");
                        throw new TimeoutException("Git command timed out");
                    }

                    var output = outputBuilder.ToString();
                    var error = errorBuilder.ToString();

                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Git exit code: {process.ExitCode}");
                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Git output: '{output.Trim()}'");

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        System.Diagnostics.Debug.WriteLine($"[CommitIntent] Git stderr: '{error.Trim()}'");
                    }

                    // Exit code 0 = success, 1 = no changes (for diff), 128+ = error
                    if (process.ExitCode == 0 || process.ExitCode == 1)
                    {
                        return output;
                    }

                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Git command failed with exit code {process.ExitCode}");
                    throw new Exception($"Git command failed with exit code {process.ExitCode}: {error}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CommitIntent] Git execution error: {ex.Message}");
                    throw;
                }
            }
        }
    }
}