// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections;
using System.Reflection;
using DotCompute.Runtime.Logging;
using Microsoft.Extensions.Logging;

namespace DotCompute.Runtime.Services;

/// <summary>
/// Service for discovering and registering kernels from generated source code.
/// This bridges the generated KernelRegistry with the runtime KernelExecutionService.
/// </summary>
public class GeneratedKernelDiscoveryService(ILogger<GeneratedKernelDiscoveryService> logger)
{
    private readonly ILogger<GeneratedKernelDiscoveryService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly List<Assembly> _scannedAssemblies = [];

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
                .Where(t => t.Name == "KernelRegistry" && t.Namespace?.EndsWith(".Generated", StringComparison.Ordinal) == true)
                .ToList();

            foreach (var registryType in kernelRegistryTypes)
            {
                var registryKernels = await ExtractKernelsFromRegistryAsync(registryType);
                kernels.AddRange(registryKernels);
            }

            // Fallback: reflect [Kernel]-attributed methods directly, but ONLY for kernels the
            // generated registry did not already provide. The registry carries the real execution
            // payload (CUDA source, CPU invoker, dimensions, parameters); the reflection fallback
            // produces a bare registration and must not shadow it. Without this dedup the same
            // kernel is registered twice and the empty one wins the short-name index.
            var alreadyRegistered = new HashSet<string>(kernels.Select(k => k.FullName), StringComparer.Ordinal);

            var kernelMethods = assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(HasKernelAttribute)
                .ToList();

            foreach (var method in kernelMethods)
            {
                var kernelInfo = await CreateKernelRegistrationFromMethodAsync(method);
                if (kernelInfo != null && alreadyRegistered.Add(kernelInfo.FullName))
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
        var registryAssembly = registryType.Assembly;

        try
        {
            // Preferred path: GetAllKernels() returns the full KernelMetadata list emitted by the
            // [Kernel] source generator (DotCompute.Generated.KernelRegistry.GetAllKernels()).
            var getAllMethod = registryType.GetMethod("GetAllKernels", BindingFlags.Public | BindingFlags.Static);
            if (getAllMethod != null)
            {
                var registrations = getAllMethod.Invoke(null, null);
                if (registrations is IEnumerable registrationList)
                {
                    foreach (var registration in registrationList)
                    {
                        var kernelInfo = await ConvertRegistrationToKernelInfoAsync(registration, registryAssembly);
                        if (kernelInfo != null)
                        {
                            kernels.Add(kernelInfo);
                        }
                    }

                    return kernels;
                }
            }

            // Fallback: read the private _kernels dictionary directly (older registry shapes).
            var kernelsField = registryType.GetField("_kernels", BindingFlags.NonPublic | BindingFlags.Static);
            if (kernelsField != null && kernelsField.GetValue(null) is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    var kernelInfo = await ConvertRegistrationToKernelInfoAsync(entry.Value, registryAssembly);
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

    private async Task<KernelRegistrationInfo?> ConvertRegistrationToKernelInfoAsync(object? registration, Assembly registryAssembly)
    {
        await Task.CompletedTask; // Make async
        if (registration == null)
        {
            return null;
        }


        try
        {
            var name = GetPropertyValue<string>(registration, "Name");

            // The [Kernel] generator emits 'FullyQualifiedName' and 'Backends'; older/hand-written
            // shapes used 'FullName' and 'SupportedBackends'. Accept either.
            var fullName = GetPropertyValue<string>(registration, "FullyQualifiedName")
                ?? GetPropertyValue<string>(registration, "FullName");

            // The generated registry carries ContainingType as a string (it must not reference
            // runtime/reflection types); resolve it to a System.Type here.
            var containingTypeName = GetPropertyValue<string>(registration, "ContainingType");
            var containingType = ResolveType(containingTypeName, registryAssembly)
                ?? GetPropertyValue<Type>(registration, "ContainingType");

            var backends = GetPropertyValue<Array>(registration, "Backends")
                ?? GetPropertyValue<Array>(registration, "SupportedBackends");
            var vectorSize = GetPropertyValue<int>(registration, "VectorSize");
            var isParallel = GetPropertyValue<bool>(registration, "IsParallel");

            // Execution metadata (issue #182): dimensionality, real CUDA-C, CPU invoker, parameters.
            var dimensions = GetPropertyValue<int>(registration, "Dimensions");
            var cudaSource = GetPropertyValue<string>(registration, "CudaSource");
            var cudaEntryPoint = GetPropertyValue<string>(registration, "CudaEntryPoint");
            var cpuInvoker = GetPropertyValue<Delegate>(registration, "CpuInvoker");
            var parameters = ExtractParameters(registration);

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(fullName) || containingType == null)
            {
                _logger.LogWarningMessage("Invalid kernel registration found: missing required properties");
                return null;
            }

            var backendStrings = backends?.Cast<object>()
                .Select(b => b.ToString() ?? "Unknown")
                .ToArray() ?? ["CPU"];

            return new KernelRegistrationInfo
            {
                Name = name,
                FullName = fullName,
                ContainingType = containingType,
                SupportedBackends = backendStrings,
                VectorSize = vectorSize > 0 ? vectorSize : 8,
                IsParallel = isParallel,
                Dimensions = dimensions > 0 ? dimensions : 1,
                CudaSource = string.IsNullOrEmpty(cudaSource) ? null : cudaSource,
                CudaEntryPoint = string.IsNullOrEmpty(cudaEntryPoint) ? null : cudaEntryPoint,
                CpuInvoker = cpuInvoker,
                Parameters = parameters
            };
        }
        catch (Exception ex)
        {
            _logger.LogErrorMessage(ex, "Failed to convert registration to KernelRegistrationInfo");
            return null;
        }
    }

    /// <summary>
    /// Reflects the generated <c>KernelParam[]</c> into runtime parameter descriptors,
    /// resolving each <c>ElementType</c> string to a <see cref="Type"/>.
    /// </summary>
    private List<KernelParameterInfo> ExtractParameters(object registration)
    {
        var result = new List<KernelParameterInfo>();
        var rawParameters = GetPropertyValue<Array>(registration, "Parameters");
        if (rawParameters == null)
        {
            return result;
        }

        foreach (var rawParam in rawParameters)
        {
            if (rawParam == null)
            {
                continue;
            }

            var paramName = GetPropertyValue<string>(rawParam, "Name") ?? string.Empty;
            var isBuffer = GetPropertyValue<bool>(rawParam, "IsBuffer");
            var isReadOnly = GetPropertyValue<bool>(rawParam, "IsReadOnly");
            var elementTypeName = GetPropertyValue<string>(rawParam, "ElementType");
            result.Add(new KernelParameterInfo
            {
                Name = paramName,
                IsBuffer = isBuffer,
                IsReadOnly = isReadOnly,
                ElementType = ResolveElementType(elementTypeName)
            });
        }

        return result;
    }

    /// <summary>
    /// Resolves a fully-qualified type name (emitted as the kernel's containing type) to a
    /// <see cref="Type"/>, searching the registry assembly first, then all loaded assemblies.
    /// </summary>
    private static Type? ResolveType(string? fullName, Assembly registryAssembly)
    {
        if (string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        foreach (var candidate in NestedTypeNameCandidates(fullName!))
        {
            var type = registryAssembly.GetType(candidate, throwOnError: false) ?? ScanLoadedAssembliesForType(candidate);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    /// <summary>
    /// Searches all non-dynamic loaded assemblies for a type by its fully-qualified name.
    /// Uses <see cref="Assembly.GetType(string, bool)"/> (trim-friendlier than
    /// <c>Type.GetType(string)</c>, which the IL trimmer rejects for non-constant names).
    /// </summary>
    private static Type? ScanLoadedAssembliesForType(string candidate)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
            {
                continue;
            }

            try
            {
                var type = assembly.GetType(candidate, throwOnError: false);
                if (type != null)
                {
                    return type;
                }
            }
            catch (Exception)
            {
                // Some assemblies throw on GetType during reflection; skip and continue.
            }
        }

        return null;
    }

    /// <summary>
    /// Yields the dotted name plus nested-type variants (rightmost dots replaced by '+'),
    /// since the generator emits nested containing types with '.' separators.
    /// </summary>
    private static IEnumerable<string> NestedTypeNameCandidates(string fullName)
    {
        yield return fullName;

        var chars = fullName.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            if (chars[i] == '.')
            {
                chars[i] = '+';
                yield return new string(chars);
            }
        }
    }

    /// <summary>
    /// Resolves a kernel parameter element-type string (C# keyword such as "float", or a
    /// fully-qualified type name) to a <see cref="Type"/>.
    /// </summary>
    private static Type? ResolveElementType(string? elementTypeName)
    {
        if (string.IsNullOrEmpty(elementTypeName))
        {
            return null;
        }

        return elementTypeName switch
        {
            "float" => typeof(float),
            "double" => typeof(double),
            "int" => typeof(int),
            "uint" => typeof(uint),
            "long" => typeof(long),
            "ulong" => typeof(ulong),
            "short" => typeof(short),
            "ushort" => typeof(ushort),
            "byte" => typeof(byte),
            "sbyte" => typeof(sbyte),
            "bool" => typeof(bool),
            "char" => typeof(char),
            _ => ScanLoadedAssembliesForType(elementTypeName!)
        };
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


                return [.. supportedBackends];
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
