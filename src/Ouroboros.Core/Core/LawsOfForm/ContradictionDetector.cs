// <copyright file="ContradictionDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

/// <summary>
/// Detects contradictions in LLM responses using Laws of Form re-entry pattern.
/// Uses the principle: f = ⌐f → Imaginary (self-contradiction).
/// </summary>
public sealed class ContradictionDetector
{
    private readonly IClaimExtractor claimExtractor;
    private readonly double similarityThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContradictionDetector"/> class.
    /// </summary>
    /// <param name="claimExtractor">Service for extracting claims from text.</param>
    /// <param name="similarityThreshold">Threshold for considering claims as referring to same topic (default 0.8).</param>
    public ContradictionDetector(
        IClaimExtractor claimExtractor,
        double similarityThreshold = 0.8)
    {
        this.claimExtractor = claimExtractor ?? throw new ArgumentNullException(nameof(claimExtractor));
        this.similarityThreshold = similarityThreshold;
    }

    /// <summary>
    /// Analyzes an LLM response for internal contradictions.
    /// Returns Imaginary if contradictions found, Mark if consistent, Void if insufficient claims.
    /// </summary>
    /// <param name="response">The LLM response to analyze.</param>
    /// <returns>A Form representing the consistency state.</returns>
    public Form Analyze(LlmResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Extract claims from the response
        var claims = this.claimExtractor.ExtractClaims(response.Text, response.ModelName ?? "unknown");

        if (claims.Count < 2)
        {
            // Not enough claims to detect contradictions
            return Form.Void;
        }

        // Check all pairs of claims for contradictions
        for (int i = 0; i < claims.Count; i++)
        {
            for (int j = i + 1; j < claims.Count; j++)
            {
                var pairCheck = this.CheckPair(claims[i], claims[j]);

                if (pairCheck.IsImaginary())
                {
                    // Contradiction detected
                    return Form.Imaginary;
                }
            }
        }

        // No contradictions found
        return Form.Mark;
    }

    /// <summary>
    /// Analyzes multiple responses for contradictions across models/attempts.
    /// </summary>
    /// <param name="responses">The responses to analyze.</param>
    /// <returns>A Form representing cross-response consistency.</returns>
    public Form AnalyzeMultiple(IEnumerable<LlmResponse> responses)
    {
        var responseList = responses.ToList();

        if (responseList.Count < 2)
        {
            return Form.Void;
        }

        // Extract all claims from all responses
        var allClaims = responseList
            .SelectMany(r => this.claimExtractor.ExtractClaims(r.Text, r.ModelName ?? "unknown"))
            .ToList();

        if (allClaims.Count < 2)
        {
            return Form.Void;
        }

        // Check for contradictions across all claims
        for (int i = 0; i < allClaims.Count; i++)
        {
            for (int j = i + 1; j < allClaims.Count; j++)
            {
                var pairCheck = this.CheckPair(allClaims[i], allClaims[j]);

                if (pairCheck.IsImaginary())
                {
                    return Form.Imaginary;
                }
            }
        }

        return Form.Mark;
    }

    /// <summary>
    /// Checks if two claims contradict each other.
    /// Returns Imaginary if contradictory, Mark if consistent, Void if unrelated.
    /// </summary>
    /// <param name="claim1">The first claim.</param>
    /// <param name="claim2">The second claim.</param>
    /// <returns>A Form indicating the relationship between claims.</returns>
    public Form CheckPair(Claim claim1, Claim claim2)
    {
        ArgumentNullException.ThrowIfNull(claim1);
        ArgumentNullException.ThrowIfNull(claim2);

        // Check if claims are about the same topic
        var similarity = this.CalculateSimilarity(claim1.Statement, claim2.Statement);

        if (similarity < this.similarityThreshold)
        {
            // Claims are unrelated
            return Form.Void;
        }

        // Check if both claims are confident about opposite things
        // This is the re-entry pattern: f = ⌐f
        var claim1Form = claim1.Confidence.ToForm();
        var claim2Form = claim2.Confidence.ToForm();

        // If one claim is positive (Mark) and the other is negative (Void) about the same thing
        // and both are confident, we have a contradiction
        if (claim1Form.IsMark() && claim2Form.IsMark())
        {
            // Both confident - check semantic opposition
            if (this.AreOpposite(claim1.Statement, claim2.Statement))
            {
                // Re-entry detected: f = ⌐f → Imaginary
                return Form.Imaginary;
            }

            // Both confident and similar but not opposite - consistent
            return Form.Mark;
        }

        // At least one claim is uncertain or weak - not enough confidence to detect contradiction
        return Form.Void;
    }

    /// <summary>
    /// Calculates semantic similarity between two statements.
    /// In a real implementation, this would use embeddings or NLP.
    /// </summary>
    /// <param name="statement1">The first statement.</param>
    /// <param name="statement2">The second statement.</param>
    /// <returns>Similarity score from 0.0 to 1.0.</returns>
    private double CalculateSimilarity(string statement1, string statement2)
    {
        // Simple implementation: word overlap
        // In production, use embeddings or semantic similarity models
        var words1 = statement1.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var words2 = statement2.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        if (words1.Count == 0 || words2.Count == 0)
        {
            return 0.0;
        }

        var intersection = words1.Intersect(words2).Count();
        var union = words1.Union(words2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    /// <summary>
    /// Checks if two statements are semantically opposite.
    /// In a real implementation, this would use NLP for negation detection.
    /// </summary>
    /// <param name="statement1">The first statement.</param>
    /// <param name="statement2">The second statement.</param>
    /// <returns>True if the statements are opposite.</returns>
    private bool AreOpposite(string statement1, string statement2)
    {
        // Simple heuristic: check for negation words
        var negationWords = new HashSet<string> { "not", "no", "never", "cannot", "can't", "won't", "don't", "doesn't", "isn't", "aren't" };

        var words1 = statement1.ToLowerInvariant().Split(' ');
        var words2 = statement2.ToLowerInvariant().Split(' ');

        var hasNegation1 = words1.Any(w => negationWords.Contains(w));
        var hasNegation2 = words2.Any(w => negationWords.Contains(w));

        // If one has negation and the other doesn't, they might be opposite
        // This is a very simplified heuristic
        return hasNegation1 != hasNegation2;
    }
}

/// <summary>
/// Interface for extracting claims from text.
/// Abstracts the claim extraction logic for testability and flexibility.
/// </summary>
public interface IClaimExtractor
{
    /// <summary>
    /// Extracts verifiable claims from text.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <param name="source">The source of the text (e.g., model name).</param>
    /// <returns>A list of extracted claims.</returns>
    IReadOnlyList<Claim> ExtractClaims(string text, string source);
}

/// <summary>
/// Represents a factual claim extracted from text.
/// </summary>
public sealed record Claim
{
    /// <summary>
    /// Gets the statement text.
    /// </summary>
    public string Statement { get; init; }

    /// <summary>
    /// Gets the confidence in this claim (0.0 to 1.0).
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the source of this claim (e.g., model name, document reference).
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Claim"/> class.
    /// </summary>
    /// <param name="statement">The claim statement.</param>
    /// <param name="confidence">The confidence score.</param>
    /// <param name="source">The source of the claim.</param>
    public Claim(string statement, double confidence, string source)
    {
        this.Statement = statement;
        this.Confidence = Math.Clamp(confidence, 0.0, 1.0);
        this.Source = source;
    }
}

/// <summary>
/// Simple claim extractor that splits text into sentences.
/// In production, this would use NLP for proper claim extraction.
/// </summary>
public sealed class SimpleClaimExtractor : IClaimExtractor
{
    /// <inheritdoc/>
    public IReadOnlyList<Claim> ExtractClaims(string text, string source)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<Claim>();
        }

        // Simple sentence splitting (production would use NLP)
        var sentences = text
            .Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 10) // Filter out very short fragments
            .Select(s => new Claim(s, 0.8, source)) // Default confidence
            .ToList();

        return sentences;
    }
}
