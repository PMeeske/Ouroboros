// <copyright file="QdrantAdminTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Ouroboros.Tools;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tool for Ouroboros to self-administer its Qdrant vector database.
/// Enables autonomous collection management, dimension adjustment, and neuro-symbolic health checks.
/// </summary>
public sealed class QdrantAdminTool : ITool
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly Func<string, CancellationToken, Task<float[]>>? _embedFunc;
    private readonly Func<string, CancellationToken, Task<string>>? _llmFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantAdminTool"/> class.
    /// </summary>
    public QdrantAdminTool(
        string qdrantEndpoint = "http://localhost:6333",
        Func<string, CancellationToken, Task<float[]>>? embedFunc = null,
        Func<string, CancellationToken, Task<string>>? llmFunc = null)
    {
        _baseUrl = qdrantEndpoint.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(60)
        };
        _embedFunc = embedFunc;
        _llmFunc = llmFunc;
    }

    /// <inheritdoc/>
    public string Name => "qdrant_admin";

    /// <inheritdoc/>
    public string Description => @"Administrate my Qdrant neuro-symbolic memory. Commands:
- status: Get all collections and their health
- diagnose: Check for dimension mismatches and issues
- fix <collection>: Auto-fix dimension mismatch by migrating data
- compact: Remove empty collections and optimize storage
- stats: Detailed statistics about my memory usage
- collections: List all collections with point counts
- compress: Analyze vector compression potential using Fourier/DCT transforms";

    /// <inheritdoc/>
    public string? JsonSchema => """
{
  "type": "object",
  "properties": {
    "command": {
      "type": "string",
      "enum": ["status", "diagnose", "fix", "compact", "stats", "collections", "compress"],
      "description": "The admin command to execute"
    },
    "collection": {
      "type": "string",
      "description": "Collection name for fix/compress commands"
    }
  },
  "required": ["command"]
}
""";

    /// <inheritdoc/>
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            string command = input.Trim().ToLowerInvariant();
            string? collection = null;

            // Try JSON parsing
            try
            {
                using var doc = JsonDocument.Parse(input);
                if (doc.RootElement.TryGetProperty("command", out var cmdEl))
                    command = cmdEl.GetString()?.ToLowerInvariant() ?? command;
                if (doc.RootElement.TryGetProperty("collection", out var colEl))
                    collection = colEl.GetString();
            }
            catch { /* Use raw input */ }

            return command switch
            {
                "status" => await GetStatusAsync(ct),
                "diagnose" => await DiagnoseAsync(ct),
                "fix" => await FixCollectionAsync(collection, ct),
                "compact" => await CompactAsync(ct),
                "stats" => await GetStatsAsync(ct),
                "collections" => await ListCollectionsAsync(ct),
                "compress" => await AnalyzeCompressionAsync(collection, ct),
                _ => Result<string, string>.Failure($"Unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Qdrant admin error: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> GetStatusAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Qdrant Neuro-Symbolic Memory Status\n");

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure("Cannot connect to Qdrant");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            sb.AppendLine($"**Connected to:** {_baseUrl}");
            sb.AppendLine($"**Total collections:** {collections.GetArrayLength()}\n");

            // Get current embedding dimension
            int? currentDim = null;
            if (_embedFunc != null)
            {
                try
                {
                    var testEmbed = await _embedFunc("test", ct);
                    currentDim = testEmbed.Length;
                    sb.AppendLine($"**Active embedding dimension:** {currentDim}");
                }
                catch { }
            }

            sb.AppendLine("\n## Collections\n");
            sb.AppendLine("| Collection | Points | Dimension | Status |");
            sb.AppendLine("|------------|--------|-----------|--------|");

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "unknown";
                var info = await GetCollectionInfoAsync(name, ct);

                string status = "✓ OK";
                if (currentDim.HasValue && info.Dimension > 0 && info.Dimension != currentDim.Value)
                    status = $"⚠ Dim mismatch ({info.Dimension}→{currentDim})";
                else if (info.Points == 0)
                    status = "○ Empty";

                sb.AppendLine($"| {name} | {info.Points:N0} | {info.Dimension} | {status} |");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Status check failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> DiagnoseAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Qdrant Diagnostic Report\n");

        var issues = new List<string>();
        var recommendations = new List<string>();

        try
        {
            // Get current embedding dimension
            int? currentDim = null;
            if (_embedFunc != null)
            {
                try
                {
                    var testEmbed = await _embedFunc("test", ct);
                    currentDim = testEmbed.Length;
                }
                catch (Exception ex)
                {
                    issues.Add($"Embedding function error: {ex.Message}");
                    recommendations.Add("Check embedding model configuration");
                }
            }
            else
            {
                issues.Add("No embedding function configured");
                recommendations.Add("Configure embedding model for semantic search");
            }

            // Check collections
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            int totalPoints = 0;
            int emptyCollections = 0;
            int dimensionMismatches = 0;
            var ouroborosCollections = new List<string>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("ouroboros_")) continue;

                ouroborosCollections.Add(name);
                var info = await GetCollectionInfoAsync(name, ct);
                totalPoints += info.Points;

                if (info.Points == 0)
                {
                    emptyCollections++;
                }

                if (currentDim.HasValue && info.Dimension > 0 && info.Dimension != currentDim.Value)
                {
                    dimensionMismatches++;
                    issues.Add($"Collection '{name}' has dimension {info.Dimension}, but current model uses {currentDim}");
                    recommendations.Add($"Run: fix {name}");
                }
            }

            sb.AppendLine($"**Ouroboros Collections:** {ouroborosCollections.Count}");
            sb.AppendLine($"**Total Points:** {totalPoints:N0}");
            sb.AppendLine($"**Empty Collections:** {emptyCollections}");
            sb.AppendLine($"**Dimension Mismatches:** {dimensionMismatches}\n");

            if (issues.Count == 0)
            {
                sb.AppendLine("## ✓ No Issues Found\n");
                sb.AppendLine("Neuro-symbolic memory is healthy.");
            }
            else
            {
                sb.AppendLine("## ⚠ Issues Found\n");
                foreach (var issue in issues)
                    sb.AppendLine($"- {issue}");

                sb.AppendLine("\n## Recommendations\n");
                foreach (var rec in recommendations)
                    sb.AppendLine($"- {rec}");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Diagnosis failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> FixCollectionAsync(string? collection, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(collection))
            return Result<string, string>.Failure("Specify collection name: fix <collection>");

        if (_embedFunc == null)
            return Result<string, string>.Failure("No embedding function available for migration");

        var sb = new StringBuilder();
        sb.AppendLine($"# Fixing Collection: {collection}\n");

        try
        {
            // Get current dimension
            var testEmbed = await _embedFunc("test", ct);
            int targetDim = testEmbed.Length;

            var info = await GetCollectionInfoAsync(collection, ct);
            if (info.Dimension == targetDim)
            {
                sb.AppendLine($"✓ Collection already has correct dimension ({targetDim})");
                return Result<string, string>.Success(sb.ToString());
            }

            sb.AppendLine($"Current dimension: {info.Dimension}");
            sb.AppendLine($"Target dimension: {targetDim}");
            sb.AppendLine($"Points to migrate: {info.Points}\n");

            if (info.Points == 0)
            {
                // Just recreate empty collection
                sb.AppendLine("Recreating empty collection...");
                await _httpClient.DeleteAsync($"/collections/{collection}", ct);
                await CreateCollectionAsync(collection, targetDim, ct);
                sb.AppendLine("✓ Collection recreated with correct dimension");
            }
            else
            {
                // Migrate data
                sb.AppendLine("Migrating data with new embeddings...");

                // Scroll all points
                var points = await ScrollAllPointsAsync(collection, ct);
                sb.AppendLine($"Retrieved {points.Count} points");

                // Delete and recreate
                await _httpClient.DeleteAsync($"/collections/{collection}", ct);
                await CreateCollectionAsync(collection, targetDim, ct);

                // Re-embed and insert
                int migrated = 0;
                int failed = 0;

                foreach (var (id, payload) in points)
                {
                    try
                    {
                        string text = ExtractTextFromPayload(payload);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            failed++;
                            continue;
                        }

                        var newEmbed = await _embedFunc(text, ct);
                        await UpsertPointAsync(collection, id, newEmbed, payload, ct);
                        migrated++;
                    }
                    catch
                    {
                        failed++;
                    }
                }

                sb.AppendLine($"\n✓ Migrated {migrated} points ({failed} failed)");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Fix failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> CompactAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Compacting Qdrant Storage\n");

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            int removed = 0;
            var removedNames = new List<string>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("ouroboros_")) continue;

                var info = await GetCollectionInfoAsync(name, ct);

                // Remove empty backup collections
                if (info.Points == 0 && name.Contains("_backup_"))
                {
                    await _httpClient.DeleteAsync($"/collections/{name}", ct);
                    removedNames.Add(name);
                    removed++;
                }
            }

            sb.AppendLine($"**Removed {removed} empty backup collections:**");
            foreach (var name in removedNames)
                sb.AppendLine($"- {name}");

            if (removed == 0)
                sb.AppendLine("No empty backup collections to remove.");

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Compact failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> GetStatsAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Neuro-Symbolic Memory Statistics\n");

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            long totalPoints = 0;
            var categoryStats = new Dictionary<string, (int collections, long points)>();

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (!name.StartsWith("ouroboros_")) continue;

                var info = await GetCollectionInfoAsync(name, ct);
                totalPoints += info.Points;

                // Categorize by type
                var category = name.Replace("ouroboros_", "").Split('_')[0];
                if (!categoryStats.ContainsKey(category))
                    categoryStats[category] = (0, 0);
                categoryStats[category] = (categoryStats[category].collections + 1, categoryStats[category].points + info.Points);
            }

            sb.AppendLine($"**Total Points:** {totalPoints:N0}");
            sb.AppendLine($"**Categories:** {categoryStats.Count}\n");

            sb.AppendLine("## By Category\n");
            sb.AppendLine("| Category | Collections | Points |");
            sb.AppendLine("|----------|-------------|--------|");

            foreach (var (cat, stats) in categoryStats.OrderByDescending(x => x.Value.points))
            {
                sb.AppendLine($"| {cat} | {stats.collections} | {stats.points:N0} |");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Stats failed: {ex.Message}");
        }
    }

    private async Task<Result<string, string>> ListCollectionsAsync(CancellationToken ct)
    {
        var sb = new StringBuilder();

        try
        {
            var response = await _httpClient.GetAsync("/collections", ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString() ?? "";
                if (name.StartsWith("ouroboros_"))
                {
                    var info = await GetCollectionInfoAsync(name, ct);
                    sb.AppendLine($"- {name}: {info.Points} points (dim={info.Dimension})");
                }
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"List failed: {ex.Message}");
        }
    }

    private async Task<(int Points, int Dimension)> GetCollectionInfoAsync(string name, CancellationToken ct)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/collections/{name}", ct);
            if (!response.IsSuccessStatusCode)
                return (0, 0);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");

            int points = result.TryGetProperty("points_count", out var pc) ? pc.GetInt32() : 0;
            int dim = 0;

            if (result.TryGetProperty("config", out var cfg) &&
                cfg.TryGetProperty("params", out var prm) &&
                prm.TryGetProperty("vectors", out var vec))
            {
                if (vec.TryGetProperty("size", out var sz))
                    dim = sz.GetInt32();
            }

            return (points, dim);
        }
        catch
        {
            return (0, 0);
        }
    }

    private async Task CreateCollectionAsync(string name, int vectorSize, CancellationToken ct)
    {
        var payload = new { vectors = new { size = vectorSize, distance = "Cosine" } };
        await _httpClient.PutAsJsonAsync($"/collections/{name}", payload, ct);
    }

    private async Task<List<(string Id, Dictionary<string, object> Payload)>> ScrollAllPointsAsync(
        string collection, CancellationToken ct)
    {
        var results = new List<(string, Dictionary<string, object>)>();
        string? offset = null;

        while (true)
        {
            var request = new { limit = 100, offset = offset, with_payload = true, with_vector = false };
            var response = await _httpClient.PostAsJsonAsync($"/collections/{collection}/points/scroll", request, ct);

            if (!response.IsSuccessStatusCode) break;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");
            var points = result.GetProperty("points");

            foreach (var point in points.EnumerateArray())
            {
                var id = point.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString();
                var payloadDict = new Dictionary<string, object>();

                if (point.TryGetProperty("payload", out var payloadProp))
                {
                    foreach (var prop in payloadProp.EnumerateObject())
                    {
                        payloadDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }
                }

                results.Add((id, payloadDict));
            }

            if (result.TryGetProperty("next_page_offset", out var nextOffset) &&
                nextOffset.ValueKind != JsonValueKind.Null)
            {
                offset = nextOffset.ToString();
            }
            else
            {
                break;
            }
        }

        return results;
    }

    private async Task UpsertPointAsync(string collection, string id, float[] vector, Dictionary<string, object> payload, CancellationToken ct)
    {
        var point = new { id = id, vector = vector, payload = payload };
        var request = new { points = new[] { point } };
        await _httpClient.PutAsJsonAsync($"/collections/{collection}/points", request, ct);
    }

    private static string ExtractTextFromPayload(Dictionary<string, object> payload)
    {
        var textParts = new List<string>();

        // Try common text fields
        string[] textFields = { "content", "text", "description", "title", "topic", "user_message", "assistant_response", "rationale" };
        foreach (var field in textFields)
        {
            if (payload.TryGetValue(field, out var value) && value is string s && !string.IsNullOrWhiteSpace(s))
            {
                textParts.Add(s);
            }
        }

        return string.Join(" ", textParts);
    }

    private async Task<Result<string, string>> AnalyzeCompressionAsync(string? collection, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Fourier Vector Compression Analysis\n");

        try
        {
            // Get all collections if none specified
            var response = await _httpClient.GetAsync("/collections", ct);
            if (!response.IsSuccessStatusCode)
                return Result<string, string>.Failure("Cannot connect to Qdrant");

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var collections = json.GetProperty("result").GetProperty("collections");

            var collectionsToAnalyze = new List<string>();
            foreach (var col in collections.EnumerateArray())
            {
                var name = col.GetProperty("name").GetString()!;
                if (collection == null || name.Equals(collection, StringComparison.OrdinalIgnoreCase))
                {
                    collectionsToAnalyze.Add(name);
                }
            }

            if (collectionsToAnalyze.Count == 0)
            {
                return Result<string, string>.Failure($"Collection '{collection}' not found");
            }

            var compressor = new Ouroboros.Domain.VectorCompression.VectorCompressionService(128, 0.95);
            long totalOriginalBytes = 0;
            long totalPotentialBytes = 0;

            foreach (var colName in collectionsToAnalyze)
            {
                // Get collection info
                var infoResponse = await _httpClient.GetAsync($"/collections/{colName}", ct);
                if (!infoResponse.IsSuccessStatusCode) continue;

                var info = await infoResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                var result = info.GetProperty("result");
                var config = result.GetProperty("config");
                var vectorConfig = config.GetProperty("params");
                int dimension = vectorConfig.GetProperty("size").GetInt32();
                int pointCount = result.GetProperty("points_count").GetInt32();

                sb.AppendLine($"## Collection: `{colName}`");
                sb.AppendLine($"- Current dimension: **{dimension}**");
                sb.AppendLine($"- Point count: **{pointCount:N0}**");

                // Sample some vectors for compression analysis
                if (pointCount > 0)
                {
                    var vectors = await SampleVectorsAsync(colName, Math.Min(100, pointCount), ct);

                    if (vectors.Count > 0)
                    {
                        // Analyze compression potential
                        var preview = compressor.Preview(vectors[0], Ouroboros.Domain.VectorCompression.VectorCompressionService.CompressionMethod.DCT);

                        sb.AppendLine($"\n### Compression Analysis (sampled {vectors.Count} vectors):");
                        sb.AppendLine($"- Original size per vector: **{preview.OriginalSizeBytes:N0} bytes**");
                        sb.AppendLine($"- DCT compressed: **{preview.DCTCompressedSize:N0} bytes** ({preview.DCTEnergyRetained:P1} energy retained)");
                        sb.AppendLine($"- FFT compressed: **{preview.FFTCompressedSize:N0} bytes** (ratio: {preview.FFTCompressionRatio:F1}x)");
                        sb.AppendLine($"- Quantized DCT: **{preview.QuantizedDCTSize:N0} bytes** (8-bit)");
                        sb.AppendLine($"- Best compression: **{preview.BestCompressionRatio:F1}x** using {preview.RecommendedMethod}");

                        // Calculate total savings
                        long origTotal = (long)pointCount * preview.OriginalSizeBytes;
                        long compTotal = (long)pointCount * preview.QuantizedDCTSize;

                        totalOriginalBytes += origTotal;
                        totalPotentialBytes += compTotal;

                        sb.AppendLine($"\n### Potential Savings for Collection:");
                        sb.AppendLine($"- Current storage: **{FormatBytes(origTotal)}**");
                        sb.AppendLine($"- Compressed storage: **{FormatBytes(compTotal)}**");
                        sb.AppendLine($"- Savings: **{FormatBytes(origTotal - compTotal)}** ({(1 - (double)compTotal / origTotal):P1} reduction)");

                        // Spectral analysis
                        var spectralInfo = AnalyzeSpectralContent(vectors);
                        sb.AppendLine($"\n### Spectral Profile:");
                        sb.AppendLine($"- Energy concentration: {spectralInfo.EnergyConcentration:P1} in first 25% of spectrum");
                        sb.AppendLine($"- Effective dimensionality: ~{spectralInfo.EffectiveDimension} (where 95% energy lives)");
                        sb.AppendLine($"- Recommended target dim: **{spectralInfo.RecommendedDimension}**");
                    }
                }

                sb.AppendLine();
            }

            if (totalOriginalBytes > 0)
            {
                sb.AppendLine("## Total Compression Summary");
                sb.AppendLine($"- Total current storage: **{FormatBytes(totalOriginalBytes)}**");
                sb.AppendLine($"- Total potential: **{FormatBytes(totalPotentialBytes)}**");
                sb.AppendLine($"- Total savings: **{FormatBytes(totalOriginalBytes - totalPotentialBytes)}**");
                sb.AppendLine($"- Overall compression: **{(double)totalOriginalBytes / totalPotentialBytes:F1}x**");
            }

            return Result<string, string>.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Compression analysis failed: {ex.Message}");
        }
    }

    private async Task<List<float[]>> SampleVectorsAsync(string collection, int count, CancellationToken ct)
    {
        var vectors = new List<float[]>();

        try
        {
            var scrollRequest = new { limit = count, with_vector = true };
            var response = await _httpClient.PostAsJsonAsync($"/collections/{collection}/points/scroll", scrollRequest, ct);

            if (!response.IsSuccessStatusCode)
                return vectors;

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var points = json.GetProperty("result").GetProperty("points");

            foreach (var point in points.EnumerateArray())
            {
                if (point.TryGetProperty("vector", out var vectorEl))
                {
                    var vec = new List<float>();
                    foreach (var v in vectorEl.EnumerateArray())
                    {
                        vec.Add(v.GetSingle());
                    }

                    if (vec.Count > 0)
                        vectors.Add(vec.ToArray());
                }
            }
        }
        catch
        {
            // Ignore sampling errors
        }

        return vectors;
    }

    private static (double EnergyConcentration, int EffectiveDimension, int RecommendedDimension) AnalyzeSpectralContent(List<float[]> vectors)
    {
        if (vectors.Count == 0 || vectors[0].Length == 0)
            return (0, 0, 64);

        int dim = vectors[0].Length;
        var dctCompressor = new Ouroboros.Domain.VectorCompression.DCTVectorCompressor(dim, 1.0);

        // Compute average energy distribution across DCT coefficients
        var avgEnergy = new double[dim];
        double totalEnergy = 0;

        foreach (var vec in vectors)
        {
            var compressed = dctCompressor.Compress(vec);
            for (int i = 0; i < compressed.Coefficients.Length && i < dim; i++)
            {
                double e = compressed.Coefficients[i] * compressed.Coefficients[i];
                avgEnergy[i] += e;
                totalEnergy += e;
            }
        }

        if (totalEnergy < double.Epsilon)
            return (0, dim, Math.Min(128, dim));

        // Normalize
        for (int i = 0; i < dim; i++)
            avgEnergy[i] /= totalEnergy;

        // Find energy concentration in first 25%
        int quarter = dim / 4;
        double energyInQuarter = avgEnergy.Take(quarter).Sum();

        // Find effective dimension (95% cumulative energy)
        double cumulative = 0;
        int effective = dim;
        for (int i = 0; i < dim; i++)
        {
            cumulative += avgEnergy[i];
            if (cumulative >= 0.95)
            {
                effective = i + 1;
                break;
            }
        }

        // Recommend dimension based on effective + safety margin
        int recommended = Math.Min(dim, Math.Max(64, (int)(effective * 1.1)));

        // Round to nice power-of-2-ish numbers
        int[] niceDims = { 64, 96, 128, 192, 256, 384, 512, 768, 1024 };
        recommended = niceDims.FirstOrDefault(d => d >= recommended);
        if (recommended == 0) recommended = dim;

        return (energyInQuarter, effective, recommended);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:F1} {sizes[order]}";
    }
}
