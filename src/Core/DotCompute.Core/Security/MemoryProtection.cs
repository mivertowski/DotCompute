// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides comprehensive memory protection services including bounds checking, 
/// guard pages, and secure memory management with defense against common memory vulnerabilities.
/// </summary>
public sealed class MemoryProtection : IDisposable
{
    private readonly ILogger _logger;
    private readonly MemoryProtectionConfiguration _configuration;
    private readonly ConcurrentDictionary<IntPtr, ProtectedMemoryRegion> _protectedRegions = new();
    private readonly ConcurrentDictionary<IntPtr, AllocationMetadata> _allocations = new();
    private readonly SemaphoreSlim _allocationLock = new(1, 1);
    private readonly Timer _integrityCheckTimer;
    private volatile bool _disposed;

    public MemoryProtection(ILogger<MemoryProtection> logger, MemoryProtectionConfiguration? configuration = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? MemoryProtectionConfiguration.Default;
        
        // Start integrity monitoring
        _integrityCheckTimer = new Timer(PerformIntegrityCheck, null, 
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        
        _logger.LogInformation("MemoryProtection initialized with configuration: {Configuration}", 
            _configuration.ToString());
    }

    /// <summary>
    /// Allocates protected memory with guard pages and bounds checking.
    /// </summary>
    /// <param name="size">Size of memory to allocate in bytes</param>
    /// <param name="alignment">Memory alignment requirement (default: 8)</param>
    /// <param name="canExecute">Whether the memory should be executable</param>
    /// <param name="identifier">Optional identifier for debugging</param>
    /// <returns>Protected memory allocation result</returns>
    public async Task<ProtectedMemoryAllocation> AllocateProtectedMemoryAsync(nuint size, 
        nuint alignment = 8, bool canExecute = false, string? identifier = null)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemoryProtection));
        }


        if (size == 0 || size > _configuration.MaxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(size), 
                $"Size must be between 1 and {_configuration.MaxAllocationSize} bytes");
        }


        await _allocationLock.WaitAsync();
        try
        {
            _logger.LogDebug("Allocating protected memory: Size={Size}, Alignment={Alignment}, Executable={CanExecute}, Id={Identifier}",
                size, alignment, canExecute, identifier);

            // Calculate total size with guard pages
            var guardPageSize = (nuint)Environment.SystemPageSize;
            var totalSize = size + (2 * guardPageSize); // Guard pages before and after
            var alignedSize = AlignSize(totalSize, alignment);

            // Allocate memory with appropriate permissions
            var baseAddress = AllocateRawMemory(alignedSize, canExecute);
            if (baseAddress == IntPtr.Zero)
            {
                throw new OutOfMemoryException("Failed to allocate protected memory");
            }

            // Setup guard pages
            var userDataPtr = baseAddress + (int)guardPageSize;
            SetupGuardPages(baseAddress, guardPageSize, userDataPtr + (int)size, guardPageSize);

            // Create protection metadata
            var region = new ProtectedMemoryRegion
            {
                BaseAddress = baseAddress,
                UserDataAddress = userDataPtr,
                Size = size,
                TotalSize = alignedSize,
                GuardPageSize = guardPageSize,
                CanExecute = canExecute,
                Identifier = identifier ?? Guid.NewGuid().ToString("N")[..8],
                AllocationTime = DateTimeOffset.UtcNow
            };

            // Initialize canary values for integrity checking
            InitializeCanaryValues(region);

            // Register the protected region
            _protectedRegions.TryAdd(baseAddress, region);
            _allocations.TryAdd(userDataPtr, new AllocationMetadata 
            { 
                Region = region, 
                AccessCount = 0,
                LastAccess = DateTimeOffset.UtcNow
            });

            var allocation = new ProtectedMemoryAllocation
            {
                Address = userDataPtr,
                Size = size,
                Identifier = region.Identifier,
                CanExecute = canExecute,
                Region = region
            };

            _logger.LogInformation("Protected memory allocated: Address={Address:X}, Size={Size}, Id={Identifier}",
                userDataPtr.ToInt64(), size, region.Identifier);

            return allocation;
        }
        finally
        {
            _allocationLock.Release();
        }
    }

    /// <summary>
    /// Performs bounds-checked memory read operation.
    /// </summary>
    /// <typeparam name="T">Type of data to read</typeparam>
    /// <param name="address">Memory address to read from</param>
    /// <param name="offset">Offset in bytes from the base address</param>
    /// <returns>The value read from memory</returns>
    public unsafe T ReadMemory<T>(IntPtr address, nuint offset = 0) where T : unmanaged
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemoryProtection));
        }


        if (!_allocations.TryGetValue(address, out var metadata))
        {

            throw new SecurityException($"Attempt to read from unprotected memory address: {address:X}");
        }


        var region = metadata.Region;
        var readSize = (nuint)sizeof(T);
        var readAddress = address + (int)offset;

        // Bounds checking
        if (offset > region.Size || offset + readSize > region.Size)
        {
            var violation = new MemoryViolation
            {
                ViolationType = MemoryViolationType.BoundsViolation,
                Address = readAddress,
                Size = readSize,
                Offset = offset,
                AllowedSize = region.Size,
                Region = region,
                Operation = "Read"
            };
            
            LogMemoryViolation(violation);
            throw new AccessViolationException($"Memory bounds violation: attempted to read {readSize} bytes at offset {offset}, but allocation is only {region.Size} bytes");
        }

        // Verify memory integrity
        if (_configuration.EnableIntegrityChecking && !VerifyCanaryValues(region))
        {
            var violation = new MemoryViolation
            {
                ViolationType = MemoryViolationType.CorruptionDetected,
                Address = readAddress,
                Region = region,
                Operation = "Read"
            };
            
            LogMemoryViolation(violation);
            throw new DataMisalignedException("Memory corruption detected during read operation");
        }

        // Update access tracking
        metadata.AccessCount++;
        metadata.LastAccess = DateTimeOffset.UtcNow;

        // Perform safe memory read
        try
        {
            var value = Unsafe.Read<T>((void*)readAddress);
            _logger.LogTrace("Memory read successful: Address={Address:X}, Size={Size}, Value={Value}",
                readAddress.ToInt64(), readSize, value);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading memory at address: {Address:X}", readAddress.ToInt64());
            throw new AccessViolationException($"Failed to read memory at address {readAddress:X}", ex);
        }
    }

    /// <summary>
    /// Performs bounds-checked memory write operation.
    /// </summary>
    /// <typeparam name="T">Type of data to write</typeparam>
    /// <param name="address">Memory address to write to</param>
    /// <param name="value">Value to write</param>
    /// <param name="offset">Offset in bytes from the base address</param>
    public unsafe void WriteMemory<T>(IntPtr address, T value, nuint offset = 0) where T : unmanaged
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemoryProtection));
        }


        if (!_allocations.TryGetValue(address, out var metadata))
        {

            throw new SecurityException($"Attempt to write to unprotected memory address: {address:X}");
        }


        var region = metadata.Region;
        var writeSize = (nuint)sizeof(T);
        var writeAddress = address + (int)offset;

        // Bounds checking
        if (offset > region.Size || offset + writeSize > region.Size)
        {
            var violation = new MemoryViolation
            {
                ViolationType = MemoryViolationType.BoundsViolation,
                Address = writeAddress,
                Size = writeSize,
                Offset = offset,
                AllowedSize = region.Size,
                Region = region,
                Operation = "Write"
            };
            
            LogMemoryViolation(violation);
            throw new AccessViolationException($"Memory bounds violation: attempted to write {writeSize} bytes at offset {offset}, but allocation is only {region.Size} bytes");
        }

        // Verify memory integrity before write
        if (_configuration.EnableIntegrityChecking && !VerifyCanaryValues(region))
        {
            var violation = new MemoryViolation
            {
                ViolationType = MemoryViolationType.CorruptionDetected,
                Address = writeAddress,
                Region = region,
                Operation = "Write"
            };
            
            LogMemoryViolation(violation);
            throw new DataMisalignedException("Memory corruption detected before write operation");
        }

        // Update access tracking
        metadata.AccessCount++;
        metadata.LastAccess = DateTimeOffset.UtcNow;

        // Perform safe memory write
        try
        {
            Unsafe.Write((void*)writeAddress, value);
            _logger.LogTrace("Memory write successful: Address={Address:X}, Size={Size}, Value={Value}",
                writeAddress.ToInt64(), writeSize, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing memory at address: {Address:X}", writeAddress.ToInt64());
            throw new AccessViolationException($"Failed to write memory at address {writeAddress:X}", ex);
        }
    }

    /// <summary>
    /// Securely deallocates protected memory with sanitization.
    /// </summary>
    /// <param name="allocation">The protected memory allocation to free</param>
    public async Task FreeProtectedMemoryAsync(ProtectedMemoryAllocation allocation)
    {
        if (_disposed)
        {

            throw new ObjectDisposedException(nameof(MemoryProtection));
        }


        ArgumentNullException.ThrowIfNull(allocation);

        await _allocationLock.WaitAsync();
        try
        {
            _logger.LogDebug("Freeing protected memory: Address={Address:X}, Size={Size}, Id={Identifier}",
                allocation.Address.ToInt64(), allocation.Size, allocation.Identifier);

            if (!_protectedRegions.TryRemove(allocation.Region.BaseAddress, out var region))
            {
                _logger.LogWarning("Attempted to free unknown memory region: {Address:X}", allocation.Address.ToInt64());
                return;
            }

            _allocations.TryRemove(allocation.Address, out _);

            // Verify integrity one final time
            if (_configuration.EnableIntegrityChecking && !VerifyCanaryValues(region))
            {
                _logger.LogWarning("Memory corruption detected during deallocation: {Identifier}", region.Identifier);
            }

            // Secure memory wiping
            if (_configuration.EnableSecureWiping)
            {
                await SecureWipeMemoryAsync(region);
            }

            // Free the memory
            FreeRawMemory(region.BaseAddress, region.TotalSize);

            _logger.LogInformation("Protected memory freed: Address={Address:X}, Size={Size}, Id={Identifier}",
                allocation.Address.ToInt64(), allocation.Size, allocation.Identifier);
        }
        finally
        {
            _allocationLock.Release();
        }
    }

    /// <summary>
    /// Validates memory access patterns for potential attacks.
    /// </summary>
    /// <param name="address">Memory address being accessed</param>
    /// <param name="size">Size of the access</param>
    /// <param name="operation">Type of operation (read/write)</param>
    /// <returns>True if access is valid, false otherwise</returns>
    public bool ValidateMemoryAccess(IntPtr address, nuint size, string operation)
    {
        if (_disposed)
        {

            return false;
        }


        try
        {
            if (!_allocations.TryGetValue(address, out var metadata))
            {
                _logger.LogWarning("Access to unprotected memory detected: {Address:X}", address.ToInt64());
                return false;
            }

            var region = metadata.Region;
            
            // Check for potential buffer overflow
            if (size > region.Size)
            {
                _logger.LogWarning("Potential buffer overflow detected: {Address:X}, RequestedSize={Size}, AllocatedSize={AllocatedSize}",
                    address.ToInt64(), size, region.Size);
                return false;
            }

            // Check for suspicious access patterns
            if (DetectSuspiciousAccessPattern(metadata, operation))
            {
                _logger.LogWarning("Suspicious memory access pattern detected: {Address:X}, Operation={Operation}",
                    address.ToInt64(), operation);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating memory access: {Address:X}", address.ToInt64());
            return false;
        }
    }

    /// <summary>
    /// Gets memory protection statistics.
    /// </summary>
    /// <returns>Current memory protection statistics</returns>
    public MemoryProtectionStatistics GetStatistics()
    {
        return new MemoryProtectionStatistics
        {
            ActiveAllocations = _allocations.Count,
            TotalAllocatedBytes = _protectedRegions.Values.Sum(r => (long)r.Size),
            TotalGuardPageBytes = _protectedRegions.Values.Sum(r => (long)(r.GuardPageSize * 2)),
            ViolationCount = _violationCount,
            CorruptionDetectionCount = _corruptionCount
        };
    }

    #region Private Implementation

    private volatile int _violationCount;
    private volatile int _corruptionCount;

    private IntPtr AllocateRawMemory(nuint size, bool canExecute)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return AllocateWindowsMemory(size, canExecute);
            }
            else
            {
                return AllocateUnixMemory(size, canExecute);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to allocate raw memory: Size={Size}, Executable={CanExecute}", size, canExecute);
            return IntPtr.Zero;
        }
    }

    private IntPtr AllocateWindowsMemory(nuint size, bool canExecute)
    {
        // Windows VirtualAlloc implementation
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        var protect = canExecute ? 0x40u : 0x04u; // PAGE_EXECUTE_READWRITE : PAGE_READWRITE

        var result = VirtualAlloc(IntPtr.Zero, size, MEM_COMMIT | MEM_RESERVE, protect);
        if (result == IntPtr.Zero)
        {
            throw new OutOfMemoryException($"VirtualAlloc failed: {Marshal.GetLastWin32Error()}");
        }

        return result;
    }

    private IntPtr AllocateUnixMemory(nuint size, bool canExecute)
    {
        // Unix mmap implementation
        const int PROT_READ = 0x1;
        const int PROT_WRITE = 0x2;
        const int PROT_EXEC = 0x4;
        const int MAP_PRIVATE = 0x2;
        const int MAP_ANONYMOUS = 0x20;

        var prot = PROT_READ | PROT_WRITE;
        if (canExecute)
        {
            prot |= PROT_EXEC;
        }


        var result = mmap(IntPtr.Zero, size, prot, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);
        if (result == new IntPtr(-1))
        {
            throw new OutOfMemoryException($"mmap failed: {Marshal.GetLastPInvokeError()}");
        }

        return result;
    }

    private void SetupGuardPages(IntPtr beforeGuard, nuint beforeSize, IntPtr afterGuard, nuint afterSize)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Set guard pages to no access
                VirtualProtect(beforeGuard, beforeSize, 0x01, out _); // PAGE_NOACCESS
                VirtualProtect(afterGuard, afterSize, 0x01, out _); // PAGE_NOACCESS
            }
            else
            {
                // Unix mprotect to remove all permissions
                mprotect(beforeGuard, beforeSize, 0);
                mprotect(afterGuard, afterSize, 0);
            }

            _logger.LogTrace("Guard pages configured: Before={BeforeAddress:X}, After={AfterAddress:X}",
                beforeGuard.ToInt64(), afterGuard.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to setup guard pages");
            throw new SecurityException("Failed to configure memory protection", ex);
        }
    }

    private void InitializeCanaryValues(ProtectedMemoryRegion region)
    {
        if (!_configuration.EnableIntegrityChecking)
        {
            return;
        }

        // Generate random canary values

        var canary = new byte[8];
        RandomNumberGenerator.Fill(canary);
        region.CanaryValue = BitConverter.ToUInt64(canary);

        // Write canary at the beginning and end of user data
        unsafe
        {
            *(ulong*)region.UserDataAddress = region.CanaryValue;
            if (region.Size >= 16) // Only if we have enough space
            {
                *(ulong*)(region.UserDataAddress + (int)region.Size - 8) = region.CanaryValue;
            }
        }
    }

    private unsafe bool VerifyCanaryValues(ProtectedMemoryRegion region)
    {
        if (!_configuration.EnableIntegrityChecking)
        {

            return true;
        }


        try
        {
            var startCanary = *(ulong*)region.UserDataAddress;
            var isValid = startCanary == region.CanaryValue;

            if (region.Size >= 16)
            {
                var endCanary = *(ulong*)(region.UserDataAddress + (int)region.Size - 8);
                isValid &= endCanary == region.CanaryValue;
            }

            return isValid;
        }
        catch
        {
            return false;
        }
    }

    private async Task SecureWipeMemoryAsync(ProtectedMemoryRegion region)
    {
        try
        {
            await Task.Run(() =>
            {
                // Multiple pass secure wipe following DoD 5220.22-M standard
                SecureWipePass(region.UserDataAddress, region.Size, 0x00);
                SecureWipePass(region.UserDataAddress, region.Size, 0xFF);
                SecureWipePass(region.UserDataAddress, region.Size, 0xAA);
                SecureWipePass(region.UserDataAddress, region.Size, 0x55);
                
                // Final random pass
                var random = new byte[region.Size];
                RandomNumberGenerator.Fill(random);
                Marshal.Copy(random, 0, region.UserDataAddress, (int)region.Size);
            });

            _logger.LogTrace("Secure memory wipe completed: {Address:X}, Size={Size}",
                region.UserDataAddress.ToInt64(), region.Size);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Secure memory wipe failed: {Address:X}", region.UserDataAddress.ToInt64());
        }
    }

    private unsafe void SecureWipePass(IntPtr address, nuint size, byte pattern)
    {
        var ptr = (byte*)address;
        for (nuint i = 0; i < size; i++)
        {
            ptr[i] = pattern;
        }
        
        // Ensure compiler doesn't optimize away the write
        MemoryBarrier();
    }

    private void FreeRawMemory(IntPtr address, nuint size)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                VirtualFree(address, UIntPtr.Zero, 0x8000); // MEM_RELEASE
            }
            else
            {
                munmap(address, size);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error freeing raw memory: {Address:X}", address.ToInt64());
        }
    }

    private static nuint AlignSize(nuint size, nuint alignment) => (size + alignment - 1) & ~(alignment - 1);

    private bool DetectSuspiciousAccessPattern(AllocationMetadata metadata, string operation)
    {
        var region = metadata.Region;
        var now = DateTimeOffset.UtcNow;
        var timeSinceLastAccess = now - metadata.LastAccess;

        // Check for rapid repeated access (potential exploit)
        if (timeSinceLastAccess < TimeSpan.FromMilliseconds(1) && metadata.AccessCount > 1000)
        {
            return true;
        }

        // Check for access to very old allocations (use-after-free detection)
        var allocationAge = now - region.AllocationTime;
        if (allocationAge > TimeSpan.FromHours(1) && metadata.AccessCount > 10000)
        {
            return true;
        }

        return false;
    }

    private void LogMemoryViolation(MemoryViolation violation)
    {
        Interlocked.Increment(ref _violationCount);
        
        if (violation.ViolationType == MemoryViolationType.CorruptionDetected)
        {
            Interlocked.Increment(ref _corruptionCount);
        }

        _logger.LogError("Memory violation detected: Type={ViolationType}, Address={Address:X}, Operation={Operation}, Size={Size}",
            violation.ViolationType, violation.Address.ToInt64(), violation.Operation, violation.Size);
    }

    private void PerformIntegrityCheck(object? state)
    {
        if (_disposed || !_configuration.EnableIntegrityChecking)
        {
            return;
        }


        try
        {
            var corruptedRegions = new List<ProtectedMemoryRegion>();

            foreach (var region in _protectedRegions.Values)
            {
                if (!VerifyCanaryValues(region))
                {
                    corruptedRegions.Add(region);
                }
            }

            if (corruptedRegions.Any())
            {
                _logger.LogError("Integrity check failed: {Count} corrupted memory regions detected",
                    corruptedRegions.Count);
                
                foreach (var region in corruptedRegions)
                {
                    _logger.LogError("Corrupted region: {Identifier}, Address={Address:X}, Size={Size}",
                        region.Identifier, region.UserDataAddress.ToInt64(), region.Size);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during integrity check");
        }
    }

    #endregion

    #region P/Invoke Declarations

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr lpAddress, nuint dwSize, uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr mmap(IntPtr addr, nuint length, int prot, int flags, int fd, long offset);

    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(IntPtr addr, nuint length);

    [DllImport("libc", SetLastError = true)]
    private static extern int mprotect(IntPtr addr, nuint len, int prot);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MemoryBarrier() => Thread.MemoryBarrier();

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }


        _disposed = true;

        _integrityCheckTimer?.Dispose();
        _allocationLock?.Dispose();

        // Free all remaining protected memory
        foreach (var region in _protectedRegions.Values)
        {
            try
            {
                if (_configuration.EnableSecureWiping)
                {
                    SecureWipeMemoryAsync(region).GetAwaiter().GetResult();
                }
                FreeRawMemory(region.BaseAddress, region.TotalSize);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing memory region: {Identifier}", region.Identifier);
            }
        }

        _protectedRegions.Clear();
        _allocations.Clear();

        _logger.LogInformation("MemoryProtection disposed. Final statistics: Allocations={AllocationsFreed}, Violations={Violations}",
            _protectedRegions.Count, _violationCount);
    }
}

#region Supporting Types

/// <summary>
/// Configuration for memory protection behavior.
/// </summary>
public sealed class MemoryProtectionConfiguration
{
    public static MemoryProtectionConfiguration Default => new()
    {
        EnableIntegrityChecking = true,
        EnableSecureWiping = true,
        EnableGuardPages = true,
        MaxAllocationSize = 1024 * 1024 * 1024, // 1GB
        DefaultAlignment = 8
    };

    public bool EnableIntegrityChecking { get; init; } = true;
    public bool EnableSecureWiping { get; init; } = true;
    public bool EnableGuardPages { get; init; } = true;
    public nuint MaxAllocationSize { get; init; } = 1024 * 1024 * 1024;
    public nuint DefaultAlignment { get; init; } = 8;

    public override string ToString() =>
        $"Integrity={EnableIntegrityChecking}, SecureWipe={EnableSecureWiping}, GuardPages={EnableGuardPages}, MaxSize={MaxAllocationSize / (1024 * 1024)}MB";
}

/// <summary>
/// Represents a protected memory region with metadata.
/// </summary>
public sealed class ProtectedMemoryRegion
{
    public required IntPtr BaseAddress { get; init; }
    public required IntPtr UserDataAddress { get; init; }
    public required nuint Size { get; init; }
    public required nuint TotalSize { get; init; }
    public required nuint GuardPageSize { get; init; }
    public required bool CanExecute { get; init; }
    public required string Identifier { get; init; }
    public required DateTimeOffset AllocationTime { get; init; }
    public ulong CanaryValue { get; set; }
}

/// <summary>
/// Represents a protected memory allocation.
/// </summary>
public sealed class ProtectedMemoryAllocation
{
    public required IntPtr Address { get; init; }
    public required nuint Size { get; init; }
    public required string Identifier { get; init; }
    public required bool CanExecute { get; init; }
    public required ProtectedMemoryRegion Region { get; init; }
}

/// <summary>
/// Metadata for tracking memory allocations.
/// </summary>
internal sealed class AllocationMetadata
{
    public required ProtectedMemoryRegion Region { get; init; }
    public long AccessCount { get; set; }
    public DateTimeOffset LastAccess { get; set; }
}

/// <summary>
/// Represents a memory violation.
/// </summary>
public sealed class MemoryViolation
{
    public required MemoryViolationType ViolationType { get; init; }
    public required IntPtr Address { get; init; }
    public nuint Size { get; init; }
    public nuint Offset { get; init; }
    public nuint AllowedSize { get; init; }
    public required ProtectedMemoryRegion Region { get; init; }
    public required string Operation { get; init; }
}

/// <summary>
/// Types of memory violations.
/// </summary>
public enum MemoryViolationType
{
    BoundsViolation,
    CorruptionDetected,
    UseAfterFree,
    DoubleFree,
    InvalidAccess
}

/// <summary>
/// Memory protection statistics.
/// </summary>
public sealed class MemoryProtectionStatistics
{
    public int ActiveAllocations { get; init; }
    public long TotalAllocatedBytes { get; init; }
    public long TotalGuardPageBytes { get; init; }
    public int ViolationCount { get; init; }
    public int CorruptionDetectionCount { get; init; }
}

#endregion