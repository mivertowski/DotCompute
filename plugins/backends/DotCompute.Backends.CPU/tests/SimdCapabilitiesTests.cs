using Xunit;
using DotCompute.Backends.CPU.Intrinsics;

namespace DotCompute.Backends.CPU.Tests;

public class SimdCapabilitiesTests
{
    [Fact]
    public void SimdCapabilities_ReportsCorrectSupport()
    {
        // Act
        var summary = SimdCapabilities.GetSummary();

        // Assert
        Assert.NotNull(summary);
        Assert.NotNull(summary.SupportedInstructionSets);
        
        // At least some form of SIMD should be available on modern CPUs
        Assert.True(summary.PreferredVectorWidth >= 64);
        
        // Verify platform-specific expectations
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
        {
            // x86/x64 platforms should at least have SSE2
            Assert.Contains("SSE2", summary.SupportedInstructionSets);
        }
    }

    [Fact]
    public void SimdCapabilities_ToString_ReturnsReadableFormat()
    {
        // Act
        var summary = SimdCapabilities.GetSummary();
        var description = summary.ToString();

        // Assert
        Assert.NotNull(description);
        Assert.NotEmpty(description);
    }
}