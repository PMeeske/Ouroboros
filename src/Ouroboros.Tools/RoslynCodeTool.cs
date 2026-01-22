using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Ouroboros.Core;

namespace Ouroboros.Tools
{
    /// <summary>
    /// Tool for analyzing and manipulating C# code using Roslyn.
    /// </summary>
    public class RoslynCodeTool
    {
        /// <summary>
        /// Analyzes C# code and returns structural information.
        /// </summary>
        /// <param name="code">The C# code to analyze.</param>
        /// <returns>Analysis result with classes, methods, and diagnostics.</returns>
        public async Task<CodeAnalysisResult> AnalyzeCode(string code)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("Analysis")
                .AddSyntaxTrees(tree)
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            var diagnostics = compilation.GetDiagnostics().ToArray();
            var classes = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Select(c => c.Identifier.Text).ToArray();
            var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Select(m => m.Identifier.Text).ToArray();

            return new CodeAnalysisResult(classes, methods, diagnostics);
        }

        /// <summary>
        /// Generates a C# class with specified properties and methods.
        /// </summary>
        /// <param name="className">The class name.</param>
        /// <param name="namespace">The namespace.</param>
        /// <param name="methods">List of method names.</param>
        /// <param name="properties">List of property declarations.</param>
        /// <returns>Generated C# code.</returns>
        public string GenerateClass(string className, string @namespace, IEnumerable<string> methods, IEnumerable<string> properties)
        {
            var props = properties.Select(p => $"    public {p} {{ get; set; }}").ToArray();
            var meths = methods.Select(m => $"    public async Task {m}() => await Task.CompletedTask;").ToArray();

            return $@"
namespace {@namespace}
{{
    public class {className}
    {{
{string.Join(Environment.NewLine, props)}
{string.Join(Environment.NewLine, meths)}
    }}
}}";
        }

        /// <summary>
        /// Adds a method to existing C# code.
        /// </summary>
        /// <param name="existingCode">The existing code.</param>
        /// <param name="signature">The method signature.</param>
        /// <param name="body">The method body.</param>
        /// <returns>Updated code.</returns>
        public string AddMethod(string existingCode, string signature, string body)
        {
            var tree = CSharpSyntaxTree.ParseText(existingCode);
            var root = tree.GetRoot();

            var classDecl = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDecl == null) return existingCode;

            var methodDecl = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("int"),
                signature.Split(' ')[2].Split('(')[0])
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                .WithParameterList(SyntaxFactory.ParseParameterList(signature.Split('(')[1].Split(')')[0]))
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(body)));

            var newClass = classDecl.AddMembers(methodDecl);
            var newRoot = root.ReplaceNode(classDecl, newClass);

            return newRoot.ToFullString();
        }

        /// <summary>
        /// Renames a symbol in the code.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="oldName">The old name.</param>
        /// <param name="newName">The new name.</param>
        /// <returns>Updated code.</returns>
        public string RenameSymbol(string code, string oldName, string newName)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(id => id.Identifier.Text == oldName);

            var newRoot = root.ReplaceNodes(identifiers, (old, _) =>
                old.WithIdentifier(SyntaxFactory.Identifier(newName)));

            return newRoot.ToFullString();
        }

        /// <summary>
        /// Performs extract method refactoring.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="startLine">Start line.</param>
        /// <param name="endLine">End line.</param>
        /// <param name="methodName">New method name.</param>
        /// <returns>Refactored code.</returns>
        public string ExtractMethod(string code, int startLine, int endLine, string methodName)
        {
            // Simplified implementation - in real scenario, use Roslyn refactoring APIs
            var lines = code.Split(Environment.NewLine);
            var extracted = string.Join(Environment.NewLine, lines.Skip(startLine - 1).Take(endLine - startLine + 1));
            var newMethod = $@"
    private void {methodName}()
    {{
{extracted}
    }}";

            lines[startLine - 1] = $"{methodName}();";
            for (int i = startLine; i <= endLine; i++)
                lines[i] = string.Empty;

            return string.Join(Environment.NewLine, lines) + newMethod;
        }

        /// <summary>
        /// Runs custom analyzers on the code.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns>Analysis result with findings.</returns>
        public async Task<CodeAnalysisResult> AnalyzeWithCustomAnalyzers(string code)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var compilation = CSharpCompilation.Create("Analysis")
                .AddSyntaxTrees(tree)
                .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));

            // Custom analyzer for async patterns
            var findings = new List<string>();
            var awaitExpressions = tree.GetRoot().DescendantNodes().OfType<AwaitExpressionSyntax>();
            var invocations = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                if (invocation.Expression.ToString().EndsWith(".Result") || invocation.Expression.ToString().EndsWith(".Wait()"))
                {
                    findings.Add($"Blocking call detected: {invocation}");
                }
            }

            return new CodeAnalysisResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Diagnostic>(), findings.ToArray());
        }

        /// <summary>
        /// Analyzes documentation in the code.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <returns>Analysis result with documentation findings.</returns>
        public async Task<CodeAnalysisResult> AnalyzeDocumentation(string code)
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                .Where(m => !m.HasLeadingTrivia || !m.GetLeadingTrivia().Any(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)))
                .Select(m => m.Identifier.Text)
                .ToArray();

            var findings = methods.Select(m => $"Missing documentation for method: {m}").ToArray();

            return new CodeAnalysisResult(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<Diagnostic>(), findings);
        }
    }

    /// <summary>
    /// Result of code analysis.
    /// </summary>
    public class CodeAnalysisResult
    {
        public string[] Classes { get; }
        public string[] Methods { get; }
        public Diagnostic[] Diagnostics { get; }
        public string[] Findings { get; }
        public bool IsValid => !Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        public CodeAnalysisResult(string[] classes, string[] methods, Diagnostic[] diagnostics, string[]? findings = null)
        {
            Classes = classes;
            Methods = methods;
            Diagnostics = diagnostics;
            Findings = findings ?? Array.Empty<string>();
        }
    }
}
