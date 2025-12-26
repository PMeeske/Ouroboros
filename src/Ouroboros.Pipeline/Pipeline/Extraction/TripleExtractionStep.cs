// <copyright file="TripleExtractionStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Extraction;

using System.Text.RegularExpressions;

/// <summary>
/// Represents a semantic triple extracted from a document.
/// </summary>
/// <param name="Subject">The subject of the triple (e.g., a document).</param>
/// <param name="Predicate">The predicate/relation (e.g., Author, Status, Topic).</param>
/// <param name="Object">The object of the triple (e.g., a user, state, concept).</param>
public sealed record SemanticTriple(string Subject, string Predicate, string Object)
{
    /// <summary>
    /// Converts the triple to a MeTTa fact representation.
    /// </summary>
    /// <returns>The MeTTa fact string.</returns>
    public string ToMeTTaFact() => $"({this.Predicate} (Doc \"{this.Subject}\") ({this.InferObjectType()} \"{this.Object}\"))";

    /// <summary>
    /// Infers the object type based on the predicate.
    /// </summary>
    private string InferObjectType() => this.Predicate switch
    {
        "Author" or "CreatedBy" or "ModifiedBy" => "User",
        "Status" => "State",
        "Topic" or "Contains" => "Concept",
        "References" or "DependsOn" => "Doc",
        _ => "Entity",
    };
}

/// <summary>
/// Extraction result containing all triples from a document.
/// </summary>
/// <param name="DocumentId">The source document identifier.</param>
/// <param name="Triples">The extracted semantic triples.</param>
public sealed record ExtractionResult(string DocumentId, IReadOnlyList<SemanticTriple> Triples);

/// <summary>
/// Pipeline step that extracts semantic triples from documents using an LLM.
/// </summary>
public sealed class TripleExtractionStep
{
    private readonly ToolAwareChatModel _llm;
    private static readonly string ExtractionPrompt = """
        Extract semantic triples from the following document content.
        Output each triple on a new line in the format: (Relation Subject Object)
        
        Valid relations are:
        - Author: who wrote/created the document
        - Status: the document's state (Outdated, Current, Draft, Reviewed, Archived)
        - Topic: what the document is about (use concise topic names)
        - Contains: entities mentioned in the document
        - References: other documents referenced
        - DependsOn: dependencies
        
        Document ID: {document_id}
        Document Content:
        {content}
        
        Extract triples:
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="TripleExtractionStep"/> class.
    /// </summary>
    /// <param name="llm">The LLM to use for extraction.</param>
    public TripleExtractionStep(ToolAwareChatModel llm)
    {
        this._llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <summary>
    /// Extracts semantic triples from a document.
    /// </summary>
    /// <param name="documentId">The document identifier.</param>
    /// <param name="content">The document content.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The extraction result containing triples.</returns>
    public async Task<Result<ExtractionResult, string>> ExtractAsync(
        string documentId,
        string content,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        ArgumentNullException.ThrowIfNull(content);

        try
        {
            string prompt = ExtractionPrompt
                .Replace("{document_id}", documentId)
                .Replace("{content}", content);

            (string response, _) = await this._llm.GenerateWithToolsAsync(prompt);

            List<SemanticTriple> triples = this.ParseTriples(documentId, response);

            return Result<ExtractionResult, string>.Success(new ExtractionResult(documentId, triples));
        }
        catch (Exception ex)
        {
            return Result<ExtractionResult, string>.Failure($"Triple extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a Kleisli arrow for triple extraction.
    /// </summary>
    /// <returns>A step that extracts triples from document content.</returns>
    public Step<(string DocumentId, string Content), Result<ExtractionResult, string>> AsArrow()
        => input => this.ExtractAsync(input.DocumentId, input.Content);

    /// <summary>
    /// Parses LLM output into semantic triples.
    /// </summary>
    private List<SemanticTriple> ParseTriples(string documentId, string llmOutput)
    {
        List<SemanticTriple> triples = new();

        // Match patterns like (Relation Subject Object) or Relation: Subject -> Object
        Regex triplePattern = new(@"\((\w+)\s+([^\s)]+)\s+([^)]+)\)", RegexOptions.Compiled);
        
        foreach (Match match in triplePattern.Matches(llmOutput))
        {
            if (match.Groups.Count == 4)
            {
                string predicate = match.Groups[1].Value;
                string obj = match.Groups[3].Value.Trim().Trim('"');
                
                triples.Add(new SemanticTriple(documentId, predicate, obj));
            }
        }

        // Also try alternative format: Relation: Object
        Regex simplePattern = new(@"(\w+):\s*(.+)", RegexOptions.Compiled);
        foreach (string line in llmOutput.Split('\n'))
        {
            Match match = simplePattern.Match(line.Trim());
            if (match.Success && !line.Contains('('))
            {
                string predicate = match.Groups[1].Value;
                string obj = match.Groups[2].Value.Trim().Trim('"');
                
                // Only add if it's a valid predicate
                if (IsValidPredicate(predicate))
                {
                    triples.Add(new SemanticTriple(documentId, predicate, obj));
                }
            }
        }

        return triples;
    }

    /// <summary>
    /// Checks if a predicate is valid.
    /// </summary>
    private static bool IsValidPredicate(string predicate) => predicate switch
    {
        "Author" or "Status" or "Topic" or "Contains" or
        "References" or "DependsOn" or "CreatedBy" or "ModifiedBy" or "Type" => true,
        _ => false,
    };
}
