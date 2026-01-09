// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Abstractions.Security;

/// <summary>
/// Defines an access control policy for device authorization.
/// </summary>
/// <remarks>
/// <para>
/// Access policies evaluate whether a principal should be granted access to a device.
/// Policies can be combined using logical operators (AND, OR, NOT) and can
/// consider various factors:
/// </para>
/// <list type="bullet">
/// <item><description>Principal identity and roles</description></item>
/// <item><description>Device type and capabilities</description></item>
/// <item><description>Time of day and schedule</description></item>
/// <item><description>Current resource utilization</description></item>
/// <item><description>Request context and metadata</description></item>
/// </list>
/// </remarks>
public interface IAccessPolicy
{
    /// <summary>Gets the unique policy identifier.</summary>
    string Id { get; }

    /// <summary>Gets the policy name.</summary>
    string Name { get; }

    /// <summary>Gets the policy description.</summary>
    string? Description { get; }

    /// <summary>Gets the policy priority (higher = evaluated first).</summary>
    int Priority { get; }

    /// <summary>Gets whether this policy is currently enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Evaluates the policy for a given access request.
    /// </summary>
    /// <param name="context">The policy evaluation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The policy evaluation result.</returns>
    ValueTask<PolicyEvaluationResult> EvaluateAsync(
        PolicyEvaluationContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Context for policy evaluation.
/// </summary>
public sealed class PolicyEvaluationContext
{
    /// <summary>Gets the principal requesting access.</summary>
    public required ISecurityPrincipal Principal { get; init; }

    /// <summary>Gets the target device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the requested access type.</summary>
    public required DeviceAccessType AccessType { get; init; }

    /// <summary>Gets the access request (if available).</summary>
    public DeviceAccessRequest? Request { get; init; }

    /// <summary>Gets the current time for time-based policies.</summary>
    public DateTimeOffset CurrentTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets device information for capability-based policies.</summary>
    public DeviceInfo? DeviceInfo { get; init; }

    /// <summary>Gets current resource usage for quota-based policies.</summary>
    public ResourceUsage? CurrentUsage { get; init; }

    /// <summary>Gets additional context attributes.</summary>
    public IReadOnlyDictionary<string, object> Attributes { get; init; } =
        new Dictionary<string, object>();
}

/// <summary>
/// Basic device information for policy evaluation.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>Gets the device identifier.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Gets the device type.</summary>
    public required string DeviceType { get; init; }

    /// <summary>Gets the device name.</summary>
    public string? Name { get; init; }

    /// <summary>Gets the device vendor.</summary>
    public string? Vendor { get; init; }

    /// <summary>Gets the device capabilities/features.</summary>
    public IReadOnlySet<string> Capabilities { get; init; } = new HashSet<string>();

    /// <summary>Gets device metadata.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Result of a policy evaluation.
/// </summary>
public sealed class PolicyEvaluationResult
{
    /// <summary>Gets the effect of the policy evaluation.</summary>
    public required PolicyEffect Effect { get; init; }

    /// <summary>Gets the reason for the decision.</summary>
    public required string Reason { get; init; }

    /// <summary>Gets the policy that produced this result.</summary>
    public required string PolicyId { get; init; }

    /// <summary>Gets any conditions that must be met.</summary>
    public IReadOnlyList<PolicyCondition> Conditions { get; init; } = [];

    /// <summary>Gets recommended resource limits if access is granted.</summary>
    public ResourceLimits? RecommendedLimits { get; init; }

    /// <summary>Creates an allow result.</summary>
    public static PolicyEvaluationResult Allow(string policyId, string reason)
        => new() { Effect = PolicyEffect.Allow, PolicyId = policyId, Reason = reason };

    /// <summary>Creates a deny result.</summary>
    public static PolicyEvaluationResult Deny(string policyId, string reason)
        => new() { Effect = PolicyEffect.Deny, PolicyId = policyId, Reason = reason };

    /// <summary>Creates a not applicable result (policy doesn't apply).</summary>
    public static PolicyEvaluationResult NotApplicable(string policyId)
        => new() { Effect = PolicyEffect.NotApplicable, PolicyId = policyId, Reason = "Policy does not apply" };
}

/// <summary>
/// The effect of a policy evaluation.
/// </summary>
public enum PolicyEffect
{
    /// <summary>Access is explicitly allowed.</summary>
    Allow,

    /// <summary>Access is explicitly denied.</summary>
    Deny,

    /// <summary>Policy does not apply to this request.</summary>
    NotApplicable
}

/// <summary>
/// A condition that must be met for policy compliance.
/// </summary>
public sealed class PolicyCondition
{
    /// <summary>Gets the condition identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the condition description.</summary>
    public required string Description { get; init; }

    /// <summary>Gets the condition type.</summary>
    public required ConditionType Type { get; init; }

    /// <summary>Gets condition parameters.</summary>
    public IReadOnlyDictionary<string, object>? Parameters { get; init; }
}

/// <summary>
/// Types of policy conditions.
/// </summary>
public enum ConditionType
{
    /// <summary>Must agree to terms of use.</summary>
    AcceptTerms,

    /// <summary>Must complete multi-factor authentication.</summary>
    RequireMfa,

    /// <summary>Must be within allowed time window.</summary>
    TimeRestriction,

    /// <summary>Must be from allowed network/location.</summary>
    NetworkRestriction,

    /// <summary>Must complete a review/approval process.</summary>
    RequireApproval,

    /// <summary>Custom condition type.</summary>
    Custom
}

/// <summary>
/// Provides access to registered policies.
/// </summary>
public interface IAccessPolicyProvider
{
    /// <summary>
    /// Gets all enabled policies, ordered by priority.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ordered list of enabled policies.</returns>
    ValueTask<IReadOnlyList<IAccessPolicy>> GetPoliciesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific policy by ID.
    /// </summary>
    /// <param name="policyId">The policy identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The policy, or null if not found.</returns>
    ValueTask<IAccessPolicy?> GetPolicyAsync(
        string policyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a new policy.
    /// </summary>
    /// <param name="policy">The policy to register.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RegisterPolicyAsync(
        IAccessPolicy policy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a policy.
    /// </summary>
    /// <param name="policyId">The policy identifier to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask UnregisterPolicyAsync(
        string policyId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Evaluates multiple policies to produce a final access decision.
/// </summary>
public interface IPolicyEvaluator
{
    /// <summary>
    /// Evaluates all applicable policies and produces a final decision.
    /// </summary>
    /// <param name="context">The evaluation context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated access decision.</returns>
    ValueTask<AccessDecision> EvaluateAsync(
        PolicyEvaluationContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the combining algorithm used for policy decisions.
    /// </summary>
    PolicyCombiningAlgorithm CombiningAlgorithm { get; }
}

/// <summary>
/// Algorithms for combining multiple policy decisions.
/// </summary>
public enum PolicyCombiningAlgorithm
{
    /// <summary>Deny if any policy denies (deny-overrides).</summary>
    DenyOverrides,

    /// <summary>Allow if any policy allows (permit-overrides).</summary>
    PermitOverrides,

    /// <summary>First applicable policy wins.</summary>
    FirstApplicable,

    /// <summary>All policies must allow (unanimous permit).</summary>
    UnanimousPermit,

    /// <summary>Highest priority policy wins.</summary>
    HighestPriority
}
