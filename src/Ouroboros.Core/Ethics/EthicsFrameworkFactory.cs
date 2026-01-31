// <copyright file="EthicsFrameworkFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Factory for creating instances of the ethics framework.
/// Ensures consistent configuration and prevents direct instantiation.
/// </summary>
public static class EthicsFrameworkFactory
{
    /// <summary>
    /// Creates a default instance of the ethics framework with in-memory audit logging.
    /// </summary>
    /// <returns>A configured ethics framework instance.</returns>
    public static IEthicsFramework CreateDefault()
    {
        var auditLog = new InMemoryEthicsAuditLog();
        var reasoner = new BasicEthicalReasoner();
        return new ImmutableEthicsFramework(auditLog, reasoner);
    }

    /// <summary>
    /// Creates an ethics framework with a custom audit log implementation.
    /// </summary>
    /// <param name="auditLog">The audit log implementation to use.</param>
    /// <returns>A configured ethics framework instance.</returns>
    public static IEthicsFramework CreateWithAuditLog(IEthicsAuditLog auditLog)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        var reasoner = new BasicEthicalReasoner();
        return new ImmutableEthicsFramework(auditLog, reasoner);
    }

    /// <summary>
    /// Creates an ethics framework with custom audit log and reasoner implementations.
    /// </summary>
    /// <param name="auditLog">The audit log implementation to use.</param>
    /// <param name="reasoner">The ethical reasoner implementation to use.</param>
    /// <returns>A configured ethics framework instance.</returns>
    public static IEthicsFramework CreateCustom(IEthicsAuditLog auditLog, IEthicalReasoner reasoner)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(reasoner);
        return new ImmutableEthicsFramework(auditLog, reasoner);
    }
}
