using DotCompute.Backends.CUDA.Factory;
using DotCompute.Backends.CUDA.Initialization;
using DotCompute.Backends.CUDA.Native;
using DotCompute.Backends.CUDA.Types.Native;
using DotCompute.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace DotCompute.Hardware.Cuda.Tests
{
    /// <summary>
    /// Diagnostic tests to understand CUDA device property issues.
    /// These tests help identify why ManagedMemory is not being detected correctly.
    /// </summary>
    [Trait("Category", "Diagnostic")]
    public class CudaDiagnosticTests : ConsolidatedTestBase
    {
        public CudaDiagnosticTests(ITestOutputHelper output) : base(output) { }

        [SkippableFact]
        public void Diagnose_Device_Properties_Direct()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");


            Output.WriteLine("=== Direct Device Properties Test ===");

            // Ensure CUDA is properly initialized first

            if (!CudaInitializer.EnsureInitialized())
            {
                Output.WriteLine($"CUDA initialization failed: {CudaInitializer.InitializationErrorMessage}");
                Skip.If(true, $"CUDA initialization failed: {CudaInitializer.InitializationErrorMessage}");
            }


            Output.WriteLine("CUDA runtime initialized successfully");

            // Test 1: Direct P/Invoke

            var props = new CudaDeviceProperties();
            var result = CudaRuntime.cudaGetDeviceProperties(ref props, 0);


            Output.WriteLine($"cudaGetDeviceProperties result: {result}");
            Output.WriteLine($"Device Name: {props.DeviceName}");
            Output.WriteLine($"Compute Capability: {props.Major}.{props.Minor}");
            Output.WriteLine($"Total Global Memory: {props.TotalGlobalMem:N0} bytes");
            Output.WriteLine($"ManagedMemory field value: {props.ManagedMemory}");
            Output.WriteLine($"UnifiedAddressing: {props.UnifiedAddressing}");
            Output.WriteLine($"ConcurrentManagedAccess: {props.ConcurrentManagedAccess}");
            Output.WriteLine($"PageableMemoryAccess: {props.PageableMemoryAccess}");

            // Test 2: Check raw memory at offset

            unsafe
            {
                Output.WriteLine("\n=== Raw Memory Check ===");
                var ptr = &props;
                var bytes = (byte*)ptr;
                var intAt652 = (int*)(bytes + 652);
                Output.WriteLine($"Raw int value at offset 652: {*intAt652}");

                // Check surrounding bytes

                Output.WriteLine("Bytes around offset 652:");
                for (var i = 648; i < 660; i += 4)
                {
                    var val = (int*)(bytes + i);
                    Output.WriteLine($"  Offset {i}: {*val}");
                }
            }

            // Assertions

            Assert.Equal(CudaError.Success, result);

            // CRITICAL: This assertion should pass if the GPU supports unified memory
            // The RTX 2000 Ada Generation definitely supports unified memory

            Output.WriteLine($"\n=== FINAL CHECK ===");
            Output.WriteLine($"Raw ManagedMemory value: {props.ManagedMemory}");
            Output.WriteLine($"Corrected ManagedMemorySupported: {props.ManagedMemorySupported}");
            Output.WriteLine($"Test will {(props.ManagedMemorySupported ? "PASS" : "FAIL")}");

            // Use the corrected method that handles known device issues

            Assert.True(props.ManagedMemorySupported, $"ManagedMemory should be supported. Raw value: {props.ManagedMemory}, Compute Capability: {props.Major}.{props.Minor}");
        }


        [SkippableFact]
        public void Diagnose_Device_Through_CudaDevice()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Ensure CUDA is initialized

            CudaInitializer.EnsureInitialized();


            Output.WriteLine("=== CudaDevice Class Test ===");


            var device = new DotCompute.Backends.CUDA.CudaDevice(0);


            Output.WriteLine($"Device Name: {device.Name}");
            Output.WriteLine($"Compute Capability: {device.ComputeCapability}");
            Output.WriteLine($"SupportsManagedMemory: {device.SupportsManagedMemory}");
            Output.WriteLine($"SupportsUnifiedAddressing: {device.SupportsUnifiedAddressing}");

            // Check the raw properties

            var propsField = typeof(DotCompute.Backends.CUDA.CudaDevice)
                .GetField("_deviceProperties", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);


            if (propsField != null)
            {
                var props = (CudaDeviceProperties)propsField.GetValue(device)!;
                Output.WriteLine($"\n=== Internal Properties ===");
                Output.WriteLine($"_deviceProperties.ManagedMemory: {props.ManagedMemory}");
                Output.WriteLine($"_deviceProperties.UnifiedAddressing: {props.UnifiedAddressing}");
            }


            Assert.True(device.SupportsManagedMemory, "Device should support managed memory");
        }


        [SkippableFact]
        public async Task Diagnose_Accelerator_Creation()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Ensure CUDA is initialized

            CudaInitializer.EnsureInitialized();


            Output.WriteLine("=== Accelerator Creation Test ===");


            using var factory = new CudaAcceleratorFactory();
            var accelerator = factory.CreateProductionAccelerator(0);


            Assert.NotNull(accelerator);


            Output.WriteLine($"Accelerator Type: {accelerator!.GetType().FullName}");
            Output.WriteLine($"Accelerator Info: {accelerator.Info.Name}");
            Output.WriteLine($"IsUnifiedMemory: {accelerator.Info.IsUnifiedMemory}");

            // Check capabilities dictionary

            if (accelerator.Info.Capabilities != null)
            {
                Output.WriteLine("\n=== Capabilities ===");
                foreach (var cap in accelerator.Info.Capabilities)
                {
                    if (cap.Key.Contains("Memory", StringComparison.OrdinalIgnoreCase) ||

                        cap.Key.Contains("Unified", StringComparison.OrdinalIgnoreCase) ||
                        cap.Key.Contains("Managed", StringComparison.OrdinalIgnoreCase))
                    {
                        Output.WriteLine($"{cap.Key}: {cap.Value}");
                    }
                }
            }

            // Check memory manager type

            Output.WriteLine($"\n=== Memory Manager ===");
            Output.WriteLine($"Memory Manager Type: {accelerator.Memory?.GetType().FullName ?? "NULL"}");


            if (accelerator is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (accelerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }


        [SkippableFact]

        public void Diagnose_Struct_Size_And_Alignment()
        {
            Output.WriteLine("=== Struct Size and Alignment Test ===");


            unsafe
            {
                Output.WriteLine($"CudaDeviceProperties size: {sizeof(CudaDeviceProperties)} bytes");
                Output.WriteLine($"Expected size: 1032 bytes (for CUDA 12.x) or 720 bytes (for older versions)");

                // Check field offsets

                var props = new CudaDeviceProperties();
                var ptr = &props;
                Output.WriteLine("\n=== Field Offsets ===");
                Output.WriteLine($"DeviceName offset: 0");
                Output.WriteLine($"Major offset: {(byte*)&ptr->Major - (byte*)ptr}");
                Output.WriteLine($"Minor offset: {(byte*)&ptr->Minor - (byte*)ptr}");
                Output.WriteLine($"TotalGlobalMem offset: {(byte*)&ptr->TotalGlobalMem - (byte*)ptr}");
                Output.WriteLine($"ManagedMemory offset: {(byte*)&ptr->ManagedMemory - (byte*)ptr}");

                // The struct size is 1032 bytes for CUDA 12.x (which we're using)
                // This is the correct size for the current CUDA runtime

                Assert.Equal(1032, sizeof(CudaDeviceProperties));
            }
        }


        [SkippableFact]
        public void Diagnose_Multiple_Calls_Consistency()
        {
            Skip.IfNot(IsCudaAvailable(), "CUDA hardware not available");

            // Ensure CUDA is initialized

            CudaInitializer.EnsureInitialized();


            Output.WriteLine("=== Multiple Calls Consistency Test ===");


            for (var i = 0; i < 3; i++)
            {
                var props = new CudaDeviceProperties();
                var result = CudaRuntime.cudaGetDeviceProperties(ref props, 0);


                Output.WriteLine($"\nCall {i + 1}:");
                Output.WriteLine($"  Result: {result}");
                Output.WriteLine($"  Raw ManagedMemory: {props.ManagedMemory}");
                Output.WriteLine($"  Corrected ManagedMemorySupported: {props.ManagedMemorySupported}");
                Output.WriteLine($"  UnifiedAddressing: {props.UnifiedAddressing}");


                Assert.Equal(CudaError.Success, result);
                Assert.True(props.ManagedMemorySupported, $"Call {i + 1}: ManagedMemory should be supported");
            }
        }
    }
}