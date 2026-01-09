// <copyright file="SExpressionParser.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;

namespace Ouroboros.Core.Hyperon.Parsing;

/// <summary>
/// Parser for MeTTa-like S-expression syntax.
/// Parses strings into Atom structures.
/// </summary>
public sealed class SExpressionParser
{
    /// <summary>
    /// Parses a string containing an S-expression into an Atom.
    /// </summary>
    /// <param name="input">The input string to parse.</param>
    /// <returns>Result containing the parsed Atom or an error message.</returns>
    public Result<Atom> Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<Atom>.Failure("Input cannot be empty or whitespace");
        }

        var tokens = Tokenize(input);
        if (tokens.Count == 0)
        {
            return Result<Atom>.Failure("No tokens found in input");
        }

        var index = 0;
        try
        {
            var atom = ParseAtom(tokens, ref index);

            // Check for trailing tokens
            SkipWhitespaceTokens(tokens, ref index);
            if (index < tokens.Count)
            {
                return Result<Atom>.Failure($"Unexpected token after expression: '{tokens[index]}'");
            }

            return Result<Atom>.Success(atom);
        }
        catch (ParseException ex)
        {
            return Result<Atom>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Parses multiple S-expressions from a string.
    /// </summary>
    /// <param name="input">The input string containing one or more expressions.</param>
    /// <returns>Result containing the list of parsed Atoms or an error message.</returns>
    public Result<ImmutableList<Atom>> ParseMultiple(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<ImmutableList<Atom>>.Failure("Input cannot be empty or whitespace");
        }

        var tokens = Tokenize(input);
        if (tokens.Count == 0)
        {
            return Result<ImmutableList<Atom>>.Failure("No tokens found in input");
        }

        var atoms = ImmutableList<Atom>.Empty;
        var index = 0;

        try
        {
            while (index < tokens.Count)
            {
                SkipWhitespaceTokens(tokens, ref index);
                if (index >= tokens.Count)
                {
                    break;
                }

                var atom = ParseAtom(tokens, ref index);
                atoms = atoms.Add(atom);
            }

            return Result<ImmutableList<Atom>>.Success(atoms);
        }
        catch (ParseException ex)
        {
            return Result<ImmutableList<Atom>>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Tries to parse a string into an Atom.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <param name="atom">The resulting atom if successful.</param>
    /// <returns>True if parsing succeeded.</returns>
    public bool TryParse(string input, out Atom? atom)
    {
        var result = Parse(input);
        if (result.IsSuccess)
        {
            atom = result.Value;
            return true;
        }

        atom = null;
        return false;
    }

    private List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var escapeNext = false;

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (escapeNext)
            {
                current.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                current.Append(c);
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                current.Append(c);
                continue;
            }

            if (inString)
            {
                current.Append(c);
                continue;
            }

            switch (c)
            {
                case '(':
                case ')':
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }

                    tokens.Add(c.ToString());
                    break;

                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }

                    break;

                case ';': // Comment - skip rest of line
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }

                    while (i < input.Length && input[i] != '\n')
                    {
                        i++;
                    }

                    break;

                default:
                    current.Append(c);
                    break;
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        if (inString)
        {
            throw new ParseException("Unterminated string literal");
        }

        return tokens;
    }

    private Atom ParseAtom(List<string> tokens, ref int index)
    {
        SkipWhitespaceTokens(tokens, ref index);

        if (index >= tokens.Count)
        {
            throw new ParseException("Unexpected end of input");
        }

        var token = tokens[index];

        if (token == "(")
        {
            return ParseExpression(tokens, ref index);
        }

        if (token == ")")
        {
            throw new ParseException("Unexpected ')'");
        }

        index++;
        return ParseSymbolOrVariable(token);
    }

    private Expression ParseExpression(List<string> tokens, ref int index)
    {
        if (tokens[index] != "(")
        {
            throw new ParseException($"Expected '(' but found '{tokens[index]}'");
        }

        index++; // consume '('

        var children = ImmutableList<Atom>.Empty;

        while (index < tokens.Count)
        {
            SkipWhitespaceTokens(tokens, ref index);

            if (index >= tokens.Count)
            {
                throw new ParseException("Unexpected end of input, expected ')'");
            }

            if (tokens[index] == ")")
            {
                index++; // consume ')'
                return new Expression(children);
            }

            var child = ParseAtom(tokens, ref index);
            children = children.Add(child);
        }

        throw new ParseException("Unexpected end of input, expected ')'");
    }

    private static Atom ParseSymbolOrVariable(string token)
    {
        if (token.StartsWith("$"))
        {
            var varName = token.Substring(1);
            if (string.IsNullOrEmpty(varName))
            {
                throw new ParseException("Variable name cannot be empty after '$'");
            }

            return new Variable(varName);
        }

        // Handle quoted strings
        if (token.StartsWith("\"") && token.EndsWith("\"") && token.Length > 1)
        {
            var content = token.Substring(1, token.Length - 2);
            return new Symbol(content);
        }

        // Handle numbers (parsed as symbols for now)
        // Handle regular symbols
        return new Symbol(token);
    }

    private static void SkipWhitespaceTokens(List<string> tokens, ref int index)
    {
        // Our tokenizer already strips whitespace, so this is a no-op
        // but kept for potential future extensions
    }
}

/// <summary>
/// Exception thrown during parsing.
/// </summary>
public sealed class ParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public ParseException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public ParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
