using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FixState = Ouroboros.Roslynator.Pipeline.FixState;

namespace Ouroboros.Roslynator.Pipeline.Steps;

/// <summary>
/// Ollama AI pipeline step implemented as a self-contained Kleisli-like function.
/// Reads configuration from environment variables:
///  - OLLAMA_ENDPOINT (default http://localhost:11434/api/generate)
///  - OLLAMA_MODEL (default "codellama")
/// </summary>
public static class OllamaSteps
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(2) };

    private static string Endpoint => Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT")
        ?? "http://localhost:11434/api/generate";

    private static string Model => Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "codellama";

    /// <summary>
    /// Main Ollama step. Returns the same state if no change is made or on failure.
    /// </summary>
    public static async Task<FixState> GenerateFix(FixState state)
    {
        if (!state.Changes.IsEmpty) return state;

        try
        {
            var span = state.Diagnostic.Location.SourceSpan;
            var node = state.CurrentRoot.FindNode(span);
            var code = node.ToFullString();

            var prompt = BuildPrompt(state.Diagnostic.Id, state.Diagnostic.GetMessage(), code);

            var requestObj = new
            {
                model = Model,
                prompt = prompt,
                stream = false,
                options = new { temperature = 0.05, top_p = 0.95 }
            };

            var json = JsonSerializer.Serialize(requestObj);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.PostAsync(Endpoint, content, state.CancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return state;

            var payload = await resp.Content.ReadAsStringAsync(state.CancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(payload);

            // Ollama JSON may vary — look for "response" string or first text segment
            string? raw = null;
            if (doc.RootElement.TryGetProperty("response", out var r))
                raw = r.GetString();
            else if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                raw = results[0].GetProperty("output").GetString();

            var fixedCode = CleanOutput(raw);
            if (string.IsNullOrWhiteSpace(fixedCode) || fixedCode == code) return state;

            // parse produced code back into a SyntaxNode (best-effort)
            var fixedNode = CSharpSyntaxTree.ParseText(fixedCode).GetRoot().DescendantNodesAndSelf().FirstOrDefault();
            if (fixedNode is null) return state;

            var newRoot = state.CurrentRoot.ReplaceNode(node, fixedNode);
            return state.WithNewRoot(newRoot, "Ollama AI Fix");
        }
#pragma warning disable CA1031 // Fail gracefully on any LLM/parsing errors
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Fail gracefully: log and return unchanged state
            System.Diagnostics.Debug.WriteLine($"[OllamaStep] Error: {ex.Message}");
            return state;
        }
    }

    private static string BuildPrompt(string id, string message, string code) =>
$@"You are a C# expert. Fix this exact compiler diagnostic: [{id}] {message}
Return only the corrected code snippet (no explanation, no markdown code fences).
Code to fix:
```csharp
{code}
```";

    private static string CleanOutput(string? output)
    {
        if (string.IsNullOrWhiteSpace(output)) return string.Empty;
        var clean = Regex.Replace(output, @"^```[a-zA-Z]*\s*", "", RegexOptions.Multiline);
        clean = Regex.Replace(clean, @"\s*```$", "", RegexOptions.Multiline);
        return clean.Trim();
    }
}
