using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CommiTect
{
    /// <summary>
    /// API client for commit intent detection
    /// </summary>
    internal class ApiClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public async Task<string> AnalyzeCommitIntentAsync(string diff, OptionPageGrid options)
        {
            System.Diagnostics.Debug.WriteLine("[CommiTect] AnalyzeCommitIntentAsync called");

            try
            {
                // لو SSL غير آمن مطلوب
                if (options.AllowInsecureSSL && options.ApiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine("[CommiTect] Using insecure SSL handler");
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                    };

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMilliseconds(options.Timeout);
                        return await SendRequestAsync(client, options.ApiUrl, diff);
                    }
                }
                else
                {
                    // حالة عادية: HttpClient جديد لكل request
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMilliseconds(options.Timeout);
                        return await SendRequestAsync(client, options.ApiUrl, diff);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[CommiTect] Request timed out");
                throw new Exception($"Request timeout: Cannot reach backend API at {options.ApiUrl} within {options.Timeout / 1000} seconds.");
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CommiTect] HTTP request failed: {ex.Message}");
                throw new Exception($"Cannot reach backend API at {options.ApiUrl}. Please check if the server is running and the URL is correct. Details: {ex.Message}");
            }
        }

        private async Task<string> SendRequestAsync(HttpClient client, string apiUrl, string diff)
        {
            var requestBody = new
            {
                diff = diff
            };

            var json = JsonSerializer.Serialize(requestBody);
            System.Diagnostics.Debug.WriteLine($"[CommiTect] Sending POST to: {apiUrl}");
            System.Diagnostics.Debug.WriteLine($"[CommiTect] Request body length: {json.Length} characters");

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(apiUrl, content);
            System.Diagnostics.Debug.WriteLine($"[CommiTect] Response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                var errorText = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[CommiTect] Error response: {errorText}");
                throw new Exception($"API returned {response.StatusCode}: {errorText}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[CommiTect] Response JSON: {responseJson}");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = JsonSerializer.Deserialize<ApiResponse>(responseJson, options);

            if (result == null || string.IsNullOrEmpty(result.intent))
            {
                System.Diagnostics.Debug.WriteLine("[CommiTect] Failed to parse response or intent is empty");
                throw new Exception("Invalid response format: expected { intent: string }");
            }

            System.Diagnostics.Debug.WriteLine($"[CommiTect] Successfully parsed intent: {result.intent}");
            return result.intent;
        }

        private class ApiResponse
        {
            public string intent { get; set; }
        }
    }
}