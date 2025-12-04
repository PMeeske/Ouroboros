using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace LangChainPipeline.Roslynator.Pipeline.Steps;

/// <summary>
/// Minimal placeholders for standard deterministic steps.
/// Replace with richer implementations (Roslyn/roslynator helpers) as desired.
/// </summary>
public static class StandardSteps
{
    // Try deterministic fixes. Return state unchanged by default.
    public static async Task<FixState> TryResolve(FixState state)
    {
        SyntaxNode? root = await state.Document.GetSyntaxRootAsync().ConfigureAwait(false);
        if (root == null) return state;

        SyntaxNode? node = root.FindNode(state.Diagnostic.Location.SourceSpan);

        if (state.Diagnostic.Id == "CS0168" || state.Diagnostic.Id == "CS0219")
        {
            // Simple fix: remove the unused variable declaration
            LocalDeclarationStatementSyntax? statement = node?.AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>().FirstOrDefault();
            if (statement != null)
            {
                SyntaxNode? newRoot = root.RemoveNode(statement, SyntaxRemoveOptions.KeepNoTrivia);
                if (newRoot != null)
                {
                    Document newDoc = state.Document.WithSyntaxRoot(newRoot);
                    return state.WithNewRoot(newRoot, "Remove unused variable") with { Document = newDoc };
                }
            }
        }
        else if (state.Diagnostic.Id == "CS8600")
        {
            // Fix: Make type nullable (Converting null literal or possible null value to non-nullable type)
            VariableDeclarationSyntax? variableDeclaration = node?.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().FirstOrDefault();
            if (variableDeclaration != null && variableDeclaration.Type is not NullableTypeSyntax)
            {
                // Check if it's 'var' (which shouldn't happen for CS8600 usually, but good to check)
                bool isVar = variableDeclaration.Type is IdentifierNameSyntax id && id.Identifier.ValueText == "var";
                
                if (!isVar)
                {
                    TypeSyntax type = variableDeclaration.Type;
                    TypeSyntax newType = SyntaxFactory.NullableType(type.WithoutTrailingTrivia())
                        .WithTrailingTrivia(type.GetTrailingTrivia());
                    VariableDeclarationSyntax newVarDecl = variableDeclaration.WithType(newType);
                    SyntaxNode? newRoot = root.ReplaceNode(variableDeclaration, newVarDecl);
                    if (newRoot != null)
                    {
                        Document newDoc = state.Document.WithSyntaxRoot(newRoot);
                        return state.WithNewRoot(newRoot, "Fix CS8600 (Make nullable)") with { Document = newDoc };
                    }
                }
            }
        }
        else if (state.Diagnostic.Id == "CS8602")
        {
            // Fix: Change . to ?. (Dereference of a possibly null reference)
            // Case: MemberAccessExpression (a.b)
            MemberAccessExpressionSyntax? memberAccess = node?.AncestorsAndSelf().OfType<MemberAccessExpressionSyntax>().FirstOrDefault();
            if (memberAccess != null)
            {
                // a.b -> a?.b
                ExpressionSyntax expression = memberAccess.Expression;
                SimpleNameSyntax name = memberAccess.Name;
                
                // Create ConditionalAccessExpression
                // Expression: expression
                // WhenNotNull: MemberBindingExpression(name)
                MemberBindingExpressionSyntax memberBinding = SyntaxFactory.MemberBindingExpression(name);
                ConditionalAccessExpressionSyntax conditionalAccess = SyntaxFactory.ConditionalAccessExpression(expression, memberBinding);
                
                SyntaxNode? newRoot = root.ReplaceNode(memberAccess, conditionalAccess);
                if (newRoot != null)
                {
                    Document newDoc = state.Document.WithSyntaxRoot(newRoot);
                    return state.WithNewRoot(newRoot, "Fix CS8602 (Use ?.)") with { Document = newDoc };
                }
            }
        }

        else if (state.Diagnostic.Id == "CS0266")
        {
            // Fix: Cannot implicitly convert type 'int?' to 'int'.
            // Strategy: Change the variable type to nullable if it's a variable declaration.
            VariableDeclarationSyntax? variableDeclaration = node?.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().FirstOrDefault();
            if (variableDeclaration != null)
            {
                TypeSyntax type = variableDeclaration.Type;
                if (type is not NullableTypeSyntax && type is not IdentifierNameSyntax { Identifier: { ValueText: "var" } })
                {
                    TypeSyntax newType = SyntaxFactory.NullableType(type.WithoutTrailingTrivia())
                        .WithTrailingTrivia(type.GetTrailingTrivia());
                    VariableDeclarationSyntax newVarDecl = variableDeclaration.WithType(newType);
                    SyntaxNode? newRoot = root.ReplaceNode(variableDeclaration, newVarDecl);
                    if (newRoot != null)
                    {
                        Document newDoc = state.Document.WithSyntaxRoot(newRoot);
                        return state.WithNewRoot(newRoot, "Fix CS0266 (Make nullable)") with { Document = newDoc };
                    }
                }
            }
        }
        else if (state.Diagnostic.Id == "CS8019")
        {
            // Fix: Remove unnecessary using directive
            UsingDirectiveSyntax? usingDirective = node?.AncestorsAndSelf().OfType<UsingDirectiveSyntax>().FirstOrDefault();
            if (usingDirective != null)
            {
                SyntaxNode? newRoot = root.RemoveNode(usingDirective, SyntaxRemoveOptions.KeepNoTrivia);
                if (newRoot != null)
                {
                    Document newDoc = state.Document.WithSyntaxRoot(newRoot);
                    return state.WithNewRoot(newRoot, "Fix CS8019 (Remove unnecessary using)") with { Document = newDoc };
                }
            }
        }

        return state;
    }

    // Format (no-op placeholder). Replace with actual Formatter usage if desired.
    public static Task<FixState> FormatCode(FixState state)
    {
        // If you want formatting, call Formatter.Format with the workspace (requires Project.Workspace).
        return Task.FromResult(state);
    }
}