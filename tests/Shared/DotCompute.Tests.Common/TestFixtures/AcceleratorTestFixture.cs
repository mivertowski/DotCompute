using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotCompute.Abstractions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace DotCompute.Tests.Shared.TestFixtures;

/// <summary>
/// Shared test fixture for accelerator-based tests.
/// </summary>
public class AcceleratorTestFixture : IAsyncLifetime
{
    private readonly List<IAccelerator> _accelerators = new();
    private IAcceleratorManager? _acceleratorManager = null;
    
    public AcceleratorTestFixture()
    {
        // Note: Class fixtures cannot receive ITestOutputHelper
        // Individual tests must use their own ITestOutputHelper
    }
    
    /// <summary>
    /// Gets the available accelerators.
    /// </summary>
    public IReadOnlyList<IAccelerator> Accelerators => _accelerators.AsReadOnly();
    
    /// <summary>
    /// Gets the default accelerator(first available).
    /// </summary>
    public IAccelerator? DefaultAccelerator => _accelerators.FirstOrDefault();
    
    /// <summary>
    /// Gets the accelerator manager.
    /// </summary>
    public IAcceleratorManager? AcceleratorManager => _acceleratorManager;
    
    /// <summary>
    /// Checks if CUDA is available on the system.
    /// </summary>
    public static bool IsCudaAvailable()
    {
        try
        {
            // Check for CUDA runtime library
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeLibrary.TryLoad("cudart64_12.dll", out _) ||
                       NativeLibrary.TryLoad("cudart64_11.dll", out _) ||
                       NativeLibrary.TryLoad("cudart64_10.dll", out _);
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return NativeLibrary.TryLoad("libcudart.so.12", out _) ||
                       NativeLibrary.TryLoad("libcudart.so.11", out _) ||
                       NativeLibrary.TryLoad("libcudart.so.10", out _) ||
                       NativeLibrary.TryLoad("libcudart.so", out _);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Checks if OpenCL is available on the system.
    /// </summary>
    public static bool IsOpenCLAvailable()
    {
        try
        {
            // Check for OpenCL runtime library
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return NativeLibrary.TryLoad("OpenCL.dll", out _);
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return NativeLibrary.TryLoad("libOpenCL.so.1", out _) ||
                       NativeLibrary.TryLoad("libOpenCL.so", out _);
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return NativeLibrary.TryLoad("/System/Library/Frameworks/OpenCL.framework/OpenCL", out _);
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Checks if DirectCompute is available(Windows only).
    /// </summary>
    public static bool IsDirectComputeAvailable()
    {
        if(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;
            
        try
        {
            return NativeLibrary.TryLoad("d3d11.dll", out _) &&
                   NativeLibrary.TryLoad("dxgi.dll", out _);
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Gets the available hardware accelerator types.
    /// </summary>
    public static IEnumerable<AcceleratorType> GetAvailableAcceleratorTypes()
    {
        var types = new List<AcceleratorType> { AcceleratorType.CPU };
        
        if(IsCudaAvailable())
            types.Add(AcceleratorType.CUDA);
            
        if(IsOpenCLAvailable())
            types.Add(AcceleratorType.OpenCL);
            
        if(IsDirectComputeAvailable())
            types.Add(AcceleratorType.DirectML);
            
        return types;
    }
    
    public async Task InitializeAsync()
    {
        // TODO: Initialize real accelerator manager when available
        // For now, we'll detect available hardware but not initialize
        
        var availableTypes = GetAvailableAcceleratorTypes();
        // Store the available types for later use by tests
        
        await Task.CompletedTask;
    }
    
    public async Task DisposeAsync()
    {
        foreach (var accelerator in _accelerators)
        {
            await accelerator.DisposeAsync();
        }
        _accelerators.Clear();
        
        if(_acceleratorManager != null)
        {
            await _acceleratorManager.DisposeAsync();
        }
    }
}

/// <summary>
/// Attribute to skip tests when specific hardware is not available.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiresHardwareAttribute : Attribute
{
    public RequiresHardwareAttribute(AcceleratorType requiredType)
    {
        RequiredType = requiredType;
    }
    
    public AcceleratorType RequiredType { get; }
    
    public bool IsAvailable()
    {
        return RequiredType switch
        {
            AcceleratorType.CUDA => AcceleratorTestFixture.IsCudaAvailable(),
            AcceleratorType.OpenCL => AcceleratorTestFixture.IsOpenCLAvailable(),
            AcceleratorType.DirectML => AcceleratorTestFixture.IsDirectComputeAvailable(),
            AcceleratorType.CPU => true,
            _ => false
        };
    }
}

/// <summary>
/// Theory attribute that skips test if required hardware is not available.
/// </summary>
public class HardwareTheoryAttribute : TheoryAttribute
{
    private readonly AcceleratorType _requiredType;
    
    public HardwareTheoryAttribute(AcceleratorType requiredType)
    {
        _requiredType = requiredType;
        
        var hardware = new RequiresHardwareAttribute(requiredType);
        if(!hardware.IsAvailable())
        {
            Skip = $"Test requires {requiredType} hardware which is not available";
        }
    }
}

/// <summary>
/// Fact attribute that skips test if required hardware is not available.
/// </summary>
public class HardwareFactAttribute : FactAttribute
{
    public HardwareFactAttribute(AcceleratorType requiredType)
    {
        var hardware = new RequiresHardwareAttribute(requiredType);
        if(!hardware.IsAvailable())
        {
            Skip = $"Test requires {requiredType} hardware which is not available";
        }
    }
}
