# MeTTa Neuro-Symbolic Architecture Strategy

This document outlines the technical architecture for deepening the integration of MeTTa (Meta Type Talk) with the Ouroboros monadic pipeline.

## 1. Symbolic Guard Rails (Type-Safe Output)

**Concept:** Move beyond JSON schema validation. Use MeTTa's dependent types to enforce logical consistency. If the LLM generates a plan that violates a MeTTa type signature, it is rejected *before* runtime execution.

### A. MeTTa Code Patterns
Define a Type System for the Agent's actions. This acts as the "Physics" of the agent's world.

```scheme
; Define the Type Hierarchy for Actions
(: Action Type)
(: FileSystemAction (-> String Action))
(: NetworkAction (-> String Action))

; Define Safe Contexts (The Guard Rails)
(: SafeContext Type)
(: ReadOnly SafeContext)
(: FullAccess SafeContext)

; Define the Permission Logic
(: Allowed (-> Action SafeContext Bool))

; --- THE LOGIC ---
; Reading is allowed in ReadOnly context
(= (Allowed (FileSystemAction "read") ReadOnly) True)
; Writing is NOT allowed in ReadOnly context
(= (Allowed (FileSystemAction "write") ReadOnly) False)
; All actions allowed in FullAccess
(= (Allowed $action FullAccess) True)
```

### B. C# Integration Logic
Create a `MeTTaVerificationStep` in the pipeline.

```csharp
public class MeTTaVerificationStep : IPipelineStep<Plan, Plan>
{
    private readonly MeTTaEngine _engine;

    public async Task<Result<Plan>> ExecuteAsync(Plan plan)
    {
        // 1. Convert LLM Plan to MeTTa Expression
        // e.g., LLM says "Write to config.json" -> (FileSystemAction "write")
        string atom = plan.ToMeTTaAtom(); 
        
        // 2. Construct the Query
        // Check if this specific action is allowed in the current context (e.g., ReadOnly)
        string query = $"!(Allowed {atom} ReadOnly)";

        // 3. Execute Verification
        var result = await _engine.ExecuteQueryAsync(query);

        // 4. Monadic Check
        if (result.ToString() == "False")
        {
            return Result.Failure<Plan>(
                new SecurityException($"Guard Rail Violation: Action {atom} is forbidden in ReadOnly context.")
            );
        }

        return Result.Success(plan);
    }
}
```

### C. Benefit vs. Pure LLM
*   **Pure LLM:** You ask "Is this safe?" The LLM might say "Yes" because it hallucinates or misunderstands the nuance of "write access."
*   **Neuro-Symbolic:** The logic is deterministic. `(Allowed (FileSystemAction "write") ReadOnly)` evaluates to `False` 100% of the time.

## 2. The "Grounded" Knowledge Graph

**Concept:** Instead of just embedding text into vectors, we extract semantic triples and ground them in the AtomSpace. This allows for specific logical queries that vector search fails at (e.g., negation or specific relationships).

### A. MeTTa Code Patterns
Use the AtomSpace to store state.

```scheme
; Define Relations
(: Author (-> Atom Atom))
(: Status (-> Atom Atom))
(: Topic (-> Atom Atom))

; --- The Grounded Knowledge (Populated by LLM Extraction) ---
(Author (Doc "deployment.md") (User "PMeeske"))
(Status (Doc "deployment.md") (State "Outdated"))
(Topic (Doc "deployment.md") (Concept "Kubernetes"))

; --- The Logical Query ---
; "Find all outdated documents about Kubernetes"
; Vectors struggle here because "Outdated" is a state, not a semantic similarity.
!(match &self 
    (, 
      (Status $doc (State "Outdated"))
      (Topic $doc (Concept "Kubernetes"))
    ) 
    $doc
)
```

### B. C# Integration Logic
Create a `SymbolicIngestionStep`.

```csharp
// Inside your RAG Pipeline
public async Task<Result<Unit>> IngestDocument(Document doc)
{
    // 1. LLM Extraction Step: Ask LLM to output triples
    // Prompt: "Extract entities as: (Relation Subject Object)"
    var extraction = await _llm.ExtractTriples(doc.Content); 
    
    // 2. Add to AtomSpace
    foreach(var triple in extraction)
    {
        // Add: (Status (Doc "deployment.md") (State "Outdated"))
        await _mettaEngine.AddFactAsync(triple);
    }
    
    return Result.Success(Unit.Value);
}

// Retrieval Step
public async Task<string> SymbolicRetrieve(string topic, string status)
{
    // Hybrid retrieval: Use Logic first, then Vectors
    string query = $"!(match &self (, (Status $doc (State \"{status}\")) (Topic $doc (Concept \"{topic}\"))) $doc)";
    return await _mettaEngine.ExecuteQueryAsync(query);
}
```

### C. Benefit vs. Pure LLM
*   **Pure LLM:** A vector search for "Outdated Kubernetes docs" might return *updated* Kubernetes docs because "Outdated" and "Updated" are semantically close in vector space.
*   **Neuro-Symbolic:** It performs an exact graph match. It only returns documents explicitly linked to the `(State "Outdated")` atom.

## 3. Neuro-Symbolic Tool Discovery

**Concept:** Instead of the LLM guessing which tool to use, we define tool signatures (Input/Output types) in MeTTa. We use MeTTa's unification engine to find a "path" from our input data to our desired goal.

### A. MeTTa Code Patterns
This acts as a planner.

```scheme
; Define Types
(: Text Type)
(: Summary Type)
(: Code Type)
(: TestResult Type)

; Define Tool Capabilities (Signatures)
(: summarize_tool (-> Text Summary))
(: generate_code_tool (-> Summary Code))
(: run_tests_tool (-> Code TestResult))

; --- The Chaining Logic (Backward Chaining) ---
; "Can we get a TestResult from Text?"
; MeTTa recursively matches the output of one tool to the input of another.

(: solve (-> $start_type $end_type Expression))
(= (solve $in $out)
   (match &self 
      (: $tool (-> $in $out)) 
      $tool
   )
)
(= (solve $in $out)
   (match &self 
      (: $tool (-> $mid $out))
      (chain (solve $in $mid) $tool)
   )
)
```

### B. C# Integration Logic
Replace your imperative `Router` with a `MeTTaPlanner`.

```csharp
public async Task<Result<Pipeline>> PlanWorkflow(string goalType)
{
    // 1. We have "Text" (User Input), we want "TestResult"
    string query = "!(solve Text TestResult)";

    // 2. Execute MeTTa Reasoning
    // Result might look like: (chain (chain summarize_tool generate_code_tool) run_tests_tool)
    var planExpression = await _mettaEngine.ExecuteQueryAsync(query);

    // 3. Compile to C# Pipeline
    // Dynamic mapping of MeTTa atoms to C# ITool implementations
    var pipeline = _toolBinder.Bind(planExpression);

    return Result.Success(pipeline);
}
```

### C. Benefit vs. Pure LLM
*   **Pure LLM:** The LLM might try to pass `Text` directly into the `run_tests_tool`, causing a runtime crash because the tool expects `Code`.
*   **Neuro-Symbolic:** The plan is constructed based on Type Signatures. It is mathematically impossible for the planner to suggest a tool chain where inputs and outputs do not align. The chain is guaranteed to be valid by construction.
