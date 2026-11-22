namespace PCManager.Services.Abstractions;

/// <summary>Why a path was refused, or that it is allowed.</summary>
public sealed record SafetyVerdict(bool Allowed, string Reason)
{
    public static SafetyVerdict Allow() => new(true, string.Empty);
    public static SafetyVerdict Deny(string reason) => new(false, reason);
}

/// <summary>
/// Runtime guard called before every deletion and whenever a rule root is measured.
/// Implements the allow-list (the rule's own roots), the hard deny-list, reparse-point
/// refusal and user whitelist precedence. Path only — no rule may widen it at runtime.
/// </summary>
public interface ISafetyGuard
{
    SafetyVerdict Evaluate(string path, IReadOnlyList<string> allowedRoots);

    /// <summary>
    /// Validates a rule's own root before its contents are enumerated. The root itself is
    /// never a deletion target, so the "never delete the root" refusal is skipped here.
    /// </summary>
    SafetyVerdict EvaluateRoot(string path, IReadOnlyList<string> allowedRoots);

    bool IsWhitelisted(string path);
}
