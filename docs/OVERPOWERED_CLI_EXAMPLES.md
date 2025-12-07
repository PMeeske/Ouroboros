# Overpowered CLI Examples with DSL

This document provides advanced, "overpowered" examples of using the Ouroboros CLI DSL to perform complex tasks like deep research, iterative refinement, and massive parallel processing.

> **Note:** The output of these pipelines is printed to the console. You can redirect the output to a file using standard shell redirection (e.g., `dotnet run ... > output.txt`).


## 1. The "PhD Student" Pipeline (Deep Research & Synthesis)

This pipeline acts like a PhD student: it takes a complex topic, breaks it down into sub-questions, researches each one using a local document base, and synthesizes a comprehensive report.

**Scenario:** You have a folder of PDF/Text documents about "Quantum Computing" in `./docs/quantum`. You want a comprehensive answer to "What are the current challenges in error correction?"

**DSL:**
```bash
SetTopic('Quantum Error Correction')
SetQuery('What are the current challenges in error correction?')
SetSource('./docs/quantum')
UseDir('pattern=*.pdf')
UseDir('pattern=*.txt')
EmbedZip
DecomposeAndAggregateRAG('subs=5|per=8|stream|final=Synthesize a comprehensive academic report on the challenges of quantum error correction based on the sub-findings.')
```

**Explanation:**
1.  **SetTopic/SetQuery**: Defines the research goal.
2.  **SetSource/UseDir**: Ingests all relevant documents from the local folder.
3.  **EmbedZip**: Ensures all documents are embedded and ready for retrieval.
4.  **DecomposeAndAggregateRAG**:
    *   `subs=5`: Breaks the main question into 5 distinct sub-questions (e.g., "What is surface code?", "How does decoherence affect qubits?").
    *   `per=8`: Retrieves the top 8 most relevant document chunks for *each* sub-question.
    *   `stream`: Prints the sub-questions and their answers as they are generated.
    *   `final=...`: Uses a custom prompt to ensure the final output is an "academic report".

## 2. The "Perfectionist" Pipeline (Iterative Refinement)

This pipeline generates a draft and then ruthlessly critiques and improves it until it meets a high standard.

**Scenario:** You want to write a blog post about "The Future of AI" that is insightful and free of clichés.

**DSL:**
```bash
SetTopic('The Future of AI')
SetPrompt('Write a provocative blog post about the future of AI, focusing on human-AI symbiosis.')
LLM
UseRefinementLoop('iterations=3|critique=Identify clichés and weak arguments.|improve=Rewrite to be more original and punchy.')
```

**Explanation:**
1.  **SetTopic/SetPrompt**: Sets the creative goal.
2.  **LLM**: Generates the first draft (V1).
3.  **UseRefinementLoop**:
    *   `iterations=3`: Runs the loop 3 times.
    *   `critique=...`: Instructs the model to specifically look for clichés.
    *   `improve=...`: Instructs the model on how to fix the issues found in the critique.
    *   *Result*: V1 -> Critique -> V2 -> Critique -> V3 -> Critique -> Final Version.

## 3. The "MapReduce" Pipeline (Massive Context Summarization)

This pipeline handles a massive amount of text that wouldn't fit into a single context window by splitting it up, processing chunks in parallel, and then summarizing the summaries.

**Scenario:** You have a massive log file or a book in `./data/big_logs` and you want to find all security incidents.

**DSL:**
```bash
SetSource('./data/big_logs')
UseDir('pattern=*.log')
SetQuery('Identify all security incidents, unauthorized access attempts, and anomalies.')
DivideAndConquerRAG('k=100|group=10|stream|template=List all security incidents found in this log chunk:|final=Compile a master list of all security incidents from the partial lists below.')
```

**Explanation:**
1.  **SetSource/UseDir**: Ingests the massive logs.
2.  **DivideAndConquerRAG**:
    *   `k=100`: Retrieves the top 100 most relevant chunks (or use a higher number for more coverage).
    *   `group=10`: Processes 10 chunks at a time in a single prompt.
    *   `template=...`: Instructions for processing each group (finding incidents).
    *   `final=...`: Instructions for merging the partial lists into one master list.

## 4. The "Full Context" Pipeline (Custom RAG)

This pipeline gives you full control over how documents are retrieved and combined, useful for specific formatting needs.

**Scenario:** You need to answer a question using exactly 5 documents, formatted with specific headers, and appended to a custom prompt.

**DSL:**
```bash
SetTopic('Legal Precedents')
SetQuery('What are the precedents for fair use in software APIs?')
SetSource('./legal_docs')
UseDir('pattern=*.md')
RetrieveSimilarDocuments('amount=5')
CombineDocuments('sep=\n---\n|prefix=Here are the relevant legal cases:\n|suffix=\n\nEnd of cases.|appendPrompt')
SetPrompt('Based on the cases provided above, write a legal memo.')
LLM
```

**Explanation:**
1.  **RetrieveSimilarDocuments**: Explicitly retrieves 5 documents.
2.  **CombineDocuments**:
    *   `sep=\n---\n`: Separates docs with a markdown horizontal rule.
    *   `prefix/suffix`: Adds context wrappers around the injected documents.
    *   `appendPrompt`: Appends the combined text to the current prompt (or prepends if configured).
3.  **SetPrompt**: Sets the final instruction, which now has the context attached.
4.  **LLM**: Executes the generation.

## 5. The "Codebase Analyst" Pipeline (Solution Ingestion)

This pipeline ingests an entire .NET solution and analyzes it for architectural patterns.

**Scenario:** You want to understand the architecture of a large C# project.

**DSL:**
```bash
SetSource('./src')
UseSolution('maxFiles=500|ext=.cs')
SetQuery('Analyze the architectural patterns used in this solution. Identify any violations of Clean Architecture.')
DivideAndConquerRAG('k=50|group=5|stream|template=Analyze the architecture in these files:|final=Synthesize a complete architectural review.')
```

**Explanation:**
1.  **UseSolution**: Smartly ingests a .NET solution, respecting project structure and file limits.
2.  **DivideAndConquerRAG**: Processes the potentially large codebase in chunks to provide a comprehensive analysis.

## 6. The "Guided Setup" Pipeline (Dependency Management)

This pipeline checks for dependencies and guides the user if they are missing, ensuring the environment is ready for complex tasks.

**Scenario:** You want to ensure the user has Docker and Python installed before running a complex agent.

**DSL:**
```bash
InstallDependenciesGuided('dep=Docker')
InstallDependenciesGuided('dep=Python')
SetTopic('Environment Check')
SetPrompt('Confirm that the environment is ready.')
LLM
```

**Explanation:**
1.  **InstallDependenciesGuided**: Checks for the specified dependency. If missing, it triggers an event that can be handled by the system (e.g., pausing for user input or attempting auto-install).

