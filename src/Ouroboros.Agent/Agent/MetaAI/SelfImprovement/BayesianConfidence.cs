#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Bayesian Confidence Updating
// Implements Bayes' theorem for hypothesis testing
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Implements Bayesian confidence updating for hypothesis testing.
/// Uses Bayes' theorem: P(H|E) = P(E|H) * P(H) / P(E)
/// where P(E) = P(E|H)*P(H) + P(E|¬H)*P(¬H)
/// </summary>
/// <remarks>
/// References:
/// - Bayes' Theorem: https://en.wikipedia.org/wiki/Bayes%27_theorem
/// - Jeffreys' Scale: https://en.wikipedia.org/wiki/Bayes_factor#Interpretation
/// </remarks>
public static class BayesianConfidence
{
    /// <summary>
    /// Updates a prior probability given new evidence using Bayes' theorem.
    /// </summary>
    /// <param name="prior">P(H) — current confidence in hypothesis (0-1)</param>
    /// <param name="likelihoodIfTrue">P(E|H) — probability of observing this evidence if H is true (0-1)</param>
    /// <param name="likelihoodIfFalse">P(E|¬H) — probability of observing this evidence if H is false (0-1)</param>
    /// <returns>P(H|E) — updated confidence, clamped to [0.001, 0.999] to avoid certainty</returns>
    public static double Update(double prior, double likelihoodIfTrue, double likelihoodIfFalse)
    {
        // P(E) = P(E|H)*P(H) + P(E|¬H)*P(¬H)
        var pEvidence = (likelihoodIfTrue * prior) + (likelihoodIfFalse * (1.0 - prior));
        
        if (pEvidence <= 0) return prior; // No evidence, no update
        
        // P(H|E) = P(E|H) * P(H) / P(E)
        var posterior = (likelihoodIfTrue * prior) / pEvidence;
        
        return Math.Clamp(posterior, 0.001, 0.999); // Avoid absolute certainty
    }

    /// <summary>
    /// Computes the likelihood ratio (Bayes factor) for evidence strength.
    /// Values > 1 support H, values &lt; 1 support ¬H.
    /// </summary>
    /// <param name="likelihoodIfTrue">P(E|H) — probability of evidence if H is true</param>
    /// <param name="likelihoodIfFalse">P(E|¬H) — probability of evidence if H is false</param>
    /// <returns>Bayes factor (likelihood ratio)</returns>
    public static double BayesFactor(double likelihoodIfTrue, double likelihoodIfFalse)
    {
        if (likelihoodIfFalse <= 0) return double.PositiveInfinity;
        return likelihoodIfTrue / likelihoodIfFalse;
    }

    /// <summary>
    /// Categorizes evidence strength based on Jeffreys' scale.
    /// </summary>
    /// <param name="bayesFactor">The Bayes factor to categorize</param>
    /// <returns>Evidence strength category</returns>
    /// <remarks>
    /// Jeffreys' scale uses log10 of the Bayes factor:
    /// - &lt; 0.5: Negligible
    /// - 0.5-1.0: Substantial
    /// - 1.0-1.5: Strong
    /// - 1.5-2.0: Very Strong
    /// - &gt; 2.0: Decisive
    /// </remarks>
    public static EvidenceStrength CategorizeEvidence(double bayesFactor)
    {
        var k = Math.Abs(Math.Log10(bayesFactor));
        return k switch
        {
            < 0.5 => EvidenceStrength.Negligible,
            < 1.0 => EvidenceStrength.Substantial,
            < 1.5 => EvidenceStrength.Strong,
            < 2.0 => EvidenceStrength.VeryStrong,
            _ => EvidenceStrength.Decisive
        };
    }
}

/// <summary>
/// Evidence strength categories based on Jeffreys' scale.
/// </summary>
public enum EvidenceStrength
{
    /// <summary>
    /// Negligible evidence (log10 Bayes factor &lt; 0.5)
    /// </summary>
    Negligible,
    
    /// <summary>
    /// Substantial evidence (log10 Bayes factor 0.5-1.0)
    /// </summary>
    Substantial,
    
    /// <summary>
    /// Strong evidence (log10 Bayes factor 1.0-1.5)
    /// </summary>
    Strong,
    
    /// <summary>
    /// Very strong evidence (log10 Bayes factor 1.5-2.0)
    /// </summary>
    VeryStrong,
    
    /// <summary>
    /// Decisive evidence (log10 Bayes factor &gt; 2.0)
    /// </summary>
    Decisive
}
