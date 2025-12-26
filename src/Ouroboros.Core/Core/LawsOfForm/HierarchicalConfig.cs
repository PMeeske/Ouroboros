// <copyright file="HierarchicalConfig.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents a hierarchical configuration system with multiple inheritance levels.
/// Typical hierarchy: System → Organization → Team → User.
/// Uses TriState for proper three-valued inheritance semantics.
/// </summary>
public sealed class HierarchicalConfig
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HierarchicalConfig"/> class.
    /// </summary>
    /// <param name="systemDefault">System-wide default value.</param>
    /// <param name="organizationOverride">Organization-level override (Inherit to use system default).</param>
    /// <param name="teamOverride">Team-level override (Inherit to use org/system default).</param>
    /// <param name="userOverride">User-level override (Inherit to use team/org/system default).</param>
    public HierarchicalConfig(
        bool systemDefault,
        TriState organizationOverride = TriState.Imaginary,
        TriState teamOverride = TriState.Imaginary,
        TriState userOverride = TriState.Imaginary)
    {
        this.SystemDefault = systemDefault;
        this.OrganizationOverride = organizationOverride;
        this.TeamOverride = teamOverride;
        this.UserOverride = userOverride;
    }

    /// <summary>
    /// Gets the system-wide default value.
    /// This is the ultimate fallback for all inheritance chains.
    /// </summary>
    public bool SystemDefault { get; }

    /// <summary>
    /// Gets the organization-level override.
    /// Imaginary means use SystemDefault.
    /// </summary>
    public TriState OrganizationOverride { get; }

    /// <summary>
    /// Gets the team-level override.
    /// Imaginary means use OrganizationOverride or SystemDefault.
    /// </summary>
    public TriState TeamOverride { get; }

    /// <summary>
    /// Gets the user-level override.
    /// Imaginary means use TeamOverride, OrganizationOverride, or SystemDefault.
    /// </summary>
    public TriState UserOverride { get; }

    /// <summary>
    /// Resolves the effective value for the user level.
    /// Walks the inheritance hierarchy from most specific (User) to most general (System).
    /// </summary>
    /// <returns>The resolved boolean value.</returns>
    /// <example>
    /// var config = new HierarchicalConfig(
    ///     systemDefault: false,
    ///     organizationOverride: TriState.Mark,
    ///     teamOverride: TriState.Imaginary,
    ///     userOverride: TriState.Imaginary);
    /// config.ResolveForUser(); // returns true (inherits from Org)
    /// </example>
    public bool ResolveForUser()
    {
        return TriStateExtensions.ResolveChain(
            this.SystemDefault,
            this.UserOverride,
            this.TeamOverride,
            this.OrganizationOverride);
    }

    /// <summary>
    /// Resolves the effective value for the team level (ignoring user overrides).
    /// </summary>
    /// <returns>The resolved boolean value at team level.</returns>
    public bool ResolveForTeam()
    {
        return TriStateExtensions.ResolveChain(
            this.SystemDefault,
            this.TeamOverride,
            this.OrganizationOverride);
    }

    /// <summary>
    /// Resolves the effective value for the organization level (ignoring team and user overrides).
    /// </summary>
    /// <returns>The resolved boolean value at organization level.</returns>
    public bool ResolveForOrganization()
    {
        return this.OrganizationOverride != TriState.Imaginary
            ? this.OrganizationOverride == TriState.Mark
            : this.SystemDefault;
    }

    /// <summary>
    /// Gets the effective value at each hierarchy level.
    /// Useful for debugging configuration inheritance.
    /// </summary>
    /// <returns>A dictionary showing resolved values at each level.</returns>
    public Dictionary<string, bool> GetResolutionChain()
    {
        return new Dictionary<string, bool>
        {
            ["System"] = this.SystemDefault,
            ["Organization"] = this.ResolveForOrganization(),
            ["Team"] = this.ResolveForTeam(),
            ["User"] = this.ResolveForUser(),
        };
    }

    /// <summary>
    /// Creates a new configuration with an updated user override.
    /// </summary>
    /// <param name="newUserOverride">The new user-level override.</param>
    /// <returns>A new configuration instance with the updated value.</returns>
    public HierarchicalConfig WithUserOverride(TriState newUserOverride)
    {
        return new HierarchicalConfig(
            this.SystemDefault,
            this.OrganizationOverride,
            this.TeamOverride,
            newUserOverride);
    }

    /// <summary>
    /// Creates a new configuration with an updated team override.
    /// </summary>
    /// <param name="newTeamOverride">The new team-level override.</param>
    /// <returns>A new configuration instance with the updated value.</returns>
    public HierarchicalConfig WithTeamOverride(TriState newTeamOverride)
    {
        return new HierarchicalConfig(
            this.SystemDefault,
            this.OrganizationOverride,
            newTeamOverride,
            this.UserOverride);
    }

    /// <summary>
    /// Creates a new configuration with an updated organization override.
    /// </summary>
    /// <param name="newOrgOverride">The new organization-level override.</param>
    /// <returns>A new configuration instance with the updated value.</returns>
    public HierarchicalConfig WithOrganizationOverride(TriState newOrgOverride)
    {
        return new HierarchicalConfig(
            this.SystemDefault,
            newOrgOverride,
            this.TeamOverride,
            this.UserOverride);
    }

    /// <summary>
    /// Gets a human-readable description of the configuration state.
    /// </summary>
    /// <returns>A formatted string showing the hierarchy and resolved values.</returns>
    public override string ToString()
    {
        var chain = this.GetResolutionChain();
        var lines = new[]
        {
            $"System: {this.SystemDefault}",
            $"Organization: {this.OrganizationOverride} → {chain["Organization"]}",
            $"Team: {this.TeamOverride} → {chain["Team"]}",
            $"User: {this.UserOverride} → {chain["User"]}",
        };
        return string.Join("\n", lines);
    }
}
