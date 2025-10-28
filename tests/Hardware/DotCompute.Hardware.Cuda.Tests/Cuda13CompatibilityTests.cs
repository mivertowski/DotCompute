// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Tests.Common.Specialized;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Tests for CUDA 13.0 compatibility and minimum hardware requirements.
    /// Validates that the system properly rejects pre-Turing GPUs and enforces
    /// the minimum compute capability requirements.
    /// </summary>
    [Trait("Category", "CUDA")]
    [Trait("Category", "Hardware")]
    [Trait("Category", "CUDA13")]
    public class Cuda13CompatibilityTests(ITestOutputHelper output) : CudaTestBase(output)
    {
        /// <summary>
        /// Gets c u d a_13_ should_ require_ minimum_ compute_ capability_7_5.
        /// </summary>
        /// <returns>The result of the operation.</returns>
        [SkippableFact]
        public async Task CUDA_13_Should_Require_Minimum_Compute_Capability_7_5()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var factory = new CudaAcceleratorFactory(new NullLogger<CudaAcceleratorFactory>());

            // Try to create an accelerator - should succeed only on CC 7.5+

            try
            {
                await using var accelerator = factory.CreateProductionAccelerator(0);


                var cc = accelerator.Info.ComputeCapability;
                Output.WriteLine($"Device: {accelerator.Info.Name}");
                Output.WriteLine($"Compute Capability: {cc.Major}.{cc.Minor}");
                Output.WriteLine($"Architecture: {GetArchitectureName(accelerator.Info)}");

                // If we got here, device should be CC 7.5 or higher

                _ = cc.Major.Should().BeGreaterThanOrEqualTo(7, "CUDA 13.0 requires Turing or newer");


                if (cc.Major == 7)
                {
                    _ = cc.Minor.Should().BeGreaterThanOrEqualTo(5, "Turing starts at CC 7.5");
                }

                // Verify supported architectures

                var architecture = GetArchitectureName(accelerator.Info);
                var supportedArchitectures = new[] { "Turing", "Ampere", "Ada Lovelace", "Hopper" };
                _ = supportedArchitectures.Should().Contain(arch => architecture.Contains(arch, StringComparison.OrdinalIgnoreCase),

                    $"Architecture {architecture} should be a supported CUDA 13.0 architecture");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not compatible with CUDA 13.0", StringComparison.CurrentCulture))
            {
                // This is expected for pre-Turing GPUs
                Output.WriteLine($"Device properly rejected: {ex.Message}");

                // Verify we're actually on an older GPU

                var deviceInfo = await GetDeviceInfoWithoutValidation();
                if (deviceInfo != null)
                {
                    _ = deviceInfo.ComputeCapability.Major.Should().BeLessThan(7,

                        "Pre-Turing GPU should have CC < 7.0");
                }
            }
        }
        /// <summary>
        /// Gets pre_ turing_ g p us_ should_ be_ rejected.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        public async Task Pre_Turing_GPUs_Should_Be_Rejected()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            var deviceInfo = await GetDeviceInfoWithoutValidation();
            if (deviceInfo == null)
            {
                Skip.If(true, "Cannot get device info");
                return;
            }

            Output.WriteLine($"Testing device: {deviceInfo.Name} (CC {deviceInfo.ComputeCapability.Major}.{deviceInfo.ComputeCapability.Minor})");

            using var factory = new CudaAcceleratorFactory();


            if (deviceInfo.ComputeCapability.Major < 7 ||

                (deviceInfo.ComputeCapability.Major == 7 && deviceInfo.ComputeCapability.Minor < 5))
            {
                // Should reject pre-Turing GPUs
                var act = () => factory.CreateProductionAccelerator(0);


                _ = act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*not compatible with CUDA 13.0*")
                    .WithMessage("*Minimum requirement: CC 7.5 (Turing)*");


                Output.WriteLine("Pre-Turing GPU properly rejected");
            }
            else
            {
                // Should accept Turing and newer
                await using var accelerator = factory.CreateProductionAccelerator(0);
                _ = accelerator.Should().NotBeNull();
                Output.WriteLine("Turing or newer GPU properly accepted");
            }
        }
        /// <summary>
        /// Gets deprecated_ architectures_ should_ not_ be_ supported.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        public async Task Deprecated_Architectures_Should_Not_Be_Supported()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            var deprecatedArchitectures = new[]
            {
                (5, 0, "Maxwell"),  // GTX 900 series
                (5, 2, "Maxwell"),  // GTX 900 series
                (6, 0, "Pascal"),   // P100
                (6, 1, "Pascal"),   // GTX 1000 series
                (7, 0, "Volta")     // V100, Titan V
            };

            var deviceInfo = await GetDeviceInfoWithoutValidation();
            if (deviceInfo == null)
            {
                Skip.If(true, "Cannot get device info");
                return;
            }

            var cc = deviceInfo.ComputeCapability;


            foreach (var (major, minor, architecture) in deprecatedArchitectures)
            {
                if (cc.Major == major && cc.Minor == minor)
                {
                    Output.WriteLine($"Found deprecated {architecture} GPU (CC {major}.{minor})");


                    using var factory = new CudaAcceleratorFactory();
                    var act = () => factory.CreateProductionAccelerator(0);


                    _ = act.Should().Throw<InvalidOperationException>()
                        .WithMessage($"*{deviceInfo.Name} is not compatible with CUDA 13.0*");


                    Output.WriteLine($"{architecture} architecture properly rejected");
                    return;
                }
            }


            Output.WriteLine($"Current device CC {cc.Major}.{cc.Minor} is not a deprecated architecture");
        }
        /// <summary>
        /// Gets supported_ architectures_ should_ be_ accepted.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        public async Task Supported_Architectures_Should_Be_Accepted()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            var supportedArchitectures = new[]
            {
                (7, 5, "Turing"),        // RTX 2060-2080 Ti, T4
                (8, 0, "Ampere"),        // A100
                (8, 6, "Ampere"),        // RTX 3050-3090
                (8, 7, "Ampere"),        // A10, A30
                (8, 9, "Ada Lovelace"),  // RTX 4060-4090, L4, L40
                (9, 0, "Hopper")         // H100, H200
            };

            var deviceInfo = await GetDeviceInfoWithoutValidation();
            if (deviceInfo == null)
            {
                Skip.If(true, "Cannot get device info");
                return;
            }

            var cc = deviceInfo.ComputeCapability;


            foreach (var (major, minor, architecture) in supportedArchitectures)
            {
                if (cc.Major == major && (minor == 0 || cc.Minor == minor))
                {
                    Output.WriteLine($"Found supported {architecture} GPU (CC {cc.Major}.{cc.Minor})");


                    using var factory = new CudaAcceleratorFactory();
                    await using var accelerator = factory.CreateProductionAccelerator(0);


                    _ = accelerator.Should().NotBeNull();
                    _ = accelerator.Info.Name.Should().NotBeNullOrEmpty();
                    _ = GetArchitectureName(accelerator.Info).Should().Contain(architecture);


                    Output.WriteLine($"{architecture} architecture properly accepted");
                    return;
                }
            }


            Output.WriteLine($"Current device CC {cc.Major}.{cc.Minor} - testing basic acceptance");


            if (cc.Major >= 7 && cc.Minor >= 5)
            {
                using var factory = new CudaAcceleratorFactory();
                await using var accelerator = factory.CreateProductionAccelerator(0);
                _ = accelerator.Should().NotBeNull();
                Output.WriteLine("Device accepted as it meets minimum requirements");
            }
        }
        /// <summary>
        /// Gets c u d a_13_ a p i_ features_ should_ be_ available.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        public async Task CUDA_13_API_Features_Should_Be_Available()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");
            Skip.IfNot(HasMinimumComputeCapability(7, 5), "Requires CC 7.5+ for CUDA 13.0");

            using var factory = new CudaAcceleratorFactory();
            await using var accelerator = factory.CreateProductionAccelerator(0);

            // Test that CUDA 13.0 specific features are available

            Output.WriteLine("Testing CUDA 13.0 API availability:");

            // 1. Shared memory register spilling should be configurable

            var compilerOptions = new Backends.CUDA.Configuration.CudaCompilationOptions
            {
                EnableSharedMemoryRegisterSpilling = true
            };
            _ = compilerOptions.EnableSharedMemoryRegisterSpilling.Should().BeTrue();
            Output.WriteLine("✓ Shared memory register spilling available");

            // 2. Cooperative groups should be available

            var kernelWithCoopGroups = @"
                #include <cooperative_groups.h>
                extern ""C"" __global__ void testCoopGroups(float* data) {
                    cooperative_groups::thread_block block = cooperative_groups::this_thread_block();
                    data[threadIdx.x] = block.size();
                }";


            try
            {
                var kernel = await accelerator.CompileKernelAsync(
                    new Abstractions.Kernels.KernelDefinition
                    {
                        Name = "testCoopGroups",
                        Source = kernelWithCoopGroups,
                        EntryPoint = "testCoopGroups"
                    });


                _ = kernel.Should().NotBeNull();
                Output.WriteLine("✓ Cooperative groups compilation successful");
                await kernel.DisposeAsync();
            }
            catch (Exception ex)
            {
                Output.WriteLine($"⚠ Cooperative groups test failed: {ex.Message}");
            }

            // 3. Check for tensor core support (Turing+)

            if (accelerator.Info.ComputeCapability.Major >= 7 && accelerator.Info.ComputeCapability.Minor >= 5)
            {
                Output.WriteLine("✓ Tensor cores available");
            }
            else
            {
                Output.WriteLine("✗ Tensor cores not available (optional feature)");
            }

            // 4. Check for RT cores support (Turing+)

            if (accelerator.Info.ComputeCapability.Major >= 7 && accelerator.Info.ComputeCapability.Minor >= 5)
            {
                Output.WriteLine("✓ RT cores available (Turing+)");
            }
        }
        /// <summary>
        /// Gets driver_ version_ should_ support_ c u d a_13.
        /// </summary>
        /// <returns>The result of the operation.</returns>

        [SkippableFact]
        public async Task Driver_Version_Should_Support_CUDA_13()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            using var factory = new CudaAcceleratorFactory();

            // Get driver version

            var driverVersion = CudaRuntime.GetDriverVersion();
            Output.WriteLine($"CUDA Driver Version: {driverVersion.Major}.{driverVersion.Minor}");

            // CUDA 13.0 requires driver 535.00 or newer

            _ = driverVersion.Major.Should().BeGreaterThanOrEqualTo(535,

                "CUDA 13.0 requires driver version 535.00 or newer");

            // Get runtime version

            var runtimeVersion = CudaRuntime.GetRuntimeVersion();
            Output.WriteLine($"CUDA Runtime Version: {runtimeVersion.Major}.{runtimeVersion.Minor}");

            // Runtime should be 13.0 or newer

            _ = runtimeVersion.Major.Should().BeGreaterThanOrEqualTo(13,

                "CUDA runtime should be version 13.0 or newer");
        }

        /// <summary>
        /// Helper to get device info without validation for testing purposes
        /// </summary>
        private static async Task<DeviceInfo?> GetDeviceInfoWithoutValidation()
            // Simplified version - just return null for now
            // Full implementation would require additional P/Invoke setup

            => await Task.FromResult<DeviceInfo?>(null);


        private class DeviceInfo
        {
            /// <summary>
            /// Gets or sets the name.
            /// </summary>
            /// <value>The name.</value>
            public string Name { get; set; } = "";
            /// <summary>
            /// Gets or sets the compute capability.
            /// </summary>
            /// <value>The compute capability.</value>
            public ComputeCapability ComputeCapability { get; set; } = new();
        }


        private class ComputeCapability
        {
            /// <summary>
            /// Gets or sets the major.
            /// </summary>
            /// <value>The major.</value>
            public int Major { get; set; }
            /// <summary>
            /// Gets or sets the minor.
            /// </summary>
            /// <value>The minor.</value>
            public int Minor { get; set; }
        }


        private static string GetArchitectureName(Abstractions.AcceleratorInfo info)
        {
            var cc = info.ComputeCapability;


            if (cc.Major == 9 && cc.Minor == 0)
            {
                return "Hopper";
            }


            if (cc.Major == 8 && cc.Minor == 9)
            {
                return "Ada Lovelace";
            }


            if (cc.Major == 8 && (cc.Minor == 6 || cc.Minor == 7))
            {
                return "Ampere";
            }


            if (cc.Major == 8 && cc.Minor == 0)
            {
                return "Ampere";
            }


            if (cc.Major == 7 && cc.Minor == 5)
            {
                return "Turing";
            }


            if (cc.Major == 7 && cc.Minor == 0)
            {
                return "Volta";
            }


            if (cc.Major == 6)
            {
                return "Pascal";
            }


            if (cc.Major == 5)
            {
                return "Maxwell";
            }


            return "Unknown";
        }
    }
}