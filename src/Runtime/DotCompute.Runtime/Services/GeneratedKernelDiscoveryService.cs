// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Reflection;
using Microsoft.Extensions.Logging;
using DotCompute.Runtime.Logging;
using System.Collections;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Service for discovering and registering kernels from generated source code.
/// This bridges the generated KernelRegistry with the runtime KernelExecutionService.
/// </summary>
public class GeneratedKernelDiscoveryService
{
    private readonly ILogger<GeneratedKernelDiscoveryService> _logger;
    private readonly List<Assembly> _scannedAssemblies = [];

    public GeneratedKernelDiscoveryService(ILogger<GeneratedKernelDiscoveryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Discovers and registers all kernels from loaded assemblies.
    /// </summary>
    /// <param name="kernelExecutionService">The service to register kernels with</param>
    /// <returns>The number of kernels discovered and registered</returns>
    public async Task<int> DiscoverAndRegisterKernelsAsync(KernelExecutionService kernelExecutionService)
    {
        var discoveredKernels = await DiscoverKernelsAsync();


        if (discoveredKernels.Count > 0)
        {
            kernelExecutionService.RegisterKernels(discoveredKernels);
            _logger.LogInfoMessage($"Discovered and registered {discoveredKernels.Count} kernels from {_scannedAssemblies.Count} assemblies");
        }
        else
        {
            _logger.LogWarningMessage("No kernels discovered. Ensure that source generators have run and generated kernel registries exist.");
        }

        return discoveredKernels.Count;
    }

    /// <summary>
    /// Discovers kernels from all loaded assemblies.
    /// </summary>
    /// <returns>List of discovered kernel registrations</returns>
    public async Task<List<KernelRegistrationInfo>> DiscoverKernelsAsync()
    {
        var allKernels = new List<KernelRegistrationInfo>();
        var assemblies = GetScannableAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                var kernels = await DiscoverKernelsFromAssemblyAsync(assembly);
                allKernels.AddRange(kernels);
                _scannedAssemblies.Add(assembly);


                _logger.LogDebugMessage($"Discovered {kernels.Count} kernels from assembly {assembly.GetName().Name}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to scan assembly {AssemblyName} for kernels",
                    assembly.GetName().Name);
            }
        }

        return allKernels;
    }

    /// <summary>
    /// Discovers kernels from a specific assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan</param>
    /// <returns>List of kernel registrations from the assembly</returns>
    public async Task<List<KernelRegistrationInfo>> DiscoverKernelsFromAssemblyAsync(Assembly assembly)
    {
        var kernels = new List<KernelRegistrationInfo>();

        try
        {
            // Look for generated KernelRegistry classes
            var kernelRegistryTypes = assembly.GetTypes()
                .Where(t => t.Name == "KernelRegistry" && t.Namespace?.EndsWith(".Generated") == true)
                .ToList();

            foreach (var registryType in kernelRegistryTypes)
            {
                var registryKernels = await ExtractKernelsFromRegistryAsync(registryType);
                kernels.AddRange(registryKernels);
            }

            // Also look for types with [Kernel] attribute directly
            var kernelMethods = assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(method => HasKernelAttribute(method))
                .ToList();

            foreach (var method in kernelMethods)
            {
                var kernelInfo = await CreateKernelRegistrationFromMethodAsync(method);
                if (kernelInfo != null)
                {
                    kernels.Add(kernelInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Error scanning assembly {assembly.GetName().Name}");
        }

        return kernels;
    }

    private async Task<List<KernelRegistrationInfo>> ExtractKernelsFromRegistryAsync(Type registryType)
    {
        var kernels = new List<KernelRegistrationInfo>();

        try
        {
            // Look for the GetAllKernels method in the generated registry
            var getAllMethod = registryType.GetMethod("GetAllKernels", BindingFlags.Public | BindingFlags.Static);
            if (getAllMethod != null)
            {
                var registrations = getAllMethod.Invoke(null, null);
                if (registrations is IEnumerable<object> registrationList)
                {
                    foreach (var registration in registrationList)
                    {
                        var kernelInfo = await ConvertRegistrationToKernelInfoAsync(registration);
                        if (kernelInfo != null)
                        {
                            kernels.Add(kernelInfo);
                        }
                    }
                }
            }

            // Fallback: Look for the _kernels dictionary directly
            var kernelsField = registryType.GetField("_kernels", BindingFlags.NonPublic | BindingFlags.Static);
            if (kernelsField != null && kernelsField.GetValue(null) is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var kernelInfo = await ConvertRegistrationToKernelInfoAsync(entry.Value);
                    if (kernelInfo != null)
                    {
                        kernels.Add(kernelInfo);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to extract kernels from registry type {registryType.FullName}");
        }

        return kernels;
    }

    private async Task<KernelRegistrationInfo?> ConvertRegistrationToKernelInfoAsync(object? registration)
    {
        await Task.CompletedTask; // Make async
        if (registration == null)
        {
            return null;
        }


        try
        {
            var registrationType = registration.GetType();


            var name = GetPropertyValue<string>(registration, "Name");
            var fullName = GetPropertyValue<string>(registration, "FullName");
            var containingType = GetPropertyValue<Type>(registration, "ContainingType");
            var supportedBackends = GetPropertyValue<Array>(registration, "SupportedBackends");
            var vectorSize = GetPropertyValue<int>(registration, "VectorSize");
            var isParallel = GetPropertyValue<bool>(registration, "IsParallel");

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fullName) || containingType == null)
            {
                _logger.LogWarningMessage("Invalid kernel registration found: missing required properties");
                return null;
            }

            var backendStrings = supportedBackends?.Cast<object>()
                .Select(b => b.ToString() ?? "Unknown")
                .ToArray() ?? ["CPU"];

            return new KernelRegistrationInfo
            {
                Name = name,
                FullName = fullName,
                ContainingType = containingType,
                SupportedBackends = backendStrings,
                VectorSize = vectorSize > 0 ? vectorSize : 8,
                IsParallel = isParallel
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to convert registration to KernelRegistrationInfo");
            return null;
        }
    }

    private async Task<KernelRegistrationInfo?> CreateKernelRegistrationFromMethodAsync(MethodInfo method)
    {
        await Task.CompletedTask; // Make async
        try
        {
            var kernelAttribute = method.GetCustomAttributes(false)
                .FirstOrDefault(attr => attr.GetType().Name == "KernelAttribute");

            if (kernelAttribute == null)
            {
                return null;
            }


            var backends = ExtractSupportedBackends(kernelAttribute);
            var vectorSize = ExtractVectorSize(kernelAttribute);
            var isParallel = ExtractIsParallel(kernelAttribute);

            return new KernelRegistrationInfo
            {
                Name = method.Name,
                FullName = $"{method.DeclaringType?.FullName}.{method.Name}",
                ContainingType = method.DeclaringType ?? typeof(object),
                SupportedBackends = backends,
                VectorSize = vectorSize,
                IsParallel = isParallel
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, $"Failed to create kernel registration from method {method.Name}");
            return null;
        }
    }

    private static T? GetPropertyValue<T>(object obj, string propertyName)
    {
        try
        {
            var property = obj.GetType().GetProperty(propertyName);
            var value = property?.GetValue(obj);


            if (value is T typedValue)
            {
                return typedValue;
            }


            if (value != null && typeof(T).IsAssignableFrom(value.GetType()))
            {

                return (T)value;
            }


            return default;
        }
        catch
        {
            return default;
        }
    }

    private static bool HasKernelAttribute(MethodInfo method)
    {
        return method.GetCustomAttributes(false)
            .Any(attr => attr.GetType().Name == "KernelAttribute");
    }

    private string[] ExtractSupportedBackends(object kernelAttribute)
    {
        try
        {
            var backendsProperty = kernelAttribute.GetType().GetProperty("Backends");
            var backends = backendsProperty?.GetValue(kernelAttribute);


            if (backends != null)
            {
                // Handle enum flags
                var enumValue = (int)backends;
                var supportedBackends = new List<string>();


                if ((enumValue & 1) != 0)
                {
                    supportedBackends.Add("CPU");
                }

                if ((enumValue & 2) != 0)
                {
                    supportedBackends.Add("CUDA");
                }

                if ((enumValue & 4) != 0)
                {
                    supportedBackends.Add("Metal");
                }

                if ((enumValue & 8) != 0)
                {
                    supportedBackends.Add("OpenCL");
                }


                return supportedBackends.ToArray();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract supported backends from kernel attribute");
        }


        return ["CPU"]; // Default fallback
    }

    private static int ExtractVectorSize(object kernelAttribute)
    {
        try
        {
            var vectorSizeProperty = kernelAttribute.GetType().GetProperty("VectorSize");
            var vectorSize = vectorSizeProperty?.GetValue(kernelAttribute);
            return vectorSize is int size ? size : 8;
        }
        catch
        {
            return 8; // Default vector size
        }
    }

    private static bool ExtractIsParallel(object kernelAttribute)
    {
        try
        {
            var isParallelProperty = kernelAttribute.GetType().GetProperty("IsParallel");
            var isParallel = isParallelProperty?.GetValue(kernelAttribute);
            return isParallel is bool parallel && parallel;
        }
        catch
        {
            return true; // Default to parallel
        }
    }

    private IEnumerable<Assembly> GetScannableAssemblies()
    {
        var assemblies = new List<Assembly>();


        try
        {
            // Add current domain assemblies
            assemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !assembly.ReflectionOnly));

            // Filter to only scan relevant assemblies (exclude system assemblies)
            var relevantAssemblies = assemblies
                .Where(assembly =>
                {
                    var name = assembly.GetName().Name ?? "";
                    return !name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
                           !name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
                           !name.StartsWith("netstandard", StringComparison.OrdinalIgnoreCase) &&
                           !name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase);
                })
                .ToList();

            _logger.LogDebugMessage("Found {relevantAssemblies.Count} assemblies to scan for kernels");
            return relevantAssemblies;
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to get scannable assemblies");
            return Array.Empty<Assembly>();
        }
    }
}