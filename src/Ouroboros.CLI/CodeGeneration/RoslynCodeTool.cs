#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.MSBuild;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Roslynator.Providers;
using LangChainPipeline.Roslynator.Pipeline;
using System.Collections.Immutable;

namespace LangChainPipeline.CLI.CodeGeneration;

/// <summary>
/// Roslyn-based code analysis, generation, and refactoring tool.
/// Provides full code creation/edit capabilities with analyzer support.
/// </summary>
public class RoslynCodeTool
{
    private readonly AdhocWorkspace _workspace;

    public RoslynCodeTool()
    {
        _workspace = new AdhocWorkspace();
    }

    /// <summary>
    /// Analyzes C# code and returns diagnostics, syntax tree information, and analyzer results.
    /// </summary>
    public async Task<Result<CodeAnalysisResult, string>> AnalyzeCodeAsync(
        string code,
        string? filePath = null,
        bool runAnalyzers = true)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code, path: filePath ?? "code.cs");
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Create compilation for full analysis
            CSharpCompilation compilation = CSharpCompilation.Create(
                "Analysis",
                new[] { tree },
                GetDefaultReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            // Get diagnostics
            IEnumerable<Diagnostic> allDiagnostics = compilation.GetDiagnostics();
            List<string> diagnostics = allDiagnostics
                .Select(d => $"{d.Severity}: {d.Id} - {d.GetMessage()} at {d.Location.GetLineSpan()}")
                .ToList();

            // Extract code structure
            List<string> classes = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Select(c => c.Identifier.Text)
                .ToList();

            List<string> methods = root.DescendantNodes()
                .OfType<MethodDeclarationSyntax>()
                .Select(m => $"{m.ReturnType} {m.Identifier.Text}({string.Join(", ", m.ParameterList.Parameters)})")
                .ToList();

            List<string> usings = root.Usings
                .Select(u => u.Name?.ToString() ?? string.Empty)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();

            // Run custom analyzers if requested
            List<string> analyzerResults = new List<string>();
            if (runAnalyzers)
            {
                analyzerResults = await RunCustomAnalyzersAsync(tree, compilation);
            }

            CodeAnalysisResult result = new CodeAnalysisResult
            {
                IsValid = !allDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                Diagnostics = diagnostics,
                Classes = classes,
                Methods = methods,
                Usings = usings,
                AnalyzerResults = analyzerResults,
                SyntaxTree = tree,
                Compilation = compilation
            };

            return Result<CodeAnalysisResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<CodeAnalysisResult, string>.Failure($"Code analysis failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new C# class with specified structure using Roslyn generators.
    /// </summary>
    public Result<string, string> CreateClass(
        string className,
        string namespaceName,
        List<string>? methods = null,
        List<string>? properties = null,
        List<string>? usings = null,
        string? baseClass = null,
        List<string>? interfaces = null,
        bool addDocComments = true)
    {
        try
        {
            // Build using directives
            List<UsingDirectiveSyntax> usingDirectives = new List<UsingDirectiveSyntax>();
            foreach (string usingName in usings ?? new List<string> { "System", "System.Threading.Tasks" })
            {
                usingDirectives.Add(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(usingName)));
            }

            // Build base type list
            BaseListSyntax? baseList = null;
            if (!string.IsNullOrEmpty(baseClass) || (interfaces?.Count ?? 0) > 0)
            {
                List<BaseTypeSyntax> baseTypes = new List<BaseTypeSyntax>();
                if (!string.IsNullOrEmpty(baseClass))
                {
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseClass)));
                }
                foreach (string iface in interfaces ?? new List<string>())
                {
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(iface)));
                }
                baseList = SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes));
            }

            // Create class members
            List<MemberDeclarationSyntax> members = new List<MemberDeclarationSyntax>();

            // Add properties
            foreach (string prop in properties ?? new List<string>())
            {
                PropertyDeclarationSyntax property = ParseProperty(prop);
                if (addDocComments)
                {
                    property = AddXmlDocComment(property, $"Gets or sets the {property.Identifier.Text}.");
                }
                members.Add(property);
            }

            // Add methods
            foreach (string method in methods ?? new List<string>())
            {
                MethodDeclarationSyntax methodSyntax = ParseMethod(method);
                if (addDocComments)
                {
                    methodSyntax = AddXmlDocComment(methodSyntax, $"Executes {methodSyntax.Identifier.Text} operation.");
                }
                members.Add(methodSyntax);
            }

            // Build class declaration
            ClassDeclarationSyntax classDeclaration = SyntaxFactory.ClassDeclaration(className)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithMembers(SyntaxFactory.List(members));

            if (baseList != null)
            {
                classDeclaration = classDeclaration.WithBaseList(baseList);
            }

            if (addDocComments)
            {
                classDeclaration = AddXmlDocComment(classDeclaration, $"Represents a {className}.");
            }

            // Build namespace
            NamespaceDeclarationSyntax namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
                SyntaxFactory.ParseName(namespaceName))
                .AddMembers(classDeclaration);

            // Build compilation unit
            CompilationUnitSyntax compilationUnit = SyntaxFactory.CompilationUnit()
                .AddUsings(usingDirectives.ToArray())
                .AddMembers(namespaceDeclaration)
                .WithLeadingTrivia(
                    SyntaxFactory.Comment("#pragma warning disable CS1591"));

            // Format code
            SyntaxNode formatted = Formatter.Format(compilationUnit, _workspace);
            string code = formatted.ToFullString();

            return Result<string, string>.Success(code);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Class creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Adds a method to an existing class.
    /// </summary>
    public Result<string, string> AddMethodToClass(
        string code,
        string className,
        string methodSignature,
        string? methodBody = null)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Find the class
            ClassDeclarationSyntax? targetClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text == className);

            if (targetClass == null)
            {
                return Result<string, string>.Failure($"Class '{className}' not found");
            }

            // Parse method
            MethodDeclarationSyntax newMethod = ParseMethod(methodSignature, methodBody);

            // Add method to class
            ClassDeclarationSyntax modifiedClass = targetClass.AddMembers(newMethod);

            // Replace in tree
            SyntaxNode newRoot = root.ReplaceNode(targetClass, modifiedClass);
            SyntaxNode formatted = Formatter.Format(newRoot, _workspace);

            return Result<string, string>.Success(formatted.ToFullString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to add method: {ex.Message}");
        }
    }

    /// <summary>
    /// Refactors code by renaming a symbol (Roslyn-powered rename refactoring).
    /// </summary>
    public Result<string, string> RenameSymbol(string code, string oldName, string newName)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Find all identifiers with the old name
            IEnumerable<IdentifierNameSyntax> identifiers = root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(i => i.Identifier.Text == oldName);

            SyntaxNode newRoot = root.ReplaceNodes(identifiers, (original, _) =>
                SyntaxFactory.IdentifierName(newName).WithTriviaFrom(original));

            // Also handle declarations
            IEnumerable<SyntaxToken> declarationTokens = root.DescendantTokens()
                .Where(t => t.IsKind(SyntaxKind.IdentifierToken) && t.Text == oldName);

            newRoot = newRoot.ReplaceTokens(declarationTokens, (original, _) =>
                SyntaxFactory.Identifier(newName).WithTriviaFrom(original));

            SyntaxNode formatted = Formatter.Format(newRoot, _workspace);
            return Result<string, string>.Success(formatted.ToFullString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Rename failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts a method from selected code (Roslyn refactoring).
    /// </summary>
    public Result<string, string> ExtractMethod(
        string code,
        int startLine,
        int endLine,
        string newMethodName)
    {
        try
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(code);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            // Find statements in the range
            TextSpan span = GetSpanForLines(tree.GetText(), startLine, endLine);
            IEnumerable<StatementSyntax> statements = root.DescendantNodes()
                .OfType<StatementSyntax>()
                .Where(s => span.Contains(s.Span));

            if (!statements.Any())
            {
                return Result<string, string>.Failure("No statements found in the specified range");
            }

            // Create new method
            MethodDeclarationSyntax newMethod = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                newMethodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .WithBody(SyntaxFactory.Block(statements));

            // Find containing method
            MethodDeclarationSyntax? containingMethod = statements.First()
                .Ancestors()
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault();

            if (containingMethod == null)
            {
                return Result<string, string>.Failure("Could not find containing method");
            }

            // Replace statements with method call
            ExpressionStatementSyntax methodCall = SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(newMethodName)));

            BlockSyntax? originalBody = containingMethod.Body;
            if (originalBody == null)
            {
                return Result<string, string>.Failure("Method has no body");
            }

            // Remove extracted statements and add method call
            List<StatementSyntax> newStatements = originalBody.Statements
                .Where(s => !statements.Contains(s))
                .ToList();
            
            // Insert method call at the position of first extracted statement
            int insertIndex = originalBody.Statements.IndexOf(statements.First());
            newStatements.Insert(insertIndex, methodCall);

            BlockSyntax newBody = SyntaxFactory.Block(newStatements);
            MethodDeclarationSyntax modifiedMethod = containingMethod.WithBody(newBody);

            // Find containing class and add new method
            ClassDeclarationSyntax? containingClass = containingMethod
                .Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();

            if (containingClass == null)
            {
                return Result<string, string>.Failure("Could not find containing class");
            }

            ClassDeclarationSyntax modifiedClass = containingClass
                .ReplaceNode(containingMethod, modifiedMethod)
                .AddMembers(newMethod);

            SyntaxNode newRoot = root.ReplaceNode(containingClass, modifiedClass);
            SyntaxNode formatted = Formatter.Format(newRoot, _workspace);

            return Result<string, string>.Success(formatted.ToFullString());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Extract method failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates code based on natural language description using AI + Roslyn validation.
    /// </summary>
    public async Task<Result<string, string>> GenerateCodeFromDescriptionAsync(
        string description,
        string codeContext,
        ToolAwareChatModel llm)
    {
        try
        {
            string prompt = $@"You are an expert C# developer using Roslyn and functional programming patterns.

Context:
{codeContext}

Task: {description}

Generate complete, production-quality C# code that:
1. Follows functional programming principles (immutability, pure functions)
2. Uses Result<T> and Option<T> monads for error handling
3. Includes XML documentation comments (///)
4. Is properly formatted
5. Includes appropriate using statements
6. Uses async/await for I/O operations
7. Follows Ouroboros conventions

Respond with only the C# code, no explanations.";

            (string response, List<ToolExecution> _) = await llm.GenerateWithToolsAsync(prompt);

            // Extract code block if wrapped in markdown
            string code = ExtractCodeFromMarkdown(response);

            // Validate generated code with Roslyn
            Result<CodeAnalysisResult, string> analysisResult = await AnalyzeCodeAsync(code);
            if (analysisResult.IsFailure)
            {
                return Result<string, string>.Failure($"Generated code analysis failed: {analysisResult.Error}");
            }

            if (!analysisResult.Value.IsValid)
            {
                string errors = string.Join("\n", analysisResult.Value.Diagnostics.Take(5));
                return Result<string, string>.Failure($"Generated code has errors:\n{errors}");
            }

            return Result<string, string>.Success(code);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Code generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a source generator for compile-time code generation.
    /// </summary>
    public Result<string, string> GenerateSourceGenerator(string generatorName, string generationLogic)
    {
        try
        {
            string sourceGeneratorCode = $@"#pragma warning disable CS1591
using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LangChainPipeline.SourceGenerators
{{
    [Generator]
    public class {generatorName} : ISourceGenerator
    {{
        public void Initialize(GeneratorInitializationContext context)
        {{
            // Register syntax receiver if needed
        }}

        public void Execute(GeneratorExecutionContext context)
        {{
            try
            {{
                {generationLogic}
                
                // Example: Add generated source
                context.AddSource(""{generatorName}Generated.cs"", SourceText.From(generatedCode, Encoding.UTF8));
            }}
            catch (Exception ex)
            {{
                // Report diagnostic
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        ""SG001"",
                        ""Source Generation Error"",
                        $""{{ex.Message}}"",
                        ""SourceGenerator"",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None));
            }}
        }}
    }}
}}";

            return Result<string, string>.Success(sourceGeneratorCode);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Generator creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies the UniversalCodeFixProvider (AI + Standard) to the given code.
    /// </summary>
    public async Task<Result<string, string>> ApplyUniversalFixAsync(string code, string diagnosticId)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();
            
            // Create a dummy compilation to get diagnostics
            var compilation = CSharpCompilation.Create("FixCompilation")
                .AddReferences(GetDefaultReferences())
                .AddSyntaxTrees(tree)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(NullableContextOptions.Enable));

            var diagnostics = compilation.GetDiagnostics();
            var targetDiagnostic = diagnostics.FirstOrDefault(d => d.Id == diagnosticId);

            if (targetDiagnostic == null)
            {
                var allDiags = string.Join(", ", diagnostics.Select(d => d.Id));
                return Result<string, string>.Failure($"Diagnostic {diagnosticId} not found in code. Found: {allDiags}");
            }

            // Create a dummy document/project/solution for the fix provider
            var workspace = new AdhocWorkspace();
            var project = workspace.AddProject("FixProject", LanguageNames.CSharp)
                .AddMetadataReferences(GetDefaultReferences());
            var document = project.AddDocument("FixDocument.cs", root);

            // Re-fetch diagnostic on the document context if needed, or just pass the one we have
            // Note: FixChain.ExecuteAsync takes a Document and Diagnostic.
            
            // Instantiate the provider to access the chain logic directly or via its public methods if exposed.
            // However, UniversalCodeFixProvider is a CodeFixProvider. 
            // We can access the inner chain logic if we made it public or use the provider's logic.
            // The UniversalCodeFixProvider.ConcreteChains.UniversalChain is private/internal.
            // But we can instantiate UniversalCodeFixProvider and see if we can leverage it, 
            // OR we can just use the FixChain infrastructure if we expose the specific chain.
            
            // Let's assume we want to run the specific UniversalChain logic.
            // Since UniversalCodeFixProvider defines the chain in RegisterCodeFixesAsync, 
            // we might need to expose the chain or duplicate the composition here.
            // Let's look at UniversalCodeFixProvider again. It has a nested class ConcreteChains.UniversalChain.
            
            var chain = new UniversalCodeFixProvider.ConcreteChains.UniversalChain();
            Document newDocument = await chain.ExecuteAsync(document, targetDiagnostic, CancellationToken.None);
            
            SyntaxNode? newRoot = await newDocument.GetSyntaxRootAsync();
            return Result<string, string>.Success(newRoot?.ToFullString() ?? code);
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Fix failed: {ex.Message}");
        }
    }

    // Private helper methods

    private async Task<List<string>> RunCustomAnalyzersAsync(SyntaxTree tree, Compilation compilation)
    {
        List<string> results = new List<string>();

        // Monadic pattern analyzer
        results.AddRange(await AnalyzeMonadicPatternsAsync(tree));

        // Async/await analyzer
        results.AddRange(AnalyzeAsyncPatterns(tree));

        // Documentation analyzer
        results.AddRange(AnalyzeDocumentation(tree));

        return results;
    }

    private async Task<List<string>> AnalyzeMonadicPatternsAsync(SyntaxTree tree)
    {
        List<string> findings = new List<string>();
        CompilationUnitSyntax root = await tree.GetRootAsync() as CompilationUnitSyntax ?? throw new InvalidOperationException();

        // Check for Result<T> usage
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (method.ReturnType.ToString().Contains("Task") &&
                !method.ReturnType.ToString().Contains("Result"))
            {
                findings.Add($"Method {method.Identifier} returns Task but not Result<T>. Consider using Result monad.");
            }
        }

        return findings;
    }

    private List<string> AnalyzeAsyncPatterns(SyntaxTree tree)
    {
        List<string> findings = new List<string>();
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // Check for blocking async calls
        var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            string invocationText = invocation.ToString();
            if (invocationText.Contains(".Result") || invocationText.Contains(".Wait()"))
            {
                findings.Add($"Potential blocking async call at line {invocation.GetLocation().GetLineSpan().StartLinePosition.Line}: {invocationText}");
            }
        }

        return findings;
    }

    private List<string> AnalyzeDocumentation(SyntaxTree tree)
    {
        List<string> findings = new List<string>();
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // Check for missing XML documentation on public members
        var publicMembers = root.DescendantNodes()
            .Where(n => n is ClassDeclarationSyntax || n is MethodDeclarationSyntax)
            .Where(n =>
            {
                SyntaxTokenList modifiers = n switch
                {
                    ClassDeclarationSyntax c => c.Modifiers,
                    MethodDeclarationSyntax m => m.Modifiers,
                    _ => default
                };
                return modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
            });

        foreach (var member in publicMembers)
        {
            bool hasDocComment = member.GetLeadingTrivia()
                .Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                          t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));

            if (!hasDocComment)
            {
                string memberName = member switch
                {
                    ClassDeclarationSyntax c => c.Identifier.Text,
                    MethodDeclarationSyntax m => m.Identifier.Text,
                    _ => "unknown"
                };
                findings.Add($"Public member '{memberName}' missing XML documentation");
            }
        }

        return findings;
    }

    private PropertyDeclarationSyntax ParseProperty(string propertySpec)
    {
        // Parse various formats: "public string Name { get; set; }" or "string Name" or "Type Name"
        string trimmed = propertySpec.Trim().TrimEnd(';');
        
        // Try to parse as full property declaration
        MemberDeclarationSyntax? member = SyntaxFactory.ParseMemberDeclaration(trimmed);
        if (member is PropertyDeclarationSyntax existingProp)
        {
            return existingProp;
        }

        // Parse simple format: "Type Name"
        string[] parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            throw new ArgumentException($"Invalid property specification: {propertySpec}");
        }

        string typeName = parts[^2];
        string propName = parts[^1];

        return SyntaxFactory.PropertyDeclaration(
            SyntaxFactory.ParseTypeName(typeName),
            propName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
    }

    private MethodDeclarationSyntax ParseMethod(string methodSpec, string? body = null)
    {
        // Parse: "public async Task<Result<string>> DoSomething(string arg)" or "DoSomething"
        string methodCode = body != null
            ? $"{methodSpec} {{ {body} }}"
            : methodSpec.Contains("(") ? $"{methodSpec} {{ }}" : $"public void {methodSpec}() {{ }}";

        MemberDeclarationSyntax? member = SyntaxFactory.ParseMemberDeclaration(methodCode);
        if (member is MethodDeclarationSyntax method)
        {
            return method;
        }

        // Fallback: create simple method
        return SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
            methodSpec)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBody(SyntaxFactory.Block());
    }

    private T AddXmlDocComment<T>(T member, string summary) where T : MemberDeclarationSyntax
    {
        string xmlDoc = $@"/// <summary>
/// {summary}
/// </summary>";

        SyntaxTriviaList trivia = SyntaxFactory.ParseLeadingTrivia(xmlDoc + Environment.NewLine);
        return member.WithLeadingTrivia(trivia);
    }

    private TextSpan GetSpanForLines(SourceText text, int startLine, int endLine)
    {
        TextLine start = text.Lines[startLine];
        TextLine end = text.Lines[endLine];
        return TextSpan.FromBounds(start.Start, end.End);
    }

    private string ExtractCodeFromMarkdown(string response)
    {
        // Extract code from markdown code blocks
        string pattern = @"```(?:csharp|cs|c#)?\s*\n(.*?)\n```";
        System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(
            response,
            pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return match.Success ? match.Groups[1].Value.Trim() : response.Trim();
    }

    private IEnumerable<MetadataReference> GetDefaultReferences()
    {
        // Add common .NET references for compilation
        string assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location) ?? string.Empty;
        
        return new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Runtime.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Collections.dll")),
            MetadataReference.CreateFromFile(Path.Combine(assemblyPath, "System.Linq.dll")),
        };
    }
}

/// <summary>
/// Result of comprehensive code analysis including Roslyn analyzers.
/// </summary>
public class CodeAnalysisResult
{
    public bool IsValid { get; set; }
    public List<string> Diagnostics { get; set; } = new List<string>();
    public List<string> Classes { get; set; } = new List<string>();
    public List<string> Methods { get; set; } = new List<string>();
    public List<string> Usings { get; set; } = new List<string>();
    public List<string> AnalyzerResults { get; set; } = new List<string>();
    public SyntaxTree? SyntaxTree { get; set; }
    public Compilation? Compilation { get; set; }
}
