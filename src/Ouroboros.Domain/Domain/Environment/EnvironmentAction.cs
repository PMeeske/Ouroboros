// <copyright file="EnvironmentAction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Environment;

/// <summary>
/// Represents an action taken in an environment.
/// Immutable record capturing the action type and parameters.
/// </summary>
/// <param name="ActionType">The type/name of the action</param>
/// <param name="Parameters">Optional parameters for the action</param>
public sealed record EnvironmentAction(
    string ActionType,
    IReadOnlyDictionary<string, object>? Parameters = null);
