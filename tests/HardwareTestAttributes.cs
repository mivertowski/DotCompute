using Xunit;
using DotCompute.Tests.Common;

namespace DotCompute.Tests.Attributes;

/// <summary>
/// Test attribute that skips hardware tests when hardware is not available
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HardwareFactAttribute : FactAttribute
{
    public HardwareFactAttribute(string hardwareType)
    {
        HardwareType = hardwareType;
        
        var isAvailable = hardwareType.ToLowerInvariant() switch
        {
            "cuda" => HardwareDetection.IsCudaAvailable(),
            "opencl" => HardwareDetection.IsOpenCLAvailable(),
            "directcompute" => HardwareDetection.IsDirectComputeAvailable(),
            "metal" => HardwareDetection.IsMetalAvailable(),
            _ => false
        };

        if (!isAvailable || HardwareDetection.IsCI())
        {
            Skip = HardwareDetection.GetUnavailabilityReason(hardwareType);
        }
    }

    public string HardwareType { get; }
}

/// <summary>
/// Test attribute that skips hardware tests when hardware is not available (for Theory tests)
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HardwareTheoryAttribute : TheoryAttribute
{
    public HardwareTheoryAttribute(string hardwareType)
    {
        HardwareType = hardwareType;
        
        var isAvailable = hardwareType.ToLowerInvariant() switch
        {
            "cuda" => HardwareDetection.IsCudaAvailable(),
            "opencl" => HardwareDetection.IsOpenCLAvailable(),
            "directcompute" => HardwareDetection.IsDirectComputeAvailable(),
            "metal" => HardwareDetection.IsMetalAvailable(),
            _ => false
        };

        if (!isAvailable || HardwareDetection.IsCI())
        {
            Skip = HardwareDetection.GetUnavailabilityReason(hardwareType);
        }
    }

    public string HardwareType { get; }
}

/// <summary>
/// Conditional skip attribute for xUnit tests
/// </summary>
public sealed class ConditionalFactAttribute : FactAttribute
{
    public ConditionalFactAttribute(string condition)
    {
        var shouldSkip = condition.ToLowerInvariant() switch
        {
            "ci" => HardwareDetection.IsCI(),
            "!ci" => !HardwareDetection.IsCI(),
            "cuda" => !HardwareDetection.IsCudaAvailable(),
            "opencl" => !HardwareDetection.IsOpenCLAvailable(),
            "directcompute" => !HardwareDetection.IsDirectComputeAvailable(),
            "metal" => !HardwareDetection.IsMetalAvailable(),
            _ => false
        };

        if (shouldSkip)
        {
            Skip = $"Condition not met: {condition}";
        }
    }
}

/// <summary>
/// Performance test attribute - skipped in CI by default
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute(bool runInCI = false)
    {
        if (HardwareDetection.IsCI() && !runInCI)
        {
            Skip = "Performance tests are skipped in CI by default";
        }
    }
}

/// <summary>
/// Stress test attribute - skipped in CI by default
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class StressTestAttribute : FactAttribute
{
    public StressTestAttribute()
    {
        if (HardwareDetection.IsCI())
        {
            Skip = "Stress tests are skipped in CI environments";
        }
    }
}