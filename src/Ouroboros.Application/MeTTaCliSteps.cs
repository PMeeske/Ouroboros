using System.Text;
using System.Text.RegularExpressions;
using Ouroboros.Tools.MeTTa;
using Ouroboros.Application;

namespace Ouroboros.Application;

public static class MeTTaCliSteps
{
    // ═══════════════════════════════════════════════════════════════════════════
    // METTA ATOM CREATION & MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates a MeTTa atom (symbol) and adds it to the knowledge base.
    /// Usage: MeTTaAtom('name') or MeTTaAtom('name', 'type') or MeTTaAtom('(concept my-idea)')
    /// </summary>
    [PipelineToken("MeTTaAtom", "Atom", "Symbol")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaAtom(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            var parsed = ParseAtomArgs(args);
            if (string.IsNullOrWhiteSpace(parsed.name))
            {
                Console.WriteLine("[metta] Atom name required. Usage: MeTTaAtom('name') or MeTTaAtom('name', 'type')");
                return s;
            }

            // Build the MeTTa expression
            string atomExpr;
            if (!string.IsNullOrWhiteSpace(parsed.atomType))
            {
                // Typed atom: (: name Type)
                atomExpr = $"(: {parsed.name} {parsed.atomType})";
            }
            else if (parsed.name.StartsWith("("))
            {
                // Raw expression passed directly
                atomExpr = parsed.name;
            }
            else
            {
                // Simple symbol
                atomExpr = $"(atom {parsed.name})";
            }

            var result = await s.MeTTaEngine.AddFactAsync(atomExpr);
            result.Match(
                _ =>
                {
                    s.Output = $"Atom created: {atomExpr}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:atom:create:{parsed.name}", new[] { atomExpr });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to create atom: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:atom:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Creates a MeTTa fact (relation between atoms).
    /// Usage: MeTTaFact('(relation subject object)') or MeTTaFact('capability', 'tool_search', 'information-retrieval')
    /// </summary>
    [PipelineToken("MeTTaFact", "Fact", "Assert")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaFact(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string fact = ParseString(args);
            if (string.IsNullOrWhiteSpace(fact))
            {
                Console.WriteLine("[metta] Fact expression required. Usage: MeTTaFact('(relation arg1 arg2)')");
                return s;
            }

            // If not wrapped in parens, try to build a tuple
            if (!fact.StartsWith("("))
            {
                var parts = fact.Split(',').Select(p => p.Trim().Trim('\'', '"')).ToArray();
                fact = parts.Length switch
                {
                    2 => $"({parts[0]} {parts[1]})",
                    3 => $"({parts[0]} {parts[1]} {parts[2]})",
                    _ => $"({string.Join(" ", parts)})"
                };
            }

            var result = await s.MeTTaEngine.AddFactAsync(fact);
            result.Match(
                _ =>
                {
                    s.Output = $"Fact asserted: {fact}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:fact:assert:{fact[..Math.Min(50, fact.Length)]}", new[] { fact });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to assert fact: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:fact:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Creates a MeTTa rule (implication).
    /// Usage: MeTTaRule('(= (ruleName $x) (body $x))')
    /// </summary>
    [PipelineToken("MeTTaRule", "Rule", "Implication")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaRule(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string rule = ParseString(args);
            if (string.IsNullOrWhiteSpace(rule))
            {
                Console.WriteLine("[metta] Rule expression required. Usage: MeTTaRule('(= (head $x) (body $x))')");
                return s;
            }

            var result = await s.MeTTaEngine.ApplyRuleAsync(rule);
            result.Match(
                success =>
                {
                    s.Output = $"Rule applied: {rule}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:rule:apply", new[] { rule, success });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to apply rule: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:rule:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Queries the MeTTa knowledge base.
    /// Usage: MeTTaQuery('!(match &amp;self (capability $tool $cap) ($tool $cap))')
    /// </summary>
    [PipelineToken("MeTTaQuery", "Query", "MeTTaAsk")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaQuery(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string query = ParseString(args);
            if (string.IsNullOrWhiteSpace(query))
            {
                // Use current context or output as query
                query = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Output;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("[metta] Query required. Usage: MeTTaQuery('!(expression)')");
                return s;
            }

            // Ensure query starts with ! for execution
            if (!query.StartsWith("!"))
            {
                query = $"!{query}";
            }

            var result = await s.MeTTaEngine.ExecuteQueryAsync(query);
            result.Match(
                success =>
                {
                    s.Output = success;
                    s.Branch = s.Branch.WithIngestEvent($"metta:query:result", new[] { query, success });
                    if (s.Trace) Console.WriteLine($"[metta] Query result: {success}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Query failed: {error}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:query:{error}", Array.Empty<string>());
                });

            return s;
        };

    /// <summary>
    /// Creates a concept from the current context/output and stores it as MeTTa atoms.
    /// Usage: MeTTaConcept('concept-name') - converts s.Output or s.Context to MeTTa atoms
    /// </summary>
    [PipelineToken("MeTTaConcept", "Concept", "Reify")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaConcept(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string conceptName = ParseString(args);
            if (string.IsNullOrWhiteSpace(conceptName))
            {
                conceptName = $"concept-{Guid.NewGuid().ToString()[..8]}";
            }

            string content = !string.IsNullOrWhiteSpace(s.Output) ? s.Output : s.Context;
            if (string.IsNullOrWhiteSpace(content))
            {
                Console.WriteLine("[metta] No content to conceptualize. Run a previous step first.");
                return s;
            }

            // Create main concept atom
            await s.MeTTaEngine.AddFactAsync($"(: {conceptName} Concept)");

            // Extract key terms and create relations
            var terms = ExtractKeyTerms(content);
            foreach (var term in terms.Take(10))
            {
                await s.MeTTaEngine.AddFactAsync($"(has-term {conceptName} {term})");
            }

            // Store content summary
            var summary = content.Length > 200 ? content[..200] + "..." : content;
            summary = summary.Replace("\"", "\\\"").Replace("\n", " ");
            await s.MeTTaEngine.AddFactAsync($"(content {conceptName} \"{summary}\")");

            // Add timestamp
            await s.MeTTaEngine.AddFactAsync($"(created-at {conceptName} \"{DateTime.UtcNow:O}\")");

            s.Output = $"Concept '{conceptName}' created with {terms.Count} terms";
            s.Branch = s.Branch.WithIngestEvent($"metta:concept:create:{conceptName}", terms.Take(5).ToArray());
            if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");

            return s;
        };

    /// <summary>
    /// Links two concepts or atoms together.
    /// Usage: MeTTaLink('source', 'target', 'relation') or MeTTaLink('concept1 -> concept2')
    /// </summary>
    [PipelineToken("MeTTaLink", "Link", "Connect")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaLink(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string linkSpec = ParseString(args);
            if (string.IsNullOrWhiteSpace(linkSpec))
            {
                Console.WriteLine("[metta] Link specification required. Usage: MeTTaLink('source', 'target', 'relation')");
                return s;
            }

            string source, target, relation;

            // Parse arrow notation: "concept1 -> concept2" or "concept1 -relation-> concept2"
            var arrowMatch = Regex.Match(linkSpec, @"(\S+)\s*-(\w*)-?>\s*(\S+)");
            if (arrowMatch.Success)
            {
                source = arrowMatch.Groups[1].Value;
                relation = string.IsNullOrWhiteSpace(arrowMatch.Groups[2].Value) ? "relates-to" : arrowMatch.Groups[2].Value;
                target = arrowMatch.Groups[3].Value;
            }
            else
            {
                // Parse comma notation: "source, target, relation"
                var parts = linkSpec.Split(',').Select(p => p.Trim().Trim('\'', '"')).ToArray();
                if (parts.Length < 2)
                {
                    Console.WriteLine("[metta] Need at least source and target. Usage: MeTTaLink('src', 'tgt', 'rel')");
                    return s;
                }
                source = parts[0];
                target = parts[1];
                relation = parts.Length > 2 ? parts[2] : "relates-to";
            }

            var linkFact = $"({relation} {source} {target})";
            var result = await s.MeTTaEngine.AddFactAsync(linkFact);
            result.Match(
                _ =>
                {
                    s.Output = $"Link created: {source} -{relation}-> {target}";
                    s.Branch = s.Branch.WithIngestEvent($"metta:link:create", new[] { source, relation, target });
                    if (s.Trace) Console.WriteLine($"[metta] ✓ {s.Output}");
                },
                error =>
                {
                    Console.WriteLine($"[metta] ✗ Failed to create link: {error}");
                });

            return s;
        };

    /// <summary>
    /// Generates MeTTa atoms from LLM analysis of content.
    /// Usage: MeTTaGenerate('topic') - asks LLM to generate relevant atoms
    /// </summary>
    [PipelineToken("MeTTaGenerate", "Generate", "Atomize")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaGenerate(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null || s.Llm == null)
            {
                Console.WriteLine("[metta] Requires both MeTTa engine and LLM");
                return s;
            }

            string topic = ParseString(args);
            if (string.IsNullOrWhiteSpace(topic))
            {
                topic = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Output;
            }

            if (string.IsNullOrWhiteSpace(topic))
            {
                Console.WriteLine("[metta] Topic required. Usage: MeTTaGenerate('topic')");
                return s;
            }

            // Ask LLM to generate MeTTa atoms
            var prompt = $@"Generate MeTTa atoms for the topic: {topic}

Output ONLY valid MeTTa expressions, one per line. Use these patterns:
- Type declaration: (: name Type)
- Fact/relation: (relation subject object)
- Rule: (= (head $x) (body $x))
- Property: (has-property entity property value)

Example output for 'web search tool':
(: web-search Tool)
(capability web-search information-retrieval)
(has-property web-search speed fast)
(= (use-for-query $q) (if (contains $q search) web-search None))

Generate 5-10 relevant atoms:";

            var response = await s.Llm.InnerModel.GenerateTextAsync(prompt);

            // Parse and add each atom
            var lines = response.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.StartsWith("(") && l.EndsWith(")"))
                .ToList();

            int added = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith("(="))
                {
                    await s.MeTTaEngine.ApplyRuleAsync(line);
                }
                else
                {
                    await s.MeTTaEngine.AddFactAsync(line);
                }
                added++;
            }

            s.Output = $"Generated {added} MeTTa atoms for '{topic}':\n{string.Join("\n", lines)}";
            s.Branch = s.Branch.WithIngestEvent($"metta:generate:{topic[..Math.Min(20, topic.Length)]}", lines.Take(5).ToArray());
            if (s.Trace) Console.WriteLine($"[metta] ✓ Generated {added} atoms");

            return s;
        };

    /// <summary>
    /// Introspects the MeTTa knowledge base and shows statistics.
    /// Usage: MeTTaIntrospect or MeTTaIntrospect('types') or MeTTaIntrospect('facts')
    /// </summary>
    [PipelineToken("MeTTaIntrospect", "Introspect", "KBStatus")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaIntrospect(string? args = null)
        => async s =>
        {
            await EnsureMeTTaEngineAsync(s);
            if (s.MeTTaEngine == null) return s;

            string filter = ParseString(args).ToLowerInvariant();

            var sb = new StringBuilder();
            sb.AppendLine("=== MeTTa Knowledge Base ===");

            // Query for types
            if (string.IsNullOrWhiteSpace(filter) || filter == "types")
            {
                var typesResult = await s.MeTTaEngine.ExecuteQueryAsync("!(match &self (: $x $type) ($x $type))");
                typesResult.Match(
                    types => sb.AppendLine($"\nTypes:\n{types}"),
                    _ => sb.AppendLine("\nTypes: (none)"));
            }

            // Query for facts/relations
            if (string.IsNullOrWhiteSpace(filter) || filter == "facts" || filter == "relations")
            {
                var factsResult = await s.MeTTaEngine.ExecuteQueryAsync("!(match &self ($rel $a $b) ($rel $a $b))");
                factsResult.Match(
                    facts => sb.AppendLine($"\nFacts:\n{facts}"),
                    _ => sb.AppendLine("\nFacts: (none)"));
            }

            // Query for concepts
            if (string.IsNullOrWhiteSpace(filter) || filter == "concepts")
            {
                var conceptsResult = await s.MeTTaEngine.ExecuteQueryAsync("!(match &self (: $x Concept) $x)");
                conceptsResult.Match(
                    concepts => sb.AppendLine($"\nConcepts:\n{concepts}"),
                    _ => sb.AppendLine("\nConcepts: (none)"));
            }

            sb.AppendLine("============================");

            s.Output = sb.ToString();
            if (s.Trace) Console.WriteLine(s.Output);

            return s;
        };

    /// <summary>
    /// Resets the MeTTa knowledge base.
    /// Usage: MeTTaReset
    /// </summary>
    [PipelineToken("MeTTaReset", "Reset", "ClearKB")]
    public static Step<CliPipelineState, CliPipelineState> MeTTaReset(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null) return s;

            var result = await s.MeTTaEngine.ResetAsync();
            result.Match(
                _ =>
                {
                    s.Output = "MeTTa knowledge base reset";
                    s.Branch = s.Branch.WithIngestEvent("metta:reset", Array.Empty<string>());
                    if (s.Trace) Console.WriteLine("[metta] ✓ Knowledge base reset");
                },
                error => Console.WriteLine($"[metta] ✗ Reset failed: {error}"));

            return s;
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // EXISTING MOTTO/METTA STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    [PipelineToken("MottoInit", "InitMotto")]
    public static Step<CliPipelineState, CliPipelineState> MottoInit(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null)
            {
                s.MeTTaEngine = new SubprocessMeTTaEngine();
            }

            var initStep = new MottoSteps.MottoInitializeStep(s.MeTTaEngine);
            var result = await initStep.ExecuteAsync(Unit.Value);

            result.Match(
                success =>
                {
                    if (s.Trace) Console.WriteLine("[metta] Motto initialized");
                },
                failure =>
                {
                    Console.WriteLine($"[metta] Failed to initialize Motto: {failure}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:init:{failure}", Array.Empty<string>());
                }
            );

            return s;
        };

    [PipelineToken("MottoChat", "MeTTaChat")]
    public static Step<CliPipelineState, CliPipelineState> MottoChat(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null)
            {
                Console.WriteLine("[metta] Engine not initialized. Call MottoInit first.");
                return s;
            }

            // Use args as message, or fallback to s.Query/s.Prompt
            string message = ParseString(args);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : s.Prompt;
            }

            if (string.IsNullOrWhiteSpace(message)) return s;

            var chatStep = new MottoSteps.MottoChatStep(s.MeTTaEngine);
            var result = await chatStep.ExecuteAsync(message);

            result.Match(
                success =>
                {
                    s.Output = success;
                    if (s.Trace) Console.WriteLine($"[metta] Chat response: {success}");
                    // Record as reasoning?
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(success), message, new List<ToolExecution>());
                },
                failure =>
                {
                    Console.WriteLine($"[metta] Chat failed: {failure}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:chat:{failure}", Array.Empty<string>());
                }
            );

            return s;
        };

    [PipelineToken("MottoOllama", "OllamaAgent")]
    public static Step<CliPipelineState, CliPipelineState> MottoOllama(string? args = null)
        => async s =>
        {
            if (s.MeTTaEngine == null)
            {
                s.MeTTaEngine = new SubprocessMeTTaEngine();
                await new MottoSteps.MottoInitializeStep(s.MeTTaEngine).ExecuteAsync(Unit.Value);
            }

            string model = "deepseek-v3.1:671b-cloud";
            string message = ParseString(args);
            string? script = null;

            // Parse args: "model=phi3|msg=Hello|script=ollama_agent.msa"
            // Also handle single: "msg=Hello" or "Hello"
            if (message.Contains("|"))
            {
                foreach (var part in message.Split('|'))
                {
                    if (part.StartsWith("model=")) model = part.Substring(6);
                    else if (part.StartsWith("msg=")) message = part.Substring(4);
                    else if (part.StartsWith("script=")) script = part.Substring(7);
                }
            }
            else if (message.StartsWith("msg="))
            {
                message = message.Substring(4);
            }
            else if (message.StartsWith("model="))
            {
                model = message.Substring(6);
                message = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(message)) message = s.Query;

            // If we have context from previous pipeline steps (like UseDir), include it
            if (!string.IsNullOrWhiteSpace(s.Context))
            {
                message = $"Context:\n{s.Context}\n\nQuestion: {message}";
            }

            // The MeTTa REPL already imports motto and ollama_agent on startup

            string query;
            if (!string.IsNullOrEmpty(script))
            {
                // Use the script runner pattern
                // !((ollama-agent "model") (Script "script.msa") (user "msg"))
                query = $"!((ollama-agent \"{model}\") (Script \"{script}\") (user \"{message.Replace("\"", "\\\"")}\"))";
                if (s.Trace) Console.WriteLine($"[metta] Calling Ollama ({model}) with script {script}: {message}");
            }
            else
            {
                // Direct call
                // !((ollama-agent "model") (user "msg"))
                query = $"!((ollama-agent \"{model}\") (user \"{message.Replace("\"", "\\\"")}\"))";
                if (s.Trace) Console.WriteLine($"[metta] Calling Ollama ({model}): {message}");
            }

            var result = await s.MeTTaEngine.ExecuteQueryAsync(query);

            result.Match(
                success =>
                {
                    s.Output = success;
                    if (s.Trace) Console.WriteLine($"[metta] Ollama response: {success}");
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(success), message, new List<ToolExecution>());
                },
                failure =>
                {
                    Console.WriteLine($"[metta] Ollama call failed: {failure}");
                    s.Branch = s.Branch.WithIngestEvent($"metta:error:ollama:{failure}", Array.Empty<string>());
                }
            );

            return s;
        };

    private static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        // Simple quote stripping if needed, similar to CliSteps.ParseString
        if (arg.StartsWith("'") && arg.EndsWith("'") && arg.Length >= 2) return arg[1..^1];
        if (arg.StartsWith("\"") && arg.EndsWith("\"") && arg.Length >= 2) return arg[1..^1];
        return arg;
    }

    /// <summary>
    /// Read a file and set its content as the pipeline context.
    /// Usage: ReadFile('path/to/file.cs')
    /// </summary>
    [PipelineToken("ReadFile", "LoadFile")]
    public static Step<CliPipelineState, CliPipelineState> ReadFile(string? args = null)
        => s =>
        {
            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path))
            {
                if (s.Trace) Console.WriteLine("[file] No path provided");
                return Task.FromResult(s);
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[file] File not found: {fullPath}");
                    return Task.FromResult(s);
                }

                string content = File.ReadAllText(fullPath);
                string fileName = Path.GetFileName(fullPath);

                // Set as context with file info header
                s.Context = $"=== File: {fileName} ===\n{content}";

                if (s.Trace) Console.WriteLine($"[file] Loaded {fileName} ({content.Length} chars)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[file] Error reading file: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Applies self-critique to MeTTa-based reasoning output.
    /// Works with MottoChat or MottoOllama outputs by wrapping them in critique cycles.
    /// Usage: MottoOllama('msg=...') | MottoSelfCritique or MottoSelfCritique('2')
    /// </summary>
    [PipelineToken("MottoSelfCritique", "MeTTaCritique")]
    public static Step<CliPipelineState, CliPipelineState> MottoSelfCritique(string? args = null)
        => async s =>
        {
            // Parse iteration count from args, default to 1
            int iterations = 1;
            if (!string.IsNullOrWhiteSpace(args))
            {
                string parsed = ParseString(args);
                if (int.TryParse(parsed, out int value) && value > 0)
                {
                    iterations = Math.Min(value, 5); // Cap at 5
                }
            }

            // Use the current output or context as the initial draft
            string initialContent = !string.IsNullOrWhiteSpace(s.Output) ? s.Output : s.Context;

            if (string.IsNullOrWhiteSpace(initialContent))
            {
                Console.WriteLine("[metta-critique] No content to critique. Run MottoChat or MottoOllama first.");
                return s;
            }

            // Create a draft state from the MeTTa output
            s.Branch = s.Branch.WithReasoning(new Draft(initialContent), "MeTTa initial output", new List<ToolExecution>());

            // Now apply self-critique using the standard agent
            LangChainPipeline.Agent.SelfCritiqueAgent agent = new(s.Llm, s.Tools, s.Embed);

            // We need to extract topic and query
            string topic = !string.IsNullOrWhiteSpace(s.Topic) ? s.Topic : "MeTTa reasoning output";
            string query = !string.IsNullOrWhiteSpace(s.Query) ? s.Query : topic;

            Result<LangChainPipeline.Agent.SelfCritiqueResult, string> result =
                await agent.GenerateWithCritiqueAsync(s.Branch, topic, query, iterations, s.RetrievalK);

            if (result.IsSuccess)
            {
                LangChainPipeline.Agent.SelfCritiqueResult critiqueResult = result.Value;
                s.Branch = critiqueResult.Branch;

                // Format output to show the critique process
                StringBuilder output = new();
                output.AppendLine("\n=== MeTTa Self-Critique Result ===");
                output.AppendLine($"Iterations: {critiqueResult.IterationsPerformed}");
                output.AppendLine($"Confidence: {critiqueResult.Confidence}");
                output.AppendLine("\n--- Original MeTTa Output ---");
                output.AppendLine(critiqueResult.Draft);
                output.AppendLine("\n--- Critique ---");
                output.AppendLine(critiqueResult.Critique);
                output.AppendLine("\n--- Improved Response ---");
                output.AppendLine(critiqueResult.ImprovedResponse);
                output.AppendLine("\n=========================");

                s.Output = output.ToString();
                s.Context = critiqueResult.ImprovedResponse;

                if (s.Trace)
                {
                    Console.WriteLine($"[metta-critique] Self-critique completed with {critiqueResult.IterationsPerformed} iteration(s), confidence: {critiqueResult.Confidence}");
                }
            }
            else
            {
                Console.WriteLine($"[metta-critique] Failed: {result.Error}");
                s.Branch = s.Branch.WithIngestEvent($"metta-critique:error:{result.Error.Replace('|', ':')}", Array.Empty<string>());
            }

            return s;
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════════════════

    private static async Task EnsureMeTTaEngineAsync(CliPipelineState s)
    {
        if (s.MeTTaEngine == null)
        {
            s.MeTTaEngine = new SubprocessMeTTaEngine();
            await new MottoSteps.MottoInitializeStep(s.MeTTaEngine).ExecuteAsync(Unit.Value);
        }
    }

    private static (string name, string? atomType) ParseAtomArgs(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
            return (string.Empty, null);

        var parsed = ParseString(args);

        // Check for comma-separated: "name, Type"
        if (parsed.Contains(','))
        {
            var parts = parsed.Split(',').Select(p => p.Trim().Trim('\'', '"')).ToArray();
            return (parts[0], parts.Length > 1 ? parts[1] : null);
        }

        // Check for space-separated with type: "name Type"
        var spaceParts = parsed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (spaceParts.Length >= 2 && !parsed.StartsWith("("))
        {
            return (spaceParts[0], spaceParts[1]);
        }

        return (parsed, null);
    }

    private static List<string> ExtractKeyTerms(string content)
    {
        // Extract potential key terms from content
        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract capitalized words (potential concepts)
        var capitalizedMatches = Regex.Matches(content, @"\b[A-Z][a-z]+(?:[A-Z][a-z]+)*\b");
        foreach (Match m in capitalizedMatches)
        {
            var term = m.Value.ToLowerInvariant().Replace(" ", "-");
            if (term.Length > 2) terms.Add(term);
        }

        // Extract quoted terms
        var quotedMatches = Regex.Matches(content, @"""([^""]+)""");
        foreach (Match m in quotedMatches)
        {
            var term = m.Groups[1].Value.ToLowerInvariant().Replace(" ", "-");
            if (term.Length > 2 && term.Length < 30) terms.Add(term);
        }

        // Extract hashtag-like terms
        var hashMatches = Regex.Matches(content, @"#(\w+)");
        foreach (Match m in hashMatches)
        {
            terms.Add(m.Groups[1].Value.ToLowerInvariant());
        }

        return terms.Take(20).ToList();
    }
}
