using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

namespace Ouroboros.Application.SelfAssembly;

/// <summary>
/// Validates generated neuron source code for security violations before compilation.
/// Uses Roslyn's syntax tree analysis to detect forbidden namespaces and dangerous operations.
/// </summary>
public sealed class CodeSecurityValidator
{
    private readonly IReadOnlyList<string> _forbiddenNamespaces;
    private readonly IReadOnlyList<string> _dangerousMethodPatterns;

    /// <summary>
    /// Default forbidden namespaces that pose security risks.
    /// </summary>
    private static readonly string[] DefaultForbiddenNamespaces =
    [
        "System.Net",
        "System.Net.Http",
        "System.Net.Sockets",
        "System.Net.Mail",
        "System.Net.NetworkInformation",
        "System.IO",
        "System.IO.Compression",
        "System.IO.Pipes",
        "System.Diagnostics.Process",
        "System.Reflection.Emit",
        "System.Runtime.InteropServices",
        "Microsoft.Win32"
    ];

    /// <summary>
    /// Dangerous method call patterns that should be blocked.
    /// </summary>
    private static readonly string[] DefaultDangerousMethodPatterns =
    [
        "Assembly.Load",
        "Assembly.LoadFrom",
        "Assembly.LoadFile",
        "Assembly.UnsafeLoadFrom",
        ".InvokeMember",  // Catches Type.InvokeMember, etc.
        "Activator.CreateInstanceFrom",
        "AppDomain.CreateInstanceFrom",
        "Marshal.GetDelegateForFunctionPointer"
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeSecurityValidator"/> class.
    /// </summary>
    /// <param name="additionalForbiddenNamespaces">Additional namespaces to forbid beyond defaults.</param>
    public CodeSecurityValidator(IReadOnlyList<string>? additionalForbiddenNamespaces = null)
    {
        var allForbidden = new List<string>(DefaultForbiddenNamespaces);
        if (additionalForbiddenNamespaces != null)
        {
            allForbidden.AddRange(additionalForbiddenNamespaces);
        }

        _forbiddenNamespaces = allForbidden;
        _dangerousMethodPatterns = DefaultDangerousMethodPatterns;
    }

    /// <summary>
    /// Validates the provided source code for security violations.
    /// </summary>
    /// <param name="sourceCode">The C# source code to validate.</param>
    /// <returns>Success if no violations found, or Failure with detailed violation messages.</returns>
    public Result<Unit> Validate(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return Result<Unit>.Failure("Source code cannot be empty");
        }

        try
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = syntaxTree.GetRoot();
            var violations = new List<string>();

            var walker = new SecuritySyntaxWalker(_forbiddenNamespaces, _dangerousMethodPatterns, violations);
            walker.Visit(root);

            if (violations.Count > 0)
            {
                var violationMessage = $"Security validation failed. Found {violations.Count} violation(s):\n" +
                                     string.Join("\n", violations.Select((v, i) => $"  {i + 1}. {v}"));
                return Result<Unit>.Failure(violationMessage);
            }

            return Result<Unit>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit>.Failure($"Security validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Syntax walker that identifies security violations in the AST.
    /// </summary>
    private sealed class SecuritySyntaxWalker : CSharpSyntaxWalker
    {
        private readonly IReadOnlyList<string> _forbiddenNamespaces;
        private readonly IReadOnlyList<string> _dangerousMethodPatterns;
        private readonly List<string> _violations;

        public SecuritySyntaxWalker(
            IReadOnlyList<string> forbiddenNamespaces,
            IReadOnlyList<string> dangerousMethodPatterns,
            List<string> violations)
        {
            _forbiddenNamespaces = forbiddenNamespaces;
            _dangerousMethodPatterns = dangerousMethodPatterns;
            _violations = violations;
        }

        /// <summary>
        /// Visits using directives to check for forbidden namespace imports.
        /// </summary>
        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            var namespaceName = node.Name?.ToString();
            if (namespaceName != null && IsForbiddenNamespace(namespaceName))
            {
                _violations.Add($"Forbidden using directive: '{namespaceName}' (line {GetLineNumber(node)})");
            }

            base.VisitUsingDirective(node);
        }

        /// <summary>
        /// Visits identifier names to check for fully-qualified forbidden type references.
        /// </summary>
        public override void VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Check if this identifier is part of a qualified name that might be forbidden
            if (node.Parent is QualifiedNameSyntax)
            {
                // Will be handled by VisitQualifiedName
                base.VisitIdentifierName(node);
                return;
            }

            // Check for direct references to forbidden types (e.g., when using a namespace alias)
            var identifierText = node.Identifier.Text;
            if (IsPotentiallyForbiddenType(identifierText))
            {
                _violations.Add($"Potentially forbidden type reference: '{identifierText}' (line {GetLineNumber(node)})");
            }

            base.VisitIdentifierName(node);
        }

        /// <summary>
        /// Visits qualified names to check for fully-qualified forbidden type references.
        /// </summary>
        public override void VisitQualifiedName(QualifiedNameSyntax node)
        {
            var fullName = node.ToString();
            if (IsForbiddenNamespace(fullName) || StartsWithForbiddenNamespace(fullName))
            {
                _violations.Add($"Forbidden fully-qualified type: '{fullName}' (line {GetLineNumber(node)})");
            }

            base.VisitQualifiedName(node);
        }

        /// <summary>
        /// Visits member access expressions to catch forbidden namespace/type usage.
        /// </summary>
        public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var memberAccess = node.ToString();
            if (StartsWithForbiddenNamespace(memberAccess))
            {
                _violations.Add($"Forbidden member access: '{memberAccess}' (line {GetLineNumber(node)})");
            }

            base.VisitMemberAccessExpression(node);
        }

        /// <summary>
        /// Visits invocation expressions to check for dangerous method calls.
        /// </summary>
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var invocation = node.Expression.ToString();

            foreach (var pattern in _dangerousMethodPatterns)
            {
                if (invocation.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    _violations.Add($"Dangerous method call: '{invocation}' matches pattern '{pattern}' (line {GetLineNumber(node)})");
                    break;
                }
            }

            base.VisitInvocationExpression(node);
        }

        /// <summary>
        /// Checks if a namespace or type name is forbidden.
        /// </summary>
        private bool IsForbiddenNamespace(string namespaceName)
        {
            return _forbiddenNamespaces.Any(forbidden =>
                namespaceName.Equals(forbidden, StringComparison.OrdinalIgnoreCase) ||
                namespaceName.StartsWith(forbidden + ".", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if a name starts with a forbidden namespace.
        /// </summary>
        private bool StartsWithForbiddenNamespace(string name)
        {
            return _forbiddenNamespaces.Any(forbidden =>
                name.Equals(forbidden, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(forbidden + ".", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if an identifier might reference a forbidden type.
        /// </summary>
        private bool IsPotentiallyForbiddenType(string identifier)
        {
            // Check for common forbidden type names
            var forbiddenTypeNames = new[] { "HttpClient", "File", "Directory", "Process", "Socket", "Registry" };
            return forbiddenTypeNames.Contains(identifier, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the line number for a syntax node.
        /// </summary>
        private static int GetLineNumber(SyntaxNode node)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            return lineSpan.StartLinePosition.Line + 1;
        }
    }
}
