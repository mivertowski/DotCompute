// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using DotCompute.Core.Logging;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace DotCompute.Core.Security;

/// <summary>
/// Provides comprehensive memory protection services including bounds checking,
/// guard pages, and secure memory management with defense against common memory vulnerabilities.
/// </summary>
public sealed partial class MemoryProtection : IDisposable
{
    private readonly ILogger _logger;
    private readonly MemoryProtectionConfiguration _configuration;
    private readonly ConcurrentDictionary<IntPtr, ProtectedMemoryRegion> _protectedRegions = new();
    private readonly ConcurrentDictionary<IntPtr, AllocationMetadata> _allocations = new();
    private readonly SemaphoreSlim _allocationLock = new(1, 1);
    private readonly Timer _integrityCheckTimer;
    private volatile bool _disposed;

    #region LoggerMessage Delegates (Event ID Range: 18000-18099)

    private static readonly Action<ILogger, long, nuint, object?, string?, Exception?> _logMemoryReadSuccessful =
        LoggerMessage.Define<long, nuint, object?, string?>(
            MsLogLevel.Trace,
            new EventId(18000, nameof(MemoryReadSuccessful)),
            "Memory read successful: Address={Address:X}, Size={Size}, Value={Value}, Type={TypeName}");

    private static void MemoryReadSuccessful(ILogger logger, long address, nuint size, object? value, string? typeName)
        => _logMemoryReadSuccessful(logger, address, size, value, typeName, null);

    private static readonly Action<ILogger, long, Exception?> _logMemoryReadError =
        LoggerMessage.Define<long>(
            MsLogLevel.Error,
            new EventId(18001, nameof(MemoryReadError)),
            "Error reading memory at address: {Address:X}");

    private static void MemoryReadError(ILogger logger, Exception ex, long address)
        => _logMemoryReadError(logger, address, ex);

    private static readonly Action<ILogger, long, nuint, object?, Exception?> _logMemoryWriteSuccessful =
        LoggerMessage.Define<long, nuint, object?>(
            MsLogLevel.Trace,
            new EventId(18002, nameof(MemoryWriteSuccessful)),
            "Memory write successful: Address={Address:X}, Size={Size}, Value={Value}");

    private static void MemoryWriteSuccessful(ILogger logger, long address, nuint size, object? value)
        => _logMemoryWriteSuccessful(logger, address, size, value, null);

    private static readonly Action<ILogger, long, Exception?> _logMemoryWriteError =
        LoggerMessage.Define<long>(
            MsLogLevel.Error,
            new EventId(18003, nameof(MemoryWriteError)),
            "Error writing memory at address: {Address:X}");

    private static void MemoryWriteError(ILogger logger, Exception ex, long address)
        => _logMemoryWriteError(logger, address, ex);

    private static readonly Action<ILogger, long, long, Exception?> _logGuardPagesConfigured =
        LoggerMessage.Define<long, long>(
            MsLogLevel.Trace,
            new EventId(18004, nameof(GuardPagesConfigured)),
            "Guard pages configured: Before={BeforeAddress:X}, After={AfterAddress:X}");

    private static void GuardPagesConfigured(ILogger logger, long beforeAddress, long afterAddress)
        => _logGuardPagesConfigured(logger, beforeAddress, afterAddress, null);

    private static readonly Action<ILogger, long, nuint, Exception?> _logSecureMemoryWipeCompleted =
        LoggerMessage.Define<long, nuint>(
            MsLogLevel.Trace,
            new EventId(18005, nameof(SecureMemoryWipeCompleted)),
            "Secure memory wipe completed: {Address:X}, Size={Size}");

    private static void SecureMemoryWipeCompleted(ILogger logger, long address, nuint size)
        => _logSecureMemoryWipeCompleted(logger, address, size, null);

    private static readonly Action<ILogger, long, Exception?> _logSecureMemoryWipeFailed =
        LoggerMessage.Define<long>(
            MsLogLevel.Warning,
            new EventId(18006, nameof(SecureMemoryWipeFailed)),
            "Secure memory wipe failed: {Address:X}");

    private static void SecureMemoryWipeFailed(ILogger logger, Exception ex, long address)
        => _logSecureMemoryWipeFailed(logger, address, ex);

    private static readonly Action<ILogger, MemoryViolationType, long, string?, nuint, Exception?> _logMemoryViolationDetected =
        LoggerMessage.Define<MemoryViolationType, long, string?, nuint>(
            MsLogLevel.Error,
            new EventId(18007, nameof(MemoryViolationDetected)),
            "Memory violation detected: Type={ViolationType}, Address={Address:X}, Operation={Operation}, Size={Size}");

    private static void MemoryViolationDetected(ILogger logger, MemoryViolationType violationType, long address, string? operation, nuint size)
        => _logMemoryViolationDetected(logger, violationType, address, operation, size, null);

    private static readonly Action<ILogger, int, Exception?> _logIntegrityCheckFailed =
        LoggerMessage.Define<int>(
            MsLogLevel.Error,
            new EventId(18008, nameof(IntegrityCheckFailed)),
            "Integrity check failed: {Count} corrupted memory regions detected");

    private static void IntegrityCheckFailed(ILogger logger, int count)
        => _logIntegrityCheckFailed(logger, count, null);

    private static readonly Action<ILogger, string?, long, nuint, Exception?> _logCorruptedRegion =
        LoggerMessage.Define<string?, long, nuint>(
            MsLogLevel.Error,
            new EventId(18009, nameof(CorruptedRegion)),
            "Corrupted region: {Identifier}, Address={Address:X}, Size={Size}");

    private static void CorruptedRegion(ILogger logger, string? identifier, long address, nuint size)
        => _logCorruptedRegion(logger, identifier, address, size, null);

    private static readonly Action<ILogger, Exception?> _logIntegrityCheckError =
        LoggerMessage.Define(
            MsLogLevel.Warning,
            new EventId(18010, nameof(IntegrityCheckError)),
            "Error during integrity check");

    private static void IntegrityCheckError(ILogger logger, Exception ex)
        => _logIntegrityCheckError(logger, ex);

    private static readonly Action<ILogger, string?, Exception?> _logMemoryRegionDisposeError =
        LoggerMessage.Define<string?>(
            MsLogLevel.Warning,
            new EventId(18011, nameof(MemoryRegionDisposeError)),
            "Error disposing memory region: {Identifier}");

    private static void MemoryRegionDisposeError(ILogger logger, Exception ex, string? identifier)
        => _logMemoryRegionDisposeError(logger, identifier, ex);

    #endregion

    /// <summary>
    /// Initializes a new instance of the MemoryProtection class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>

    public MemoryProtection(ILogger<MemoryProtection> logger, MemoryProtectionConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger;
        _configuration = configuration ?? MemoryProtectionConfiguration.Default;

        // Start integrity monitoring

        _integrityCheckTimer = new Timer(PerformIntegrityCheck, null,
            TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));

        _logger.MemoryProtectionInitialized(_configuration.ToString());
    }

    /// <summary>
    /// Allocates protected memory with guard pages and bounds checking.
    /// </summary>
    /// <param name="size">Size of memory to allocate in bytes</param>
    /// <param name="alignment">Memory alignment requirement (default: 8)</param>
    /// <param name="canExecute">Whether the memory should be executable</param>
    /// <param name="identifier">Optional identifier for debugging</param>
    /// <returns>Protected memory allocation result</returns>
    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "OutOfMemoryException is appropriate when actual native memory allocation fails")]
    public async Task<ProtectedMemoryAllocation> AllocateProtectedMemoryAsync(nuint size,

        nuint alignment = 8, bool canExecute = false, string? identifier = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        if (size == 0 || size > _configuration.MaxAllocationSize)
        {

            throw new ArgumentOutOfRangeException(nameof(size),

                $"Size must be between 1 and {_configuration.MaxAllocationSize} bytes");
        }


        await _allocationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Allocating protected memory: Size={size}, Alignment={alignment}, Executable={canExecute}, Id={identifier}");

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
                Identifier = identifier ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8],
                AllocationTime = DateTimeOffset.UtcNow
            };

            // Initialize canary values for integrity checking
            InitializeCanaryValues(region);

            // Register the protected region
            _ = _protectedRegions.TryAdd(baseAddress, region);
            _ = _allocations.TryAdd(userDataPtr, new AllocationMetadata
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

            _logger.ProtectedMemoryAllocated(userDataPtr, (long)size, region.Identifier);

            return allocation;
        }
        finally
        {
            _ = _allocationLock.Release();
        }
    }

    /// <summary>
    /// Performs bounds-checked memory read operation.
    /// </summary>
    /// <typeparam name="T">Type of data to read</typeparam>
    /// <param name="address">Memory address to read from</param>
    /// <param name="offset">Offset in bytes from the base address</param>
    /// <returns>The value read from memory</returns>
    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "AccessViolationException is semantically correct for memory access violations in low-level memory protection")]
    public unsafe T ReadMemory<T>(IntPtr address, nuint offset = 0) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


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
            MemoryReadSuccessful(_logger, readAddress.ToInt64(), readSize, value, typeof(T).Name);
            return value;
        }
        catch (Exception ex)
        {
            MemoryReadError(_logger, ex, readAddress.ToInt64());
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
    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "AccessViolationException is semantically correct for memory access violations in low-level memory protection")]
    public unsafe void WriteMemory<T>(IntPtr address, T value, nuint offset = 0) where T : unmanaged
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


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
            MemoryWriteSuccessful(_logger, writeAddress.ToInt64(), writeSize, value);
        }
        catch (Exception ex)
        {
            MemoryWriteError(_logger, ex, writeAddress.ToInt64());
            throw new AccessViolationException($"Failed to write memory at address {writeAddress:X}", ex);
        }
    }

    /// <summary>
    /// Securely deallocates protected memory with sanitization.
    /// </summary>
    /// <param name="allocation">The protected memory allocation to free</param>
    public async Task FreeProtectedMemoryAsync(ProtectedMemoryAllocation allocation)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);


        ArgumentNullException.ThrowIfNull(allocation);

        await _allocationLock.WaitAsync();
        try
        {
            _logger.LogDebugMessage($"Freeing protected memory: Address={allocation.Address.ToInt64()}, Size={allocation.Size}, Id={allocation.Identifier}");

            if (!_protectedRegions.TryRemove(allocation.Region.BaseAddress, out var region))
            {
                _logger.LogWarningMessage($"Attempted to free unknown memory region: {allocation.Address.ToInt64()}");
                return;
            }

            _ = _allocations.TryRemove(allocation.Address, out _);

            // Verify integrity one final time
            if (_configuration.EnableIntegrityChecking && !VerifyCanaryValues(region))
            {
                _logger.LogWarningMessage($"Memory corruption detected during deallocation: {region.Identifier}");
            }

            // Secure memory wiping
            if (_configuration.EnableSecureWiping)
            {
                await SecureWipeMemoryAsync(region);
            }

            // Free the memory
            FreeRawMemory(region.BaseAddress, region.TotalSize);

            _logger.ProtectedMemoryFreed(allocation.Address, (long)allocation.Size, allocation.Identifier);
        }
        finally
        {
            _ = _allocationLock.Release();
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
                _logger.LogWarningMessage($"Access to unprotected memory detected: {address.ToInt64()}");
                return false;
            }

            var region = metadata.Region;

            // Check for potential buffer overflow

            if (size > region.Size)
            {
                _logger.LogWarningMessage($"Potential buffer overflow detected: {address.ToInt64()}, RequestedSize={size}, AllocatedSize={region.Size}");
                return false;
            }

            // Check for suspicious access patterns
            if (DetectSuspiciousAccessPattern(metadata, operation))
            {
                _logger.LogWarningMessage($"Suspicious memory access pattern detected: {address.ToInt64()}, Operation={operation}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error validating memory access: {address.ToInt64():X}");
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
            _logger.LogErrorMessage(ex, $"Failed to allocate raw memory: Size={size}, Executable={canExecute}");
            return IntPtr.Zero;
        }
    }

    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "OutOfMemoryException is appropriate when VirtualAlloc native allocation fails")]
    private static IntPtr AllocateWindowsMemory(nuint size, bool canExecute)
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

    [SuppressMessage("Design", "CA2201:Do not raise reserved exception types", Justification = "OutOfMemoryException is appropriate when mmap native allocation fails")]
    private static IntPtr AllocateUnixMemory(nuint size, bool canExecute)
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
                _ = VirtualProtect(beforeGuard, beforeSize, 0x01, out _); // PAGE_NOACCESS
                _ = VirtualProtect(afterGuard, afterSize, 0x01, out _); // PAGE_NOACCESS
            }
            else
            {
                // Unix mprotect to remove all permissions
                _ = mprotect(beforeGuard, beforeSize, 0);
                _ = mprotect(afterGuard, afterSize, 0);
            }

            GuardPagesConfigured(_logger, beforeGuard.ToInt64(), afterGuard.ToInt64());
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to setup guard pages");
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

            SecureMemoryWipeCompleted(_logger, region.UserDataAddress.ToInt64(), region.Size);
        }
        catch (Exception ex)
        {
            SecureMemoryWipeFailed(_logger, ex, region.UserDataAddress.ToInt64());
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
                _ = VirtualFree(address, UIntPtr.Zero, 0x8000); // MEM_RELEASE
            }
            else
            {
                _ = munmap(address, size);
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error freeing raw memory: {address.ToInt64():X}");
        }
    }

    private static nuint AlignSize(nuint size, nuint alignment) => (size + alignment - 1) & ~(alignment - 1);

    private static bool DetectSuspiciousAccessPattern(AllocationMetadata metadata, string operation)
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
        _ = Interlocked.Increment(ref _violationCount);


        if (violation.ViolationType == MemoryViolationType.CorruptionDetected)
        {
            _ = Interlocked.Increment(ref _corruptionCount);
        }

        MemoryViolationDetected(_logger, violation.ViolationType, violation.Address.ToInt64(), violation.Operation, violation.Size);
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

            if (corruptedRegions.Count != 0)
            {
                IntegrityCheckFailed(_logger, corruptedRegions.Count);


                foreach (var region in corruptedRegions)
                {
                    CorruptedRegion(_logger, region.Identifier, region.UserDataAddress.ToInt64(), region.Size);
                }
            }
        }
        catch (Exception ex)
        {
            IntegrityCheckError(_logger, ex);
        }
    }

    #endregion

    #region P/Invoke Declarations

    // CA5392: Using DefaultDllImportSearchPaths to prevent DLL hijacking attacks on Windows
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr lpAddress, nuint dwSize, uint flAllocationType, uint flProtect);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(IntPtr lpAddress, nuint dwSize, uint flNewProtect, out uint lpflOldProtect);

    // CA5392: Using SafeDirectories for libc on Unix-like systems to prevent library hijacking
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr mmap(IntPtr addr, nuint length, int prot, int flags, int fd, long offset);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", SetLastError = true)]
    private static extern int munmap(IntPtr addr, nuint length);

    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    [DllImport("libc", SetLastError = true)]
    private static extern int mprotect(IntPtr addr, nuint len, int prot);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void MemoryBarrier() => Thread.MemoryBarrier();
    /// <summary>
    /// Performs dispose.
    /// </summary>

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
                    // VSTHRD002: Synchronous wait is necessary here because IDisposable.Dispose() cannot be async.
                    // Secure wiping is critical for security during cleanup.
#pragma warning disable VSTHRD002 // Avoid problematic synchronous waits
                    SecureWipeMemoryAsync(region).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                }
                FreeRawMemory(region.BaseAddress, region.TotalSize);
            }
            catch (Exception ex)
            {
                MemoryRegionDisposeError(_logger, ex, region.Identifier);
            }
        }

        _protectedRegions.Clear();
        _allocations.Clear();

        _logger.LogInfoMessage($"MemoryProtection disposed. Final statistics: Allocations={_allocations.Count}, Violations={_violationCount}");
    }
}

#region Supporting Types

/// <summary>
/// Configuration for memory protection behavior.
/// </summary>
public sealed class MemoryProtectionConfiguration
{
    /// <summary>
    /// Gets or sets the default.
    /// </summary>
    /// <value>The default.</value>
    public static MemoryProtectionConfiguration Default => new()
    {
        EnableIntegrityChecking = true,
        EnableSecureWiping = true,
        EnableGuardPages = true,
        MaxAllocationSize = 1024 * 1024 * 1024, // 1GB
        DefaultAlignment = 8
    };
    /// <summary>
    /// Gets or sets the enable integrity checking.
    /// </summary>
    /// <value>The enable integrity checking.</value>

    public bool EnableIntegrityChecking { get; init; } = true;
    /// <summary>
    /// Gets or sets the enable secure wiping.
    /// </summary>
    /// <value>The enable secure wiping.</value>
    public bool EnableSecureWiping { get; init; } = true;
    /// <summary>
    /// Gets or sets the enable guard pages.
    /// </summary>
    /// <value>The enable guard pages.</value>
    public bool EnableGuardPages { get; init; } = true;
    /// <summary>
    /// Gets or sets the max allocation size.
    /// </summary>
    /// <value>The max allocation size.</value>
    public nuint MaxAllocationSize { get; init; } = 1024 * 1024 * 1024;
    /// <summary>
    /// Gets or sets the default alignment.
    /// </summary>
    /// <value>The default alignment.</value>
    public nuint DefaultAlignment { get; init; } = 8;
    /// <summary>
    /// Gets to string.
    /// </summary>
    /// <returns>The result of the operation.</returns>

    public override string ToString()
        => $"Integrity={EnableIntegrityChecking}, SecureWipe={EnableSecureWiping}, GuardPages={EnableGuardPages}, MaxSize={MaxAllocationSize / (1024 * 1024)}MB";
}

/// <summary>
/// Represents a protected memory region with metadata.
/// </summary>
public sealed class ProtectedMemoryRegion
{
    /// <summary>
    /// Gets or sets the base address.
    /// </summary>
    /// <value>The base address.</value>
    public required IntPtr BaseAddress { get; init; }
    /// <summary>
    /// Gets or sets the user data address.
    /// </summary>
    /// <value>The user data address.</value>
    public required IntPtr UserDataAddress { get; init; }
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    /// <value>The size.</value>
    public required nuint Size { get; init; }
    /// <summary>
    /// Gets or sets the total size.
    /// </summary>
    /// <value>The total size.</value>
    public required nuint TotalSize { get; init; }
    /// <summary>
    /// Gets or sets the guard page size.
    /// </summary>
    /// <value>The guard page size.</value>
    public required nuint GuardPageSize { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether execute.
    /// </summary>
    /// <value>The can execute.</value>
    public required bool CanExecute { get; init; }
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The identifier.</value>
    public required string Identifier { get; init; }
    /// <summary>
    /// Gets or sets the allocation time.
    /// </summary>
    /// <value>The allocation time.</value>
    public required DateTimeOffset AllocationTime { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether ary value.
    /// </summary>
    /// <value>The canary value.</value>
    public ulong CanaryValue { get; set; }
}

/// <summary>
/// Represents a protected memory allocation.
/// </summary>
public sealed class ProtectedMemoryAllocation
{
    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    /// <value>The address.</value>
    public required IntPtr Address { get; init; }
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    /// <value>The size.</value>
    public required nuint Size { get; init; }
    /// <summary>
    /// Gets or sets the identifier.
    /// </summary>
    /// <value>The identifier.</value>
    public required string Identifier { get; init; }
    /// <summary>
    /// Gets or sets a value indicating whether execute.
    /// </summary>
    /// <value>The can execute.</value>
    public required bool CanExecute { get; init; }
    /// <summary>
    /// Gets or sets the region.
    /// </summary>
    /// <value>The region.</value>
    public required ProtectedMemoryRegion Region { get; init; }
}

/// <summary>
/// Metadata for tracking memory allocations.
/// </summary>
internal sealed class AllocationMetadata
{
    /// <summary>
    /// Gets or sets the region.
    /// </summary>
    /// <value>The region.</value>
    public required ProtectedMemoryRegion Region { get; init; }
    /// <summary>
    /// Gets or sets the access count.
    /// </summary>
    /// <value>The access count.</value>
    public long AccessCount { get; set; }
    /// <summary>
    /// Gets or sets the last access.
    /// </summary>
    /// <value>The last access.</value>
    public DateTimeOffset LastAccess { get; set; }
}

/// <summary>
/// Represents a memory violation.
/// </summary>
public sealed class MemoryViolation
{
    /// <summary>
    /// Gets or sets the violation type.
    /// </summary>
    /// <value>The violation type.</value>
    public required MemoryViolationType ViolationType { get; init; }
    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    /// <value>The address.</value>
    public required IntPtr Address { get; init; }
    /// <summary>
    /// Gets or sets the size.
    /// </summary>
    /// <value>The size.</value>
    public nuint Size { get; init; }
    /// <summary>
    /// Gets or sets the offset.
    /// </summary>
    /// <value>The offset.</value>
    public nuint Offset { get; init; }
    /// <summary>
    /// Gets or sets the allowed size.
    /// </summary>
    /// <value>The allowed size.</value>
    public nuint AllowedSize { get; init; }
    /// <summary>
    /// Gets or sets the region.
    /// </summary>
    /// <value>The region.</value>
    public required ProtectedMemoryRegion Region { get; init; }
    /// <summary>
    /// Gets or sets the operation.
    /// </summary>
    /// <value>The operation.</value>
    public required string Operation { get; init; }
}
/// <summary>
/// An memory violation type enumeration.
/// </summary>

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
    /// <summary>
    /// Gets or sets the active allocations.
    /// </summary>
    /// <value>The active allocations.</value>
    public int ActiveAllocations { get; init; }
    /// <summary>
    /// Gets or sets the total allocated bytes.
    /// </summary>
    /// <value>The total allocated bytes.</value>
    public long TotalAllocatedBytes { get; init; }
    /// <summary>
    /// Gets or sets the total guard page bytes.
    /// </summary>
    /// <value>The total guard page bytes.</value>
    public long TotalGuardPageBytes { get; init; }
    /// <summary>
    /// Gets or sets the violation count.
    /// </summary>
    /// <value>The violation count.</value>
    public int ViolationCount { get; init; }
    /// <summary>
    /// Gets or sets the corruption detection count.
    /// </summary>
    /// <value>The corruption detection count.</value>
    public int CorruptionDetectionCount { get; init; }
}



#endregion
