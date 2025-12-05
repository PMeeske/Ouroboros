#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reactive.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LangChain.Databases; // for Vector, IVectorCollection
using LangChain.DocumentLoaders;
using LangChain.Providers; // for IChatModel
// for TrackedVectorStore
using LangChainPipeline.Pipeline.Ingestion.Zip;
using Ouroboros.Application.Configuration;

namespace Ouroboros.Application;

/// <summary>
/// Discoverable CLI pipeline steps. Each method is annotated with PipelineToken and returns a Step over CliPipelineState.
/// Parsing of simple args is supported via optional string? args parameter.
/// </summary>
public static class CliSteps
{
    public static (string topic, string query) Normalize(CliPipelineState s)
    {
        string topic = string.IsNullOrWhiteSpace(s.Topic) ? (string.IsNullOrWhiteSpace(s.Prompt) ? "topic" : s.Prompt) : s.Topic;
        string query = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? topic : s.Prompt) : s.Query;
        return (topic, query);
    }

















    [PipelineToken("UseAsp", "UseControllers")]
    public static Step<CliPipelineState, CliPipelineState> UseNoopAsp(string? args = null)
        => s =>
        {
            s.Branch = s.Branch.WithIngestEvent("asp:no-op", Array.Empty<string>());
            return Task.FromResult(s);
        };

    [PipelineToken("Set", "SetPrompt", "Step<string,string>")]
    public static Step<CliPipelineState, CliPipelineState> SetPrompt(string? args = null)
        => s =>
        {
            s.Prompt = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetTopic")]
    public static Step<CliPipelineState, CliPipelineState> SetTopic(string? args = null)
        => s =>
        {
            s.Topic = ParseString(args);
            return Task.FromResult(s);
        };

    [PipelineToken("SetQuery")]
    public static Step<CliPipelineState, CliPipelineState> SetQuery(string? args = null)
        => s =>
        {
            s.Query = ParseString(args);
            return Task.FromResult(s);
        };

    // Compatibility forwards for steps defined in specialized classes
    [PipelineToken("UseDir", "DirIngest")]
    public static Step<CliPipelineState, CliPipelineState> UseDir(string? args = null)
        => IngestionCliSteps.UseDir(args);

    [PipelineToken("UseRefinementLoop")]
    public static Step<CliPipelineState, CliPipelineState> UseRefinementLoop(string? args = null)
        => ReasoningCliSteps.UseRefinementLoop(args);

    [PipelineToken("SetSource", "UseSource", "Source")]
    public static Step<CliPipelineState, CliPipelineState> SetSource(string? args = null)
        => s =>
        {
            string path = ParseString(args);
            if (string.IsNullOrWhiteSpace(path)) return Task.FromResult(s);
            // Expand ~ and relative paths
            string expanded = path.StartsWith("~")
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path.TrimStart('~', '/', '\\'))
                : path;
            string full = Path.GetFullPath(expanded);
            string finalPath = full;
            bool accessible = false;
            try
            {
                if (!Directory.Exists(full)) Directory.CreateDirectory(full);
                string testFile = Path.Combine(full, ".__pipeline_access_test");
                using (File.Create(testFile)) { }
                File.Delete(testFile);
                accessible = true;
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"source:error:{ex.GetType().Name}:{full}", Array.Empty<string>());
            }
            if (!accessible)
            {
                string fallback = Path.Combine(Environment.CurrentDirectory, "pipeline_source_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                try
                {
                    Directory.CreateDirectory(fallback);
                    finalPath = fallback;
                }
                catch (Exception ex2)
                {
                    s.Branch = s.Branch.WithIngestEvent($"source:fallback-error:{ex2.GetType().Name}:{fallback}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithSource(DataSource.FromPath(finalPath));
            s.Branch = s.Branch.WithIngestEvent($"source:set:{finalPath}", Array.Empty<string>());
            return Task.FromResult(s);
        };

    [PipelineToken("SetK", "UseK", "K")]
    public static Step<CliPipelineState, CliPipelineState> SetK(string? args = null)
        => s =>
        {
            if (!string.IsNullOrWhiteSpace(args))
            {
                Match m = Regex.Match(args, @"\s*(\d+)\s*");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int k))
                {
                    s.RetrievalK = k;
                    if (s.Trace) Console.WriteLine($"[trace] RetrievalK set to {k}");
                }
            }
            return Task.FromResult(s);
        };

    [PipelineToken("TraceOn")]
    public static Step<CliPipelineState, CliPipelineState> TraceOn(string? args = null)
        => s =>
        {
            s.Trace = true;
            Console.WriteLine("[trace] tracing enabled");
            return Task.FromResult(s);
        };

    [PipelineToken("TraceOff")]
    public static Step<CliPipelineState, CliPipelineState> TraceOff(string? args = null)
        => s =>
        {
            s.Trace = false;
            Console.WriteLine("[trace] tracing disabled");
            return Task.FromResult(s);
        };





    [PipelineToken("ListVectors", "Vectors")] // Optional arg 'ids' to print IDs
    public static Step<CliPipelineState, CliPipelineState> ListVectors(string? args = null)
        => s =>
        {
            IEnumerable<Vector> all = s.Branch.Store switch
            {
                LangChainPipeline.Domain.Vectors.TrackedVectorStore tvs => tvs.GetAll(),
                _ => Enumerable.Empty<LangChain.Databases.Vector>()
            };
            int count = all.Count();
            Console.WriteLine($"[vectors] count={count}");
            if (!string.IsNullOrWhiteSpace(args) && args.Contains("ids", StringComparison.OrdinalIgnoreCase))
            {
                foreach (Vector? v in all.Take(100)) Console.WriteLine($" - {v.Id}");
                if (count > 100) Console.WriteLine($" ... (truncated) ...");
            }
            return Task.FromResult(s);
        };

    [PipelineToken("EmbedZip", "ZipEmbed")] // Re-embed docs that were skipped with noEmbed
    public static Step<CliPipelineState, CliPipelineState> EmbedZip(string? args = null)
        => async s =>
        {
            int batchSize = 16;
            if (!string.IsNullOrWhiteSpace(args) && args.StartsWith("batch=", StringComparison.OrdinalIgnoreCase) && int.TryParse(args.AsSpan(6), out int bs) && bs > 0)
                batchSize = bs;
            // Heuristic: any events zip:no-embed OR zipstream:no-embed; we can't recover original text fully unless stored; for now embed placeholders.
            List<string> pendingIds = s.Branch.Events
                .Where(e => e is IngestBatch ib && (ib.Source.StartsWith("zip:no-embed") || ib.Source.StartsWith("zipstream:no-embed")))
                .SelectMany(e => ((IngestBatch)e).Ids)
                .Distinct()
                .ToList();
            if (pendingIds.Count == 0)
            {
                Console.WriteLine("[embedzip] no deferred documents found");
                return s;
            }
            Console.WriteLine($"[embedzip] embedding {pendingIds.Count} placeholder docs");
            for (int i = 0; i < pendingIds.Count; i += batchSize)
            {
                List<string> batch = pendingIds.Skip(i).Take(batchSize).ToList();
                string[] texts = batch.Select(id =>
                {
                    if (DeferredZipTextCache.TryTake(id, out string? original) && !string.IsNullOrWhiteSpace(original)) return original;
                    return $"[DEFERRED ZIP DOC] {id}";
                }).ToArray();
                try
                {
                    IReadOnlyList<float[]> emb = await s.Embed.CreateEmbeddingsAsync(texts);
                    List<Vector> vectors = new List<Vector>();
                    for (int idx = 0; idx < emb.Count; idx++)
                    {
                        string id = batch[idx];
                        vectors.Add(new Vector { Id = id, Text = texts[idx], Embedding = emb[idx] });
                    }
                    await s.Branch.Store.AddAsync(vectors);
                }
                catch (Exception ex)
                {
                    foreach (string? id in batch)
                        s.Branch = s.Branch.WithIngestEvent($"zipembed:error:{id}:{ex.GetType().Name}", Array.Empty<string>());
                }
            }
            s.Branch = s.Branch.WithIngestEvent("zipembed:complete", pendingIds);
            return s;
        };

    public static string ParseString(string? arg)
    {
        arg ??= string.Empty;
        Match m = Regex.Match(arg, @"^'(?<s>.*)'$", RegexOptions.Singleline);
        if (m.Success) return m.Groups["s"].Value;
        m = Regex.Match(arg, @"^""(?<s>.*)""$", RegexOptions.Singleline);
        if (m.Success) return m.Groups["s"].Value;
        return arg;
    }

    // New chain-style tokens -------------------------------------------------

    [PipelineToken("RetrieveSimilarDocuments", "RetrieveDocs", "Retrieve")]
    public static Step<CliPipelineState, CliPipelineState> RetrieveSimilarDocuments(string? args = null)
        => async s =>
        {
            int amount = s.RetrievalK;
            string? overrideQuery = null;
            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("amount=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(7), out int a) && a > 0)
                        amount = a;
                    else if (part.StartsWith("query=", StringComparison.OrdinalIgnoreCase))
                        overrideQuery = part.Substring(6);
                }
            }
            string query = overrideQuery ?? (string.IsNullOrWhiteSpace(s.Query) ? s.Prompt : s.Query);
            if (string.IsNullOrWhiteSpace(query)) return s;
            try
            {
                if (s.Branch.Store is TrackedVectorStore tvs)
                {
                    IReadOnlyCollection<Document> hits = await tvs.GetSimilarDocuments(s.Embed, query, amount);
                    s.Retrieved.Clear();
                    s.Retrieved.AddRange(hits.Select(h => h.PageContent));
                    s.Branch = s.Branch.WithIngestEvent($"retrieve:{amount}:{query.Replace('|', ':').Replace('\n', ' ')}", Enumerable.Range(0, s.Retrieved.Count).Select(i => $"doc:{i}"));
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"retrieve:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    [PipelineToken("CombineDocuments", "CombineDocs")]
    public static Step<CliPipelineState, CliPipelineState> CombineDocuments(string? args = null)
        => s =>
        {
            string raw = ParseString(args);
            string separator = DefaultIngestionSettings.DocumentSeparator;
            string prefix = string.Empty;
            string suffix = string.Empty;
            int take = s.Retrieved.Count;
            bool appendToPrompt = false;
            bool clearRetrieved = false;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase))
                    {
                        string value = part.Substring(4);
                        separator = value.Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("take=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out int t) && t > 0)
                    {
                        take = Math.Min(t, s.Retrieved.Count);
                    }
                    else if (part.StartsWith("prefix=", StringComparison.OrdinalIgnoreCase))
                    {
                        prefix = part.Substring(7).Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.StartsWith("suffix=", StringComparison.OrdinalIgnoreCase))
                    {
                        suffix = part.Substring(7).Replace("\\n", "\n").Replace("\\t", "\t");
                    }
                    else if (part.Equals("append", StringComparison.OrdinalIgnoreCase) || part.Equals("appendPrompt", StringComparison.OrdinalIgnoreCase))
                    {
                        appendToPrompt = true;
                    }
                    else if (part.Equals("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        clearRetrieved = true;
                    }
                }
            }

            if (take <= 0 || s.Retrieved.Count == 0)
                return Task.FromResult(s);

            List<string> blocks = s.Retrieved.Take(take).Where(static r => !string.IsNullOrWhiteSpace(r)).ToList();
            if (blocks.Count == 0)
                return Task.FromResult(s);

            string combined = string.Join(separator, blocks);
            if (!string.IsNullOrEmpty(prefix))
                combined = prefix + combined;
            if (!string.IsNullOrEmpty(suffix))
                combined += suffix;

            s.Context = combined;
            if (appendToPrompt)
            {
                s.Prompt = string.IsNullOrWhiteSpace(s.Prompt)
                    ? combined
                    : combined + "\n\n" + s.Prompt;
            }

            if (clearRetrieved)
            {
                s.Retrieved.Clear();
            }

            return Task.FromResult(s);
        };

    [PipelineToken("Template", "UseTemplate")]
    public static Step<CliPipelineState, CliPipelineState> TemplateStep(string? args = null)
        => s =>
        {
            string templateRaw = ParseString(args);
            if (string.IsNullOrWhiteSpace(templateRaw)) return Task.FromResult(s);
            PromptTemplate pt = new PromptTemplate(templateRaw);
            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            string formatted = pt.Format(new() { ["context"] = s.Context, ["question"] = question, ["prompt"] = s.Prompt, ["topic"] = s.Topic });
            s.Prompt = formatted; // prepared for LLM
            return Task.FromResult(s);
        };

    [PipelineToken("LLM", "RunLLM")]
    public static Step<CliPipelineState, CliPipelineState> LlmStep(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(s.Prompt)) return s;
            try
            {
                (string text, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(s.Prompt);
                s.Output = text;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(text), s.Prompt, toolCalls);
                if (s.Trace) Console.WriteLine("[trace] LLM output length=" + text.Length);
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"llm:error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    /// <summary>
    /// Divide-and-Conquer RAG: retrieve K docs, split into groups, answer per group, then synthesize final.
    /// Args: 'k=24|group=6|template=...|final=...|sep=\\n---\\n'
    /// If s.Retrieved is empty, it will retrieve using current Query/Prompt.
    /// </summary>
    [PipelineToken("DivideAndConquerRAG", "DCRAG", "RAGMapReduce")]
    public static Step<CliPipelineState, CliPipelineState> DivideAndConquerRag(string? args = null)
        => async s =>
        {
            int k = Math.Max(4, s.RetrievalK);
            int group = RagDefaults.GroupSize;
            string sep = DefaultIngestionSettings.DocumentSeparator;
            string? template = null;
            string? finalTemplate = null;
            bool streamPartials = false; // print intermediate outputs to console

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kv) && kv > 0) k = kv;
                    else if (part.StartsWith("group=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(6), out int gv) && gv > 0) group = gv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) template = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTemplate = part.Substring(6);
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) streamPartials = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = part.Substring(7);
                        streamPartials = v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            if (string.IsNullOrWhiteSpace(question)) return s;

            // Ensure retrieved context
            if (s.Retrieved.Count == 0)
            {
                try { s = await RetrieveSimilarDocuments($"amount={k}")(s); } catch { /* ignore */ }
            }
            if (s.Retrieved.Count == 0) return s;

            // Defaults
            template ??= "Use the following context to answer the question. Be precise and concise.\n{context}\n\nQuestion: {question}\nAnswer:";
            finalTemplate ??= "You are to synthesize a final, precise answer from multiple partial answers.\nQuestion: {question}\n\nPartial Answers:\n{partials}\n\nFinal Answer:";

            // Partition into groups
            List<string> docs = s.Retrieved.Where(static r => !string.IsNullOrWhiteSpace(r)).Take(k).ToList();
            if (docs.Count == 0) return s;

            List<List<string>> groups = new List<List<string>>();
            for (int i = 0; i < docs.Count; i += group)
            {
                groups.Add(docs.Skip(i).Take(group).ToList());
            }

            List<string> partials = new List<string>(groups.Count);
            for (int gi = 0; gi < groups.Count; gi++)
            {
                List<string> g = groups[gi];
                string ctx = string.Join(sep, g);
                string prompt = template!
                    .Replace("{context}", ctx)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    (string answer, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(prompt);
                    partials.Add(answer ?? string.Empty);
                    // Record as reasoning step for traceability
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(answer ?? string.Empty), prompt, toolCalls);
                    if (streamPartials || s.Trace)
                    {
                        Console.WriteLine($"\n>> [dcrag] partial {gi + 1}/{groups.Count} (docs={g.Count})");
                        if (!string.IsNullOrWhiteSpace(answer))
                        {
                            Console.WriteLine(answer);
                        }
                        Console.Out.Flush();
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"dcrag:part-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }

            // Synthesize final
            string partialText = string.Join("\n\n---\n\n", partials.Where(p => !string.IsNullOrWhiteSpace(p)));
            string finalPrompt = finalTemplate!
                .Replace("{partials}", partialText)
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                (string finalAnswer, List<ToolExecution> finalToolCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
                s.Output = finalAnswer ?? string.Empty;
                s.Prompt = finalPrompt;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), finalPrompt, finalToolCalls);
                if (s.Trace) Console.WriteLine($"[trace] DCRAG final length={s.Output.Length}");
                if (streamPartials)
                {
                    Console.WriteLine("\n=== DCRAG FINAL ===");
                    Console.WriteLine(s.Output);
                    Console.Out.Flush();
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"dcrag:final-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }
            return s;
        };

    /// <summary>
    /// Decompose-and-Aggregate RAG: decomposes the main question into sub-questions, answers each with retrieved context,
    /// then synthesizes a final unified answer. Supports streaming of sub-answers and final aggregation.
    /// Args: 'subs=4|per=6|k=24|sep=\n---\n|stream|decompose=...|template=...|final=...'
    /// - subs: number of subquestions to generate (default 4)
    /// - per: number of retrieved docs per subquestion (default 6)
    /// - k: optional initial retrieval to warm cache or pre-fill (ignored if not needed)
    /// - sep: separator for combining docs
    /// - stream: print each sub-answer and the final result
    /// - decompose: custom prompt template for subquestion generation; placeholders: {question}
    /// - template: custom prompt for answering subquestions; placeholders: {context}, {subquestion}, {question}, {prompt}, {topic}
    /// - final: custom prompt for the final synthesis; placeholders: {pairs}, {question}, {prompt}, {topic}
    /// </summary>
    [PipelineToken("DecomposeAndAggregateRAG", "DARAG", "SubQAggregate")]
    public static Step<CliPipelineState, CliPipelineState> DecomposeAndAggregateRag(string? args = null)
        => async s =>
        {
            // Defaults and args
            int subs = RagDefaults.SubQuestions;
            int per = RagDefaults.DocumentsPerSubQuestion;
            int k = Math.Max(4, s.RetrievalK);
            string sep = DefaultIngestionSettings.DocumentSeparator;
            bool stream = false;
            string? decomposeTpl = null;
            string? subTpl = null;
            string? finalTpl = null;

            string raw = ParseString(args);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("subs=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(5), out int sv) && sv > 0) subs = sv;
                    else if (part.StartsWith("per=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(4), out int pv) && pv > 0) per = pv;
                    else if (part.StartsWith("k=", StringComparison.OrdinalIgnoreCase) && int.TryParse(part.AsSpan(2), out int kv) && kv > 0) k = kv;
                    else if (part.StartsWith("sep=", StringComparison.OrdinalIgnoreCase)) sep = part.Substring(4).Replace("\\n", "\n");
                    else if (part.Equals("stream", StringComparison.OrdinalIgnoreCase)) stream = true;
                    else if (part.StartsWith("stream=", StringComparison.OrdinalIgnoreCase))
                    {
                        string v = part.Substring(7);
                        stream = v.Equals("1") || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase) || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (part.StartsWith("decompose=", StringComparison.OrdinalIgnoreCase)) decomposeTpl = part.Substring(10);
                    else if (part.StartsWith("template=", StringComparison.OrdinalIgnoreCase)) subTpl = part.Substring(9);
                    else if (part.StartsWith("final=", StringComparison.OrdinalIgnoreCase)) finalTpl = part.Substring(6);
                }
            }

            string question = string.IsNullOrWhiteSpace(s.Query) ? (string.IsNullOrWhiteSpace(s.Prompt) ? s.Topic : s.Prompt) : s.Query;
            if (string.IsNullOrWhiteSpace(question)) return s;

            // Optional: warm retrieval cache using the main question
            try { s = await RetrieveSimilarDocuments($"amount={k}|query={question.Replace("|", ":")}")(s); } catch { /* ignore */ }

            // Default templates
            decomposeTpl ??= "You are tasked with answering a complex question by breaking it down into distinct sub-questions that together fully address the original.\n" +
                             "Main question: {question}\n\n" +
                             "Return exactly {N} non-overlapping sub-questions as a numbered list (1., 2., ...), one per line, focused and specific.";

            subTpl ??= "You are answering a sub-question as part of a larger task.\n" +
                      "Main question: {question}\nSub-question: {subquestion}\n\n" +
                      "Use the following context snippets to produce a precise, thorough answer. Cite facts from context; avoid speculation.\n" +
                      "Context:\n{context}\n\n" +
                      "Answer:";

            finalTpl ??= "Synthesize a high-quality final answer to the main question by integrating the following detailed sub-answers.\n" +
                       "Provide:\n- Executive summary (3-6 bullets)\n- Integrated comprehensive answer tying together all parts\n- If relevant: Considerations and Next steps\n\n" +
                       "Main question: {question}\n\nSub-answers:\n{pairs}\n\nFinal Answer:";

            // 1) Generate sub-questions
            string decomposePrompt = decomposeTpl
                .Replace("{question}", question)
                .Replace("{N}", subs.ToString());

            List<string> subQuestions = new();
            try
            {
                (string subText, List<ToolExecution> subCalls) = await s.Llm.GenerateWithToolsAsync(decomposePrompt);
                s.Branch = s.Branch.WithReasoning(new FinalSpec(subText ?? string.Empty), decomposePrompt, subCalls);
                if (!string.IsNullOrWhiteSpace(subText))
                {
                    foreach (string line in subText.Split('\n'))
                    {
                        string t = line.Trim();
                        if (string.IsNullOrWhiteSpace(t)) continue;
                        // Accept formats like: "1. ...", "1) ...", "- ..." or plain line
                        t = Regex.Replace(t, @"^\s*(\d+\.|\d+\)|[-*])\s*", string.Empty);
                        if (!string.IsNullOrWhiteSpace(t)) subQuestions.Add(t);
                    }
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"darag:decompose-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }

            if (subQuestions.Count == 0)
            {
                // Fallback: use the original question as a single sub-question
                subQuestions.Add(question);
            }
            else if (subQuestions.Count > subs)
            {
                subQuestions = subQuestions.Take(subs).ToList();
            }

            // 2) Answer each sub-question with retrieval
            List<(string q, string a)> qaPairs = new List<(string q, string a)>(subQuestions.Count);
            for (int i = 0; i < subQuestions.Count; i++)
            {
                string sq = subQuestions[i];
                // Retrieve per sub-question
                List<string> blocks = new();
                try
                {
                    if (s.Branch.Store is TrackedVectorStore tvs)
                    {
                        IReadOnlyCollection<Document> hits = await tvs.GetSimilarDocuments(s.Embed, sq, per);
                        foreach (Document doc in hits)
                        {
                            if (!string.IsNullOrWhiteSpace(doc.PageContent))
                                blocks.Add(doc.PageContent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"darag:retrieve-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }

                string ctx = string.Join(sep, blocks);
                string subPrompt = subTpl
                    .Replace("{context}", ctx)
                    .Replace("{subquestion}", sq)
                    .Replace("{question}", question)
                    .Replace("{prompt}", s.Prompt ?? string.Empty)
                    .Replace("{topic}", s.Topic ?? string.Empty);
                try
                {
                    (string ans, List<ToolExecution> toolCalls) = await s.Llm.GenerateWithToolsAsync(subPrompt);
                    string answer = ans ?? string.Empty;
                    qaPairs.Add((sq, answer));
                    s.Branch = s.Branch.WithReasoning(new FinalSpec(answer), subPrompt, toolCalls);
                    if (stream || s.Trace)
                    {
                        Console.WriteLine($"\n>> [darag] sub {i + 1}/{subQuestions.Count}: {sq}");
                        if (!string.IsNullOrWhiteSpace(answer)) Console.WriteLine(answer);
                        Console.Out.Flush();
                    }
                }
                catch (Exception ex)
                {
                    s.Branch = s.Branch.WithIngestEvent($"darag:sub-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
                }
            }

            // 3) Final synthesis
            StringBuilder sbPairs = new StringBuilder();
            for (int i = 0; i < qaPairs.Count; i++)
            {
                (string q, string a) = qaPairs[i];
                sbPairs.AppendLine($"Sub-question {i + 1}: {q}");
                sbPairs.AppendLine("Answer:");
                sbPairs.AppendLine(a);
                sbPairs.AppendLine();
            }

            string finalPrompt = finalTpl
                .Replace("{pairs}", sbPairs.ToString())
                .Replace("{question}", question)
                .Replace("{prompt}", s.Prompt ?? string.Empty)
                .Replace("{topic}", s.Topic ?? string.Empty);

            try
            {
                (string finalAnswer, List<ToolExecution> finalCalls) = await s.Llm.GenerateWithToolsAsync(finalPrompt);
                s.Output = finalAnswer ?? string.Empty;
                s.Prompt = finalPrompt;
                s.Branch = s.Branch.WithReasoning(new FinalSpec(s.Output), finalPrompt, finalCalls);
                if (s.Trace) Console.WriteLine($"[trace] DARAG final length={s.Output.Length}");
                if (stream)
                {
                    Console.WriteLine("\n=== DARAG FINAL ===");
                    Console.WriteLine(s.Output);
                    Console.Out.Flush();
                }
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"darag:final-error:{ex.GetType().Name}:{ex.Message.Replace('|', ':')}", Array.Empty<string>());
            }

            return s;
        };

    [PipelineToken("InstallDependenciesGuided", "GuidedInstall")]
    public static Step<CliPipelineState, CliPipelineState> InstallDependenciesGuided(string? args = null)
        => async s =>
        {
            string raw = ParseString(args);
            string? dep = null;
            string? error = null;

            if (!string.IsNullOrWhiteSpace(raw))
            {
                foreach (string part in raw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("dep=", StringComparison.OrdinalIgnoreCase)) dep = part.Substring(4);
                    else if (part.StartsWith("error=", StringComparison.OrdinalIgnoreCase)) error = part.Substring(6);
                }
            }

            string eventSource = "guided-install:triggered";
            if (!string.IsNullOrEmpty(dep)) eventSource += $":{dep}";
            if (!string.IsNullOrEmpty(error)) eventSource += $":{error}";

            s.Branch = s.Branch.WithIngestEvent(eventSource, Array.Empty<string>());
            
            // If we have a specific dependency, we might want to schedule a fix or prompt the user
            if (!string.IsNullOrEmpty(dep))
            {
                if (s.Trace) Console.WriteLine($"[guided-install] Dependency missing: {dep}");
                
                // In a real scenario, this might trigger an interactive prompt or a specific agent flow
            }

            return await Task.FromResult(s);
        };

    private static async Task<CliPipelineState> HandleDependencyExceptionAsync(CliPipelineState s, Exception ex)
    {
        string msg = ex.Message;
        string? dep = null;

        if (msg.Contains("NuGet", StringComparison.OrdinalIgnoreCase)) dep = "NuGet";
        else if (msg.Contains("npm", StringComparison.OrdinalIgnoreCase)) dep = "NPM";
        else if (msg.Contains("ollama", StringComparison.OrdinalIgnoreCase)) dep = "Ollama";
        else if (msg.Contains("pip", StringComparison.OrdinalIgnoreCase)) dep = "Python";
        else if (msg.Contains("docker", StringComparison.OrdinalIgnoreCase)) dep = "Docker";

        if (dep != null)
        {
            s.Branch = s.Branch.WithIngestEvent($"dependency:missing:{dep}", Array.Empty<string>());
            s.Branch = s.Branch.WithIngestEvent("schedule:guided-install", Array.Empty<string>());
            if (s.Trace) Console.WriteLine($"[dependency-handler] Detected missing dependency: {dep}");
        }
        else
        {
            s.Branch = s.Branch.WithIngestEvent($"error:generic:{ex.GetType().Name}", Array.Empty<string>());
        }

        return await Task.FromResult(s);
    }

    // Aliases for Pipe.cs compatibility
    public static Step<CliPipelineState, CliPipelineState> LangChainRetrieveStep(string? args = null)
        => RetrieveSimilarDocuments(args);

    public static Step<CliPipelineState, CliPipelineState> LangChainCombineStep(string? args = null)
        => CombineDocuments(args);

    public static Step<CliPipelineState, CliPipelineState> LangChainTemplateStep(string? args = null)
        => TemplateStep(args);

    public static Step<CliPipelineState, CliPipelineState> LangChainLlmStep(string? args = null)
        => LlmStep(args);
}

