// <copyright file="AlternativeSearchResolver.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools.CaptchaResolver;

using System.Net;
using System.Net.Http;

/// <summary>
/// CAPTCHA resolver that bypasses CAPTCHA by using alternative search engines
/// that don't have CAPTCHA protection or are less aggressive.
/// </summary>
public class AlternativeSearchResolver : ICaptchaResolverStrategy
{
    private static readonly string[] AlternativeSearchEngines =
    [
        "https://lite.duckduckgo.com/lite/?q={0}",  // DDG lite - less aggressive
        "https://html.duckduckgo.com/html/?q={0}", // DDG HTML-only version
        "https://www.mojeek.com/search?q={0}",     // Independent search engine
        "https://search.brave.com/search?q={0}",
        "https://www.startpage.com/do/dsearch?query={0}",
        "https://www.ecosia.org/search?q={0}",
        "https://www.qwant.com/?q={0}",
        "https://searx.be/search?q={0}",  // Public SearX instance
    ];

    /// <inheritdoc/>
    public string Name => "AlternativeSearch";

    /// <inheritdoc/>
    public int Priority => 50; // Medium priority - tried after vision

    /// <inheritdoc/>
    public CaptchaDetectionResult DetectCaptcha(string content, string url)
    {
        // This resolver doesn't do detection - it's a fallback
        return new CaptchaDetectionResult(false, string.Empty);
    }

    /// <inheritdoc/>
    public async Task<CaptchaResolutionResult> ResolveAsync(
        string originalUrl,
        string captchaContent,
        CancellationToken ct = default)
    {
        // Extract the search query from the original URL
        var query = ExtractQueryFromUrl(originalUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return new CaptchaResolutionResult(false, ErrorMessage: "Could not extract search query from URL");
        }

        using var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };
        using var http = new HttpClient(handler);
        ConfigureHeaders(http);
        http.Timeout = TimeSpan.FromSeconds(20);

        var errors = new List<string>();

        foreach (var engineTemplate in AlternativeSearchEngines)
        {
            try
            {
                var searchUrl = string.Format(engineTemplate, Uri.EscapeDataString(query));
                var response = await http.GetAsync(searchUrl, ct);

                if (!response.IsSuccessStatusCode)
                {
                    errors.Add($"{GetEngineName(engineTemplate)}: HTTP {(int)response.StatusCode}");
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(ct);

                // Check if this engine also has CAPTCHA
                if (ContainsCaptchaIndicators(html))
                {
                    errors.Add($"{GetEngineName(engineTemplate)}: Also has CAPTCHA");
                    continue;
                }

                // Extract results
                var engineName = GetEngineName(engineTemplate);
                var results = ExtractSearchResults(html, engineName);
                if (results.Count > 0)
                {
                    return new CaptchaResolutionResult(
                        true,
                        ResolvedContent: $"Results from {engineName}:\n{string.Join("\n\n", results.Take(5))}");
                }

                errors.Add($"{engineName}: No results extracted");
            }
            catch (Exception ex)
            {
                errors.Add($"{GetEngineName(engineTemplate)}: {ex.Message}");
            }
        }

        return new CaptchaResolutionResult(
            false,
            ErrorMessage: $"All alternative engines failed: {string.Join("; ", errors)}");
    }

    private static string? ExtractQueryFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

            // Common query parameter names
            return queryParams["q"] ?? queryParams["query"] ?? queryParams["search"];
        }
        catch
        {
            return null;
        }
    }

    private static string GetEngineName(string urlTemplate)
    {
        if (urlTemplate.Contains("lite.duckduckgo")) return "DDG-Lite";
        if (urlTemplate.Contains("html.duckduckgo")) return "DDG-HTML";
        if (urlTemplate.Contains("mojeek")) return "Mojeek";
        if (urlTemplate.Contains("brave")) return "Brave";
        if (urlTemplate.Contains("startpage")) return "StartPage";
        if (urlTemplate.Contains("ecosia")) return "Ecosia";
        if (urlTemplate.Contains("qwant")) return "Qwant";
        if (urlTemplate.Contains("searx")) return "SearX";
        return "Unknown";
    }

    private static void ConfigureHeaders(HttpClient http)
    {
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        // Don't set Accept-Encoding - HttpClientHandler handles decompression automatically
        http.DefaultRequestHeaders.Add("DNT", "1");
        http.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        http.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        http.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        http.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        http.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    private static bool ContainsCaptchaIndicators(string html)
    {
        var lower = html.ToLowerInvariant();
        return lower.Contains("captcha") ||
               lower.Contains("challenge") && lower.Contains("human") ||
               lower.Contains("robot check") ||
               lower.Contains("verify you are");
    }

    private static List<string> ExtractSearchResults(string html, string engineName)
    {
        var results = new List<string>();

        // DDG Lite specific extraction
        if (engineName.StartsWith("DDG"))
        {
            // DDG Lite uses specific class patterns
            var ddgMatches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<a[^>]*class=""[^""]*result-link[^""]*""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in ddgMatches)
            {
                var url = match.Groups[1].Value;
                var title = System.Web.HttpUtility.HtmlDecode(match.Groups[2].Value).Trim();
                if (IsValidResult(title, url))
                {
                    results.Add($"• {title}\n  {url}");
                }
            }

            if (results.Count > 0)
            {
                return results.Distinct().Take(10).ToList();
            }

            // DDG HTML format fallback
            ddgMatches = System.Text.RegularExpressions.Regex.Matches(
                html,
                @"<a[^>]*rel=""nofollow""[^>]*class=""result__a""[^>]*href=""([^""]+)""[^>]*>([^<]+)</a>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in ddgMatches)
            {
                var url = match.Groups[1].Value;
                var title = System.Web.HttpUtility.HtmlDecode(match.Groups[2].Value).Trim();
                if (IsValidResult(title, url))
                {
                    results.Add($"• {title}\n  {url}");
                }
            }

            if (results.Count > 0)
            {
                return results.Distinct().Take(10).ToList();
            }
        }

        // Generic extraction - look for common result patterns
        var titleMatches = System.Text.RegularExpressions.Regex.Matches(
            html,
            @"<a[^>]*href=""(https?://[^""]+)""[^>]*>([^<]+)</a>",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in titleMatches)
        {
            var url = match.Groups[1].Value;
            var title = System.Web.HttpUtility.HtmlDecode(match.Groups[2].Value).Trim();

            if (IsValidResult(title, url))
            {
                results.Add($"• {title}\n  {url}");
            }
        }

        return results.Distinct().Take(10).ToList();
    }

    private static bool IsValidResult(string title, string url)
    {
        return title.Length > 10 && title.Length < 200 &&
               !url.Contains("javascript:") &&
               !url.Contains("#") &&
               !IsNavigationLink(title, url);
    }

    private static bool IsNavigationLink(string title, string url)
    {
        var lowerTitle = title.ToLowerInvariant();
        var lowerUrl = url.ToLowerInvariant();

        // Common navigation patterns to exclude
        return lowerTitle.Contains("login") ||
               lowerTitle.Contains("sign in") ||
               lowerTitle.Contains("privacy") ||
               lowerTitle.Contains("terms") ||
               lowerTitle.Contains("cookie") ||
               lowerUrl.Contains("/login") ||
               lowerUrl.Contains("/signin") ||
               lowerUrl.Contains("/account");
    }
}
