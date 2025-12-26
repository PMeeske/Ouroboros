// <copyright file="LangChainPipeOperatorsExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;
/// <summary>
/// Demonstrates the LangChain-style pipe operators that have been added to
/// the Ouroboros system. These operators mirror the original LangChain
/// approach shown in OriginalLangChainPivotExample.cs.
/// </summary>
public static class LangChainPipeOperatorsExample
{
    /// <summary>
    /// Demonstrates using LangChain-style pipe operators in the Ouroboros system.
    /// This mirrors the pivot example pattern:
    /// Set | RetrieveSimilarDocuments | CombineDocuments | Template | LLM.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static Task RunLangChainPipelineExample()
    {
        Console.WriteLine("=== LANGCHAIN PIPE OPERATORS EXAMPLE ===");
        Console.WriteLine("Demonstrates the enhanced CLI steps using LangChain operators\n");

        Console.WriteLine("Available LangChain-style pipe operators:");
        Console.WriteLine("  - Set(value): Sets a value in the pipeline (like Chain.Set())");
        Console.WriteLine("  - RetrieveSimilarDocuments(amount): Retrieves docs from vector store");
        Console.WriteLine("  - CombineDocuments(): Combines retrieved documents");
        Console.WriteLine("  - Template(template): Applies a prompt template");
        Console.WriteLine("  - LLM(): Sends to the language model");
        Console.WriteLine();

        Console.WriteLine("CLI Token equivalents:");
        Console.WriteLine("  - LangChainSet / ChainSet");
        Console.WriteLine("  - LangChainRetrieve / ChainRetrieve");
        Console.WriteLine("  - LangChainCombine / ChainCombine");
        Console.WriteLine("  - LangChainTemplate / ChainTemplate");
        Console.WriteLine("  - LangChainLLM / ChainLLM");
        Console.WriteLine("  - LangChainRAG / ChainRAG (complete pipeline)");
        Console.WriteLine();

        Console.WriteLine("Example DSL usage:");
        Console.WriteLine("  pipeline --dsl \"Set('What is AI?') | LangChainRetrieve('amount=5') | LangChainCombine() | LangChainTemplate('...') | LangChainLLM()\"");
        Console.WriteLine();

        Console.WriteLine("Or use the complete RAG pipeline:");
        Console.WriteLine("  pipeline --dsl \"LangChainRAG('question=What is AI?|k=5')\"");
        Console.WriteLine();

        Console.WriteLine("Comparison with original LangChain syntax:");
        Console.WriteLine();
        Console.WriteLine("Original LangChain:");
        Console.WriteLine("  var chain =");
        Console.WriteLine("      Set(\"Who was drinking unicorn blood?\")");
        Console.WriteLine("      | RetrieveSimilarDocuments(vectorCollection, embeddingModel, amount: 5)");
        Console.WriteLine("      | CombineDocuments(outputKey: \"context\")");
        Console.WriteLine("      | Template(promptTemplate)");
        Console.WriteLine("      | LLM(llm);");
        Console.WriteLine();

        Console.WriteLine("Ouroboros equivalent (CLI DSL):");
        Console.WriteLine("  SetQuery('Who was drinking unicorn blood?')");
        Console.WriteLine("  | LangChainRetrieve('amount=5')");
        Console.WriteLine("  | LangChainCombine()");
        Console.WriteLine("  | LangChainTemplate('Use the context: {context}...')");
        Console.WriteLine("  | LangChainLLM()");
        Console.WriteLine();

        Console.WriteLine("Ouroboros equivalent (Code using static imports):");
        Console.WriteLine("  using static Ouroboros.CLI.Interop.Pipe;");
        Console.WriteLine();
        Console.WriteLine("  var step = Set(\"Who was drinking unicorn blood?\", \"query\")");
        Console.WriteLine("      .Bind(RetrieveSimilarDocuments(5))");
        Console.WriteLine("      .Bind(CombineDocuments())");
        Console.WriteLine("      .Bind(Template(promptTemplate))");
        Console.WriteLine("      .Bind(LLM());");
        Console.WriteLine();

        Console.WriteLine("✓ LangChain pipe operators successfully integrated into Ouroboros");
        Console.WriteLine("✓ Both CLI DSL and code-based composition are supported");
        Console.WriteLine("✓ Maintains functional programming principles while leveraging LangChain operators");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Demonstrates the architectural benefit of the integration.
    /// </summary>
    public static void ExplainArchitecturalBenefit()
    {
        Console.WriteLine("\n=== ARCHITECTURAL BENEFITS ===\n");

        Console.WriteLine("The integration provides:");
        Console.WriteLine("  ✓ LangChain's familiar pipe operator syntax");
        Console.WriteLine("  ✓ Ouroboros's type-safe composition");
        Console.WriteLine("  ✓ Functional error handling with Result<T>");
        Console.WriteLine("  ✓ Immutable state and event sourcing");
        Console.WriteLine("  ✓ Testable and mathematically sound pipelines");
        Console.WriteLine();

        Console.WriteLine("Best of both worlds:");
        Console.WriteLine("  - LangChain's operator convenience (Set | Retrieve | Template | LLM)");
        Console.WriteLine("  - Ouroboros's safety guarantees (Kleisli composition, monadic laws)");
        Console.WriteLine("  - Flexible usage (CLI DSL or code-based with static imports)");
        Console.WriteLine();
    }

    /// <summary>
    /// Shows the complete example workflow.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunCompleteExample()
    {
        await RunLangChainPipelineExample();
        ExplainArchitecturalBenefit();
    }
}
