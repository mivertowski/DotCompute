// <copyright file="GeneratedKernelAdapter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Core.Kernels;

namespace DotCompute.Refactored.Adapters;

/// <summary>
/// Adapter class that provides backward compatibility for legacy GeneratedKernel types.
/// Converts between different GeneratedKernel implementations found across the codebase.
/// </summary>
public static class GeneratedKernelAdapter
{
    /// <summary>
    /// Converts a legacy record-based GeneratedKernel to UnifiedGeneratedKernel.
    /// </summary>
    public static UnifiedGeneratedKernel FromRecord(dynamic legacyKernel)
    {
        var name = GetPropertyValue<string>(legacyKernel, "Name") ?? "unknown";
        var sourceCode = GetPropertyValue<string>(legacyKernel, "Source", "SourceCode") ?? "";
        var language = GetPropertyValue<string>(legacyKernel, "Language") ?? "C";
        var targetBackend = GetPropertyValue<string>(legacyKernel, "TargetBackend") ?? "CPU";
        var entryPoint = GetPropertyValue<string>(legacyKernel, "EntryPoint") ?? "main";

        var kernel = new UnifiedGeneratedKernel(name, sourceCode, language, targetBackend, entryPoint);

        // Add metadata if present
        var metadata = GetPropertyValue<IDictionary<string, object>>(legacyKernel, "Metadata");
        if (metadata != null)
        {
            kernel.AddMetadata(metadata);
        }

        // Add parameters if present
        var parameters = GetParametersFromLegacy(legacyKernel);
        if (parameters.Any())
        {
            kernel.AddParameters(parameters);
        }

        // Add optimizations if present
        var optimizations = GetPropertyValue<IEnumerable<string>>(legacyKernel, "Optimizations");
        if (optimizations != null)
        {
            kernel.AddOptimizations(optimizations);
        }

        return kernel;
    }

    /// <summary>
    /// Converts a legacy disposable GeneratedKernel to UnifiedGeneratedKernel.
    /// </summary>
    public static UnifiedGeneratedKernel FromDisposable(dynamic legacyKernel)
    {
        var sourceCode = GetPropertyValue<string>(legacyKernel, "SourceCode") ?? "";
        var compiledKernel = GetPropertyValue<ICompiledKernel>(legacyKernel, "CompiledKernel");
        var analysis = GetPropertyValue<IExpressionAnalysisResult>(legacyKernel, "Analysis");
        var memoryManager = GetPropertyValue<IGpuMemoryManager>(legacyKernel, "MemoryManager");

        var name = compiledKernel?.Name ?? "legacy_kernel";
        var kernel = new UnifiedGeneratedKernel(name, sourceCode, "CUDA", "GPU", "kernel_main");

        if (compiledKernel != null)
        {
            kernel.WithCompiledKernel(compiledKernel);
        }

        if (analysis != null)
        {
            kernel.WithAnalysis(analysis);
        }

        if (memoryManager != null)
        {
            kernel.WithMemoryManager(memoryManager);
        }

        return kernel;
    }

    /// <summary>
    /// Converts a legacy sealed GeneratedKernel to UnifiedGeneratedKernel.
    /// </summary>
    public static UnifiedGeneratedKernel FromSealed(dynamic legacyKernel)
    {
        var name = GetPropertyValue<string>(legacyKernel, "Name") ?? "sealed_kernel";
        var source = GetPropertyValue<string>(legacyKernel, "Source") ?? "";
        var language = GetPropertyValue<string>(legacyKernel, "Language") ?? "C";

        return new UnifiedGeneratedKernel(name, source, language);
    }

    /// <summary>
    /// Converts UnifiedGeneratedKernel to a legacy record-like structure.
    /// </summary>
    public static dynamic ToRecord(UnifiedGeneratedKernel unified)
    {
        return new
        {
            Name = unified.Name,
            SourceCode = unified.SourceCode,
            Source = unified.SourceCode, // Alias for backward compatibility
            Parameters = unified.Parameters.Select(p => new
            {
                Name = p.Name,
                Type = p.Type,
                IsPointer = p.IsPointer
            }).ToList(),
            EntryPoint = unified.EntryPoint,
            TargetBackend = unified.TargetBackend,
            Metadata = unified.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Language = unified.Language,
            Optimizations = unified.Optimizations.ToList()
        };
    }

    /// <summary>
    /// Converts UnifiedGeneratedKernel to a legacy disposable structure.
    /// </summary>
    public static dynamic ToDisposable(UnifiedGeneratedKernel unified)
    {
        return new LegacyDisposableKernel
        {
            CompiledKernel = unified.CompiledKernel,
            Analysis = unified.Analysis,
            SourceCode = unified.SourceCode,
            MemoryManager = unified.MemoryManager
        };
    }

    /// <summary>
    /// Converts UnifiedGeneratedKernel to a legacy sealed structure.
    /// </summary>
    public static dynamic ToSealed(UnifiedGeneratedKernel unified)
    {
        return new LegacySealedKernel
        {
            Name = unified.Name,
            Source = unified.SourceCode,
            Language = unified.Language
        };
    }

    /// <summary>
    /// Creates a UnifiedGeneratedKernel from any legacy kernel object.
    /// Uses reflection to extract available properties.
    /// </summary>
    public static UnifiedGeneratedKernel FromAny(object legacyKernel)
    {
        if (legacyKernel == null)
            throw new ArgumentNullException(nameof(legacyKernel));

        var name = GetPropertyValue<string>(legacyKernel, "Name") ?? "converted_kernel";
        var sourceCode = GetPropertyValue<string>(legacyKernel, "Source", "SourceCode") ?? "";
        var language = GetPropertyValue<string>(legacyKernel, "Language") ?? "C";
        var targetBackend = GetPropertyValue<string>(legacyKernel, "TargetBackend") ?? "CPU";
        var entryPoint = GetPropertyValue<string>(legacyKernel, "EntryPoint") ?? "main";

        var kernel = new UnifiedGeneratedKernel(name, sourceCode, language, targetBackend, entryPoint);

        // Try to extract additional properties
        var compiledKernel = GetPropertyValue<ICompiledKernel>(legacyKernel, "CompiledKernel");
        if (compiledKernel != null)
        {
            kernel.WithCompiledKernel(compiledKernel);
        }

        var analysis = GetPropertyValue<IExpressionAnalysisResult>(legacyKernel, "Analysis");
        if (analysis != null)
        {
            kernel.WithAnalysis(analysis);
        }

        var memoryManager = GetPropertyValue<IGpuMemoryManager>(legacyKernel, "MemoryManager");
        if (memoryManager != null)
        {
            kernel.WithMemoryManager(memoryManager);
        }

        // Add metadata if present
        var metadata = GetPropertyValue<IDictionary<string, object>>(legacyKernel, "Metadata");
        if (metadata != null)
        {
            kernel.AddMetadata(metadata);
        }

        // Add parameters
        var parameters = GetParametersFromLegacy(legacyKernel);
        if (parameters.Any())
        {
            kernel.AddParameters(parameters);
        }

        return kernel;
    }

    /// <summary>
    /// Helper method to extract parameters from a legacy kernel object.
    /// </summary>
    private static IEnumerable<IKernelParameter> GetParametersFromLegacy(object legacyKernel)
    {
        var parameters = new List<IKernelParameter>();

        // Try to get parameters as array
        var parameterArray = GetPropertyValue<Array>(legacyKernel, "Parameters");
        if (parameterArray != null)
        {
            foreach (var param in parameterArray)
            {
                var name = GetPropertyValue<string>(param, "Name") ?? "param";
                var type = GetPropertyValue<Type>(param, "Type") ?? typeof(object);
                var isPointer = GetPropertyValue<bool>(param, "IsPointer");

                parameters.Add(new UnifiedKernelParameter(name, type, isPointer));
            }
        }

        // Try to get parameters as list
        var parameterList = GetPropertyValue<IEnumerable<object>>(legacyKernel, "Parameters");
        if (parameterList != null && !parameters.Any())
        {
            foreach (var param in parameterList)
            {
                var name = GetPropertyValue<string>(param, "Name") ?? "param";
                var type = GetPropertyValue<Type>(param, "Type") ?? typeof(object);
                var isPointer = GetPropertyValue<bool>(param, "IsPointer");

                parameters.Add(new UnifiedKernelParameter(name, type, isPointer));
            }
        }

        return parameters;
    }

    /// <summary>
    /// Helper method to get property values with fallbacks.
    /// </summary>
    private static T GetPropertyValue<T>(object obj, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var value = GetPropertyValue(obj, propertyName);
                if (value != null)
                {
                    if (typeof(T).IsAssignableFrom(value.GetType()))
                    {
                        return (T)value;
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
            }
            catch (Exception)
            {
                // Continue to next property name
            }
        }

        return default(T)!;
    }

    /// <summary>
    /// Helper method to get a property value using reflection.
    /// </summary>
    private static object? GetPropertyValue(object obj, string propertyName)
    {
        if (obj == null) return null;

        var type = obj.GetType();
        var property = type.GetProperty(propertyName);
        return property?.GetValue(obj);
    }

    /// <summary>
    /// Legacy disposable kernel implementation for backward compatibility.
    /// </summary>
    private class LegacyDisposableKernel : IDisposable
    {
        public ICompiledKernel? CompiledKernel { get; set; }
        public IExpressionAnalysisResult? Analysis { get; set; }
        public string SourceCode { get; set; } = string.Empty;
        public IGpuMemoryManager? MemoryManager { get; set; }

        public void Dispose()
        {
            CompiledKernel?.Dispose();
            MemoryManager?.Dispose();
        }
    }

    /// <summary>
    /// Legacy sealed kernel implementation for backward compatibility.
    /// </summary>
    private class LegacySealedKernel
    {
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
    }
}

/// <summary>
/// Extension methods to provide seamless conversion between generated kernel types.
/// </summary>
public static class GeneratedKernelExtensions
{
    /// <summary>
    /// Converts any generated kernel object to UnifiedGeneratedKernel.
    /// </summary>
    public static UnifiedGeneratedKernel ToUnified(this object generatedKernel)
    {
        return generatedKernel switch
        {
            UnifiedGeneratedKernel unified => unified,
            null => throw new ArgumentNullException(nameof(generatedKernel)),
            _ => GeneratedKernelAdapter.FromAny(generatedKernel)
        };
    }

    /// <summary>
    /// Converts UnifiedGeneratedKernel to a legacy record format.
    /// </summary>
    public static dynamic ToLegacyRecord(this UnifiedGeneratedKernel unified)
    {
        return GeneratedKernelAdapter.ToRecord(unified);
    }

    /// <summary>
    /// Converts UnifiedGeneratedKernel to a legacy disposable format.
    /// </summary>
    public static dynamic ToLegacyDisposable(this UnifiedGeneratedKernel unified)
    {
        return GeneratedKernelAdapter.ToDisposable(unified);
    }

    /// <summary>
    /// Gets a summary of the kernel for logging and debugging.
    /// </summary>
    public static string GetSummary(this IGeneratedKernel kernel)
    {
        var paramCount = kernel is IExecutableGeneratedKernel exec ? exec.Parameters.Count : 0;
        var isCompiled = kernel is IExecutableGeneratedKernel compiledKernel && compiledKernel.IsCompiled;

        return $"Kernel '{kernel.Name}' [{kernel.Language}] -> {kernel.TargetBackend}, " +
               $"Params: {paramCount}, " +
               $"Status: {(isCompiled ? "Compiled" : "Source Only")}, " +
               $"Entry: {kernel.EntryPoint}";
    }

    /// <summary>
    /// Checks if two kernel instances represent the same kernel.
    /// </summary>
    public static bool IsSameKernel(this IGeneratedKernel kernel1, IGeneratedKernel kernel2)
    {
        return kernel1.Name == kernel2.Name &&
               kernel1.SourceCode == kernel2.SourceCode &&
               kernel1.TargetBackend == kernel2.TargetBackend &&
               kernel1.EntryPoint == kernel2.EntryPoint;
    }
}