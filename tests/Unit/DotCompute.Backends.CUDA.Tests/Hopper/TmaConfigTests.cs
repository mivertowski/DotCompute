// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System;
using DotCompute.Backends.CUDA.Hopper;
using FluentAssertions;
using Xunit;

namespace DotCompute.Backends.CUDA.Tests.Hopper;

/// <summary>
/// Unit tests for <see cref="TmaConfig"/> and <see cref="TmaLayout"/> — no GPU required.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "HopperTma")]
public sealed class TmaConfigTests
{
    [Fact]
    public void CreateOneD_Valid_PassesValidation()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 4096, pipelineDepth: 2);

        cfg.MaxTransferBytes.Should().Be(4096u);
        cfg.PipelineDepth.Should().Be(2u);
        cfg.Dimensionality.Should().Be(TmaDimensionality.OneD);
        cfg.Multicast.Should().BeFalse();

        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateTwoD_Valid_PassesValidation()
    {
        var cfg = TmaConfig.CreateTwoD(maxTransferBytes: 8192, boxX: 64, boxY: 32);

        cfg.Dimensionality.Should().Be(TmaDimensionality.TwoD);
        cfg.BoxDimX.Should().Be(64u);
        cfg.BoxDimY.Should().Be(32u);

        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateThreeD_Valid_PassesValidation()
    {
        var cfg = TmaConfig.CreateThreeD(maxTransferBytes: 16384, boxX: 32, boxY: 32, boxZ: 16);

        cfg.Dimensionality.Should().Be(TmaDimensionality.ThreeD);
        cfg.BoxDimZ.Should().Be(16u);

        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MinimumAllowedSize_DoesNotThrow()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: TmaConfig.MinTransferBytes, pipelineDepth: 1);
        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_MaximumAllowedSize_DoesNotThrow()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: TmaConfig.MaxTransferBytesCap, pipelineDepth: TmaConfig.MaxPipelineDepth);
        var act = () => cfg.Validate();
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_BelowMinTransferBytes_Throws()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 8, pipelineDepth: 2);
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*at least*");
    }

    [Fact]
    public void Validate_AboveMaxTransferBytes_Throws()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: TmaConfig.MaxTransferBytesCap + 16, pipelineDepth: 2);
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*cap*");
    }

    [Theory]
    [InlineData(17u)]
    [InlineData(33u)]
    [InlineData(4097u)]
    public void Validate_NotMultipleOf16_Throws(uint size)
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: size, pipelineDepth: 2);
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*multiple*");
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(9u)]
    [InlineData(100u)]
    public void Validate_PipelineDepthOutOfRange_Throws(uint depth)
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 1024, pipelineDepth: depth);
        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*PipelineDepth*");
    }

    [Fact]
    public void Validate_NoneDimensionality_Throws()
    {
        var cfg = new TmaConfig
        {
            MaxTransferBytes = 1024,
            PipelineDepth = 2,
            Dimensionality = TmaDimensionality.None,
            BoxDimX = 64,
            BoxDimY = 1,
            BoxDimZ = 1,
        };

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*Dimensionality*");
    }

    [Fact]
    public void Validate_TwoDMissingBoxDimY_Throws()
    {
        var cfg = new TmaConfig
        {
            MaxTransferBytes = 1024,
            PipelineDepth = 2,
            Dimensionality = TmaDimensionality.TwoD,
            BoxDimX = 64,
            BoxDimY = 0,
            BoxDimZ = 1,
        };

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*BoxDimY*");
    }

    [Fact]
    public void Validate_ThreeDMissingBoxDimZ_Throws()
    {
        var cfg = new TmaConfig
        {
            MaxTransferBytes = 1024,
            PipelineDepth = 2,
            Dimensionality = TmaDimensionality.ThreeD,
            BoxDimX = 64,
            BoxDimY = 16,
            BoxDimZ = 0,
        };

        var act = () => cfg.Validate();
        act.Should().Throw<ArgumentException>().WithMessage("*BoxDimZ*");
    }

    [Fact]
    public void ComputeSmemLayout_DefaultOneD_ProducesExpectedSizes()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 4096, pipelineDepth: 2);
        var layout = TmaLayout.ComputeSmemLayout(cfg);

        // Barriers first (2 slots × 8 bytes = 16), payload aligned to 16, payload = 4096 * 2 = 8192.
        layout.BarrierOffset.Should().Be(0u);
        layout.BarrierCount.Should().Be(2u);
        layout.PayloadOffset.Should().Be(16u); // aligned-up from 16.
        layout.PayloadBytes.Should().Be(8192u);
        layout.TotalBytes.Should().Be(16u + 8192u);
    }

    [Fact]
    public void ComputeSmemLayout_SinglePipelineStage_PaddsPayloadTo16()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 1024, pipelineDepth: 1);
        var layout = TmaLayout.ComputeSmemLayout(cfg);

        // 1 slot × 8 bytes = 8, aligned up to 16 for payload.
        layout.BarrierCount.Should().Be(1u);
        layout.PayloadOffset.Should().Be(16u);
        layout.PayloadBytes.Should().Be(1024u);
        layout.TotalBytes.Should().Be(16u + 1024u);
    }

    [Fact]
    public void ComputeSmemLayout_FullPipelineMaxSize_ProducesExpectedTotal()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: TmaConfig.MaxTransferBytesCap, pipelineDepth: TmaConfig.MaxPipelineDepth);
        var layout = TmaLayout.ComputeSmemLayout(cfg);

        layout.PayloadBytes.Should().Be(TmaConfig.MaxTransferBytesCap * TmaConfig.MaxPipelineDepth);
        layout.TotalBytes.Should().BeGreaterThan(layout.PayloadBytes);
    }

    [Fact]
    public void GeneratePtxSnippet_ContainsKeyMnemonics()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 4096, pipelineDepth: 2);
        var snippet = TmaLayout.GeneratePtxSnippet(cfg);

        snippet.Should().Contain("cp.async.bulk");
        snippet.Should().Contain("mbarrier");
        snippet.Should().Contain("4096");
        snippet.Should().Contain("OneD");
        snippet.Should().Contain("Unicast path");
    }

    [Fact]
    public void GeneratePtxSnippet_Multicast_ReflectsFlag()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 4096, pipelineDepth: 2, multicast: true);
        var snippet = TmaLayout.GeneratePtxSnippet(cfg);

        snippet.Should().Contain("Multicast path");
    }

    [Fact]
    public void ComputeSmemLayout_InvalidConfig_Throws()
    {
        var cfg = TmaConfig.CreateOneD(maxTransferBytes: 8, pipelineDepth: 2); // < 16 bytes.
        var act = () => _ = TmaLayout.ComputeSmemLayout(cfg);
        act.Should().Throw<ArgumentException>();
    }
}
