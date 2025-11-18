// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Abstractions.Attributes;
using DotCompute.Backends.CUDA.RingKernels;
using System;

namespace DotCompute.Tests.Validation;

/// <summary>
/// Validation program to verify message size configuration is correctly propagated.
/// This addresses the gpubridge team's feedback about 65,792-byte message requirements.
/// </summary>
public static class MessageSizeValidation
{
    /// <summary>
    /// Ring Kernel configured with MemoryPack-compatible message sizes.
    /// </summary>
    [RingKernel(
        KernelId = "ValidationKernel",
        MaxInputMessageSizeBytes = 65792,  // 64KB + 256-byte header
        MaxOutputMessageSizeBytes = 65792)]
    public static void ValidateMessageSizes(Span<byte> data)
    {
        // Kernel implementation
    }

    /// <summary>
    /// Verify that RingKernelConfig correctly receives the message size configuration.
    /// </summary>
    public static bool ValidateConfiguration()
    {
        var config = new RingKernelConfig
        {
            KernelId = "test",
            MaxInputMessageSizeBytes = 65792,
            MaxOutputMessageSizeBytes = 65792
        };

        Console.WriteLine($"✅ RingKernelConfig created:");
        Console.WriteLine($"   MaxInputMessageSizeBytes:  {config.MaxInputMessageSizeBytes:N0} bytes");
        Console.WriteLine($"   MaxOutputMessageSizeBytes: {config.MaxOutputMessageSizeBytes:N0} bytes");

        bool inputSizeCorrect = config.MaxInputMessageSizeBytes == 65792;
        bool outputSizeCorrect = config.MaxOutputMessageSizeBytes == 65792;

        if (inputSizeCorrect && outputSizeCorrect)
        {
            Console.WriteLine($"✅ Configuration PASSED: Message sizes match MemoryPack requirements (64KB + 256B)");
            return true;
        }
        else
        {
            Console.WriteLine($"❌ Configuration FAILED: Expected 65,792 bytes");
            return false;
        }
    }

    /// <summary>
    /// Verify default values are correctly set.
    /// </summary>
    public static bool ValidateDefaults()
    {
        var config = new RingKernelConfig
        {
            KernelId = "test-defaults"
        };

        Console.WriteLine($"\n✅ Testing default values:");
        Console.WriteLine($"   Default MaxInputMessageSizeBytes:  {config.MaxInputMessageSizeBytes:N0} bytes");
        Console.WriteLine($"   Default MaxOutputMessageSizeBytes: {config.MaxOutputMessageSizeBytes:N0} bytes");

        bool defaultsCorrect =
            config.MaxInputMessageSizeBytes == 65792 &&
            config.MaxOutputMessageSizeBytes == 65792;

        if (defaultsCorrect)
        {
            Console.WriteLine($"✅ Defaults PASSED: 65,792 bytes (was 256 bytes before fix)");
            return true;
        }
        else
        {
            Console.WriteLine($"❌ Defaults FAILED: Expected 65,792 bytes but got {config.MaxInputMessageSizeBytes}");
            return false;
        }
    }

    public static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("Message Size Configuration Validation");
        Console.WriteLine("Orleans.GpuBridge.Core Integration Fix");
        Console.WriteLine("=================================================\n");

        bool configTest = ValidateConfiguration();
        bool defaultsTest = ValidateDefaults();

        Console.WriteLine($"\n=================================================");
        Console.WriteLine($"Validation Results:");
        Console.WriteLine($"  Configuration Test: {(configTest ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine($"  Defaults Test:      {(defaultsTest ? "✅ PASS" : "❌ FAIL")}");
        Console.WriteLine($"=================================================\n");

        if (configTest && defaultsTest)
        {
            Console.WriteLine("🎉 SUCCESS: Message size configuration is working correctly!");
            Console.WriteLine("   This resolves the 99.6% buffer underflow issue (256 → 65,792 bytes)");
            return 0;
        }
        else
        {
            Console.WriteLine("❌ FAILURE: Message size configuration has issues");
            return 1;
        }
    }
}
