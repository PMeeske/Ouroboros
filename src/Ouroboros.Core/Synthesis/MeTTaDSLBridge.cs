// <copyright file="MeTTaDSLBridge.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Synthesis;

/// <summary>
/// Bridge between program synthesis AST and MeTTa symbolic representation.
/// Allows conversion of synthesized programs to MeTTa atoms for symbolic reasoning.
/// </summary>
public static class MeTTaDSLBridge
{
    /// <summary>
    /// Converts an AST node to a MeTTa Atom for symbolic representation.
    /// </summary>
    /// <param name="node">The AST node to convert.</param>
    /// <returns>A Result containing the MeTTa Atom or an error message.</returns>
    public static Result<Atom, string> ASTToMeTTa(ASTNode node)
    {
        try
        {
            var atom = ConvertNode(node);
            return Result<Atom, string>.Success(atom);
        }
        catch (Exception ex)
        {
            return Result<Atom, string>.Failure($"Failed to convert AST to MeTTa: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts a MeTTa Atom back to an AST node.
    /// </summary>
    /// <param name="atom">The MeTTa atom to convert.</param>
    /// <returns>A Result containing the AST node or an error message.</returns>
    public static Result<ASTNode, string> MeTTaToAST(Atom atom)
    {
        try
        {
            var node = ConvertAtom(atom);
            return Result<ASTNode, string>.Success(node);
        }
        catch (Exception ex)
        {
            return Result<ASTNode, string>.Failure($"Failed to convert MeTTa to AST: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts a program to MeTTa representation for symbolic reasoning.
    /// </summary>
    /// <param name="program">The program to convert.</param>
    /// <returns>A Result containing the MeTTa Atom representation or an error message.</returns>
    public static Result<Atom, string> ProgramToMeTTa(Program program)
    {
        return ASTToMeTTa(program.AST.Root);
    }

    /// <summary>
    /// Creates a DSL primitive definition as a MeTTa atom.
    /// </summary>
    /// <param name="primitive">The primitive to convert.</param>
    /// <returns>A MeTTa atom representing the primitive definition.</returns>
    public static Atom PrimitiveToMeTTa(Primitive primitive)
    {
        // Create a MeTTa atom for primitive: (: primitiveName type)
        return Atom.Expr(
            Atom.Sym(":"),
            Atom.Sym(primitive.Name),
            Atom.Sym(primitive.Type));
    }

    /// <summary>
    /// Creates a DSL type rule as a MeTTa atom.
    /// </summary>
    /// <param name="typeRule">The type rule to convert.</param>
    /// <returns>A MeTTa atom representing the type rule.</returns>
    public static Atom TypeRuleToMeTTa(TypeRule typeRule)
    {
        // Create a MeTTa atom for type rule: (: ruleName (-> inputTypes... outputType))
        var inputTypeAtoms = typeRule.InputTypes.Select(Atom.Sym).ToList();
        var arrowExpr = Atom.Expr(ImmutableList<Atom>.Empty
            .Add(Atom.Sym("->"))
            .AddRange(inputTypeAtoms)
            .Add(Atom.Sym(typeRule.OutputType)));

        return Atom.Expr(
            Atom.Sym(":"),
            Atom.Sym(typeRule.Name),
            arrowExpr);
    }

    /// <summary>
    /// Converts an entire DSL to MeTTa atoms for symbolic reasoning.
    /// </summary>
    /// <param name="dsl">The DSL to convert.</param>
    /// <returns>A list of MeTTa atoms representing the DSL.</returns>
    public static List<Atom> DSLToMeTTa(DomainSpecificLanguage dsl)
    {
        var atoms = new List<Atom>();

        // Add primitives
        foreach (var primitive in dsl.Primitives)
        {
            atoms.Add(PrimitiveToMeTTa(primitive));
        }

        // Add type rules
        foreach (var typeRule in dsl.TypeRules)
        {
            atoms.Add(TypeRuleToMeTTa(typeRule));
        }

        return atoms;
    }

    private static Atom ConvertNode(ASTNode node)
    {
        return node.NodeType switch
        {
            "Primitive" => Atom.Sym(node.Value),
            "Variable" => Atom.Var(node.Value.TrimStart('$')),
            "Apply" => ConvertApplication(node),
            _ => Atom.Sym(node.Value),
        };
    }

    private static Atom ConvertApplication(ASTNode node)
    {
        // Convert application to S-expression
        var children = new List<Atom> { Atom.Sym(node.Value) };
        children.AddRange(node.Children.Select(ConvertNode));
        return Atom.Expr(children.ToImmutableList());
    }

    private static ASTNode ConvertAtom(Atom atom)
    {
        return atom switch
        {
            Symbol sym => new ASTNode("Primitive", sym.Name, new List<ASTNode>()),
            Variable var => new ASTNode("Variable", $"${var.Name}", new List<ASTNode>()),
            Expression expr => ConvertExpression(expr),
            _ => throw new ArgumentException($"Unknown atom type: {atom.GetType().Name}"),
        };
    }

    private static ASTNode ConvertExpression(Expression expr)
    {
        if (expr.Children.Count == 0)
        {
            return new ASTNode("Primitive", "()", new List<ASTNode>());
        }

        var head = expr.Head();
        if (head.HasValue && head.Value is Symbol sym)
        {
            var children = expr.Tail().Select(ConvertAtom).ToList();
            return new ASTNode("Apply", sym.Name, children);
        }

        // Fallback: convert all children
        var allChildren = expr.Children.Select(ConvertAtom).ToList();
        return new ASTNode("Apply", "expr", allChildren);
    }
}
