#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.Roslynator.Pipeline.Steps;

/// <summary>
/// Provides standard code fix implementations for common Roslyn analyzer diagnostics.
/// All methods follow the <c>Func&lt;FixState, Task&lt;FixState&gt;&gt;</c> signature for composability.
/// </summary>
public static class StandardFixes
{
    #region Helper Methods

    /// <summary>
    /// Determines whether the given state should be skipped (invalid or null).
    /// </summary>
    /// <param name="state">The fix state to check.</param>
    /// <returns><c>true</c> if the state is invalid and should be skipped; otherwise, <c>false</c>.</returns>
    public static bool ShouldSkip(FixState state) =>
        state is null || !state.IsValid;

    /// <summary>
    /// Safely finds a syntax node at the specified text span.
    /// </summary>
    /// <param name="root">The root syntax node to search within.</param>
    /// <param name="span">The text span to locate.</param>
    /// <returns>The syntax node at the span, or <c>null</c> if not found.</returns>
    public static SyntaxNode? FindNodeSafe(SyntaxNode root, TextSpan span)
    {
        if (root is null)
        {
            return null;
        }

        try
        {
            return root.FindNode(span, getInnermostNodeForTie: true);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Replaces a syntax node in the current root and returns an updated FixState.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <param name="oldNode">The node to replace.</param>
    /// <param name="newNode">The replacement node.</param>
    /// <param name="description">A description of the replacement (for logging/debugging).</param>
    /// <returns>A task containing the updated FixState.</returns>
    /// <exception cref="ArgumentNullException">Thrown when state, oldNode, or newNode is null.</exception>
    public static Task<FixState> ReplaceNode(FixState state, SyntaxNode oldNode, SyntaxNode newNode, string description)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(oldNode);
        ArgumentNullException.ThrowIfNull(newNode);

        SyntaxNode newRoot = state.CurrentRoot.ReplaceNode(oldNode, newNode);
        return Task.FromResult(state.WithRoot(newRoot));
    }

    #endregion

    #region Fix Methods

    /// <summary>
    /// Simplifies LINQ expressions by combining Where().First() into First() with predicate.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> SimplifyLinq(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        InvocationExpressionSyntax? invoke = FindNodeSafe(state.CurrentRoot, state.Diagnostic.Location.SourceSpan)?
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();

        if (invoke?.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Expression is InvocationExpressionSyntax previousInvocation &&
            previousInvocation.Expression is MemberAccessExpressionSyntax previousMember &&
            previousMember.Name.Identifier.Text == "Where" &&
            memberAccess.Name.Identifier.Text == "First")
        {
            ArgumentSyntax? predicate = previousInvocation.ArgumentList.Arguments.FirstOrDefault();
            if (predicate is not null)
            {
                InvocationExpressionSyntax simplifiedCall = SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        previousMember.Expression,
                        SyntaxFactory.IdentifierName("First")),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(predicate)));

                return ReplaceNode(state, invoke, simplifiedCall, "Simplify LINQ");
            }
        }

        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts array/list creation to C# 12 collection expressions.
    /// Placeholder implementation - complete implementation requires semantic analysis.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseCollectionExpression(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        // Placeholder for C# 12 [..] syntax as complete implementation requires semantic analysis
        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts explicit collection initialization to collection initializer syntax.
    /// Placeholder implementation - complete implementation requires complex statement analysis.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseCollectionInitializer(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        // Placeholder as complete implementation requires complex statement analysis
        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts var declarations to explicit type declarations.
    /// Placeholder implementation - complete implementation requires semantic model to resolve type.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseExplicitType(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        // Placeholder as complete implementation requires semantic model to resolve type
        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts array length-based indexing to C# 8 index operator (^1).
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseIndexOperator(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        ElementAccessExpressionSyntax? elementAccess = FindNodeSafe(state.CurrentRoot, state.Diagnostic.Location.SourceSpan)?
            .FirstAncestorOrSelf<ElementAccessExpressionSyntax>();

        // Convert array[array.Length - 1] to array[^1]
        if (elementAccess?.ArgumentList.Arguments.FirstOrDefault()?.Expression is BinaryExpressionSyntax binary &&
            binary.IsKind(SyntaxKind.SubtractExpression) &&
            binary.Right is LiteralExpressionSyntax literal &&
            literal.Token.ValueText == "1")
        {
            PrefixUnaryExpressionSyntax indexExpr = SyntaxFactory.PrefixUnaryExpression(
                SyntaxKind.IndexExpression,
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(1)));

            ElementAccessExpressionSyntax newAccess = elementAccess.WithArgumentList(
                SyntaxFactory.BracketedArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(indexExpr))));

            return ReplaceNode(state, elementAccess, newAccess, "Use Index Operator");
        }

        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts conditional null checks to null propagation operator (?.).
    /// Placeholder implementation - complete implementation involves complex conditional logic analysis.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseNullPropagation(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        // Placeholder as complete implementation involves complex conditional logic analysis
        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts property assignments after construction to object initializer syntax.
    /// Placeholder implementation - complete implementation requires analyzing subsequent assignment statements.
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseObjectInitializer(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        // Placeholder as complete implementation requires analyzing subsequent assignment statements
        return Task.FromResult(state);
    }

    /// <summary>
    /// Converts Substring calls to C# 8 range operator ([start..]).
    /// </summary>
    /// <param name="state">The current fix state.</param>
    /// <returns>A task containing the updated FixState.</returns>
    public static Task<FixState> UseRangeOperator(FixState state)
    {
        if (ShouldSkip(state))
        {
            return Task.FromResult(state);
        }

        InvocationExpressionSyntax? invocation = FindNodeSafe(state.CurrentRoot, state.Diagnostic.Location.SourceSpan)?
            .FirstAncestorOrSelf<InvocationExpressionSyntax>();

        // Convert Substring(start) to [start..]
        if (invocation?.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "Substring" &&
            invocation.ArgumentList.Arguments.Count == 1)
        {
            ExpressionSyntax startArg = invocation.ArgumentList.Arguments[0].Expression;

            RangeExpressionSyntax rangeExpr = SyntaxFactory.RangeExpression(startArg, null);

            ElementAccessExpressionSyntax elementAccess = SyntaxFactory.ElementAccessExpression(
                memberAccess.Expression,
                SyntaxFactory.BracketedArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(rangeExpr))));

            return ReplaceNode(state, invocation, elementAccess, "Use Range Operator");
        }

        return Task.FromResult(state);
    }

    #endregion
}
