// <copyright file="OperatorTypeAdapter.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Core.Analysis;
namespace DotCompute.Refactored.Adapters;
{
/// <summary>
/// Legacy OperatorType enum from Pipeline analysis for compatibility.
/// </summary>
public enum LegacyPipelineOperatorType
{
    Unknown,
    Filter,
    Projection,
    Aggregation,
    Sort,
    Group,
    Join,
    Mathematical,
    Conversion,
    Memory,
    Reduction,
    Transformation,
    MethodCall,
    Custom
}
/// Legacy OperatorType enum from Compilation analysis for compatibility.
public enum LegacyCompilationOperatorType
    {
    Arithmetic,
    Logical,
    Comparison,
    Conditional,
    Assignment,
/// Adapter class that provides conversion between legacy OperatorType enums and UnifiedOperatorType.
public static class OperatorTypeAdapter
    {
    private static readonly Dictionary<LegacyPipelineOperatorType, UnifiedOperatorType> PipelineToUnifiedMapping = new()
    {
        [LegacyPipelineOperatorType.Unknown] = UnifiedOperatorType.Unknown,
        [LegacyPipelineOperatorType.Filter] = UnifiedOperatorType.Filter,
        [LegacyPipelineOperatorType.Projection] = UnifiedOperatorType.Projection,
        [LegacyPipelineOperatorType.Aggregation] = UnifiedOperatorType.Aggregation,
        [LegacyPipelineOperatorType.Sort] = UnifiedOperatorType.Sort,
        [LegacyPipelineOperatorType.Group] = UnifiedOperatorType.Group,
        [LegacyPipelineOperatorType.Join] = UnifiedOperatorType.Join,
        [LegacyPipelineOperatorType.Mathematical] = UnifiedOperatorType.Mathematical,
        [LegacyPipelineOperatorType.Conversion] = UnifiedOperatorType.Conversion,
        [LegacyPipelineOperatorType.Memory] = UnifiedOperatorType.Memory,
        [LegacyPipelineOperatorType.Reduction] = UnifiedOperatorType.Reduction,
        [LegacyPipelineOperatorType.Transformation] = UnifiedOperatorType.Transformation,
        [LegacyPipelineOperatorType.MethodCall] = UnifiedOperatorType.MethodCall,
        [LegacyPipelineOperatorType.Custom] = UnifiedOperatorType.Custom
    };
    private static readonly Dictionary<LegacyCompilationOperatorType, UnifiedOperatorType> CompilationToUnifiedMapping = new()
        [LegacyCompilationOperatorType.Unknown] = UnifiedOperatorType.Unknown,
        [LegacyCompilationOperatorType.Arithmetic] = UnifiedOperatorType.Arithmetic,
        [LegacyCompilationOperatorType.Logical] = UnifiedOperatorType.Logical,
        [LegacyCompilationOperatorType.Comparison] = UnifiedOperatorType.Comparison,
        [LegacyCompilationOperatorType.Conditional] = UnifiedOperatorType.Conditional,
        [LegacyCompilationOperatorType.Assignment] = UnifiedOperatorType.Assignment,
        [LegacyCompilationOperatorType.Filter] = UnifiedOperatorType.Filter,
        [LegacyCompilationOperatorType.Projection] = UnifiedOperatorType.Projection,
        [LegacyCompilationOperatorType.Aggregation] = UnifiedOperatorType.Aggregation,
        [LegacyCompilationOperatorType.Sort] = UnifiedOperatorType.Sort,
        [LegacyCompilationOperatorType.Group] = UnifiedOperatorType.Group,
        [LegacyCompilationOperatorType.Join] = UnifiedOperatorType.Join,
        [LegacyCompilationOperatorType.Mathematical] = UnifiedOperatorType.Mathematical,
        [LegacyCompilationOperatorType.Conversion] = UnifiedOperatorType.Conversion,
        [LegacyCompilationOperatorType.Memory] = UnifiedOperatorType.Memory,
        [LegacyCompilationOperatorType.Reduction] = UnifiedOperatorType.Reduction,
        [LegacyCompilationOperatorType.Transformation] = UnifiedOperatorType.Transformation,
        [LegacyCompilationOperatorType.MethodCall] = UnifiedOperatorType.MethodCall,
        [LegacyCompilationOperatorType.Custom] = UnifiedOperatorType.Custom
    private static readonly Dictionary<UnifiedOperatorType, LegacyPipelineOperatorType> UnifiedToPipelineMapping =
        PipelineToUnifiedMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    private static readonly Dictionary<UnifiedOperatorType, LegacyCompilationOperatorType> UnifiedToCompilationMapping =
        CompilationToUnifiedMapping.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    /// <summary>
    /// Converts a legacy pipeline OperatorType to UnifiedOperatorType.
    /// </summary>
    public static UnifiedOperatorType FromPipeline(LegacyPipelineOperatorType legacyType)
        {
        return PipelineToUnifiedMapping.TryGetValue(legacyType, out var unified) ? unified : UnifiedOperatorType.Unknown;
    }
    /// Converts a legacy compilation OperatorType to UnifiedOperatorType.
    public static UnifiedOperatorType FromCompilation(LegacyCompilationOperatorType legacyType)
        {
        return CompilationToUnifiedMapping.TryGetValue(legacyType, out var unified) ? unified : UnifiedOperatorType.Unknown;
    /// Converts UnifiedOperatorType to legacy pipeline OperatorType.
    public static LegacyPipelineOperatorType ToPipeline(UnifiedOperatorType unifiedType)
        if (UnifiedToPipelineMapping.TryGetValue(unifiedType, out var legacy))
        {
            return legacy;
        }
        // Try to find the closest match by category
        var category = unifiedType.GetCategory();
        return UnifiedToPipelineMapping.TryGetValue(category, out var categoryLegacy) ? categoryLegacy : LegacyPipelineOperatorType.Unknown;
    /// Converts UnifiedOperatorType to legacy compilation OperatorType.
    public static LegacyCompilationOperatorType ToCompilation(UnifiedOperatorType unifiedType)
        if (UnifiedToCompilationMapping.TryGetValue(unifiedType, out var legacy))
        return UnifiedToCompilationMapping.TryGetValue(category, out var categoryLegacy) ? categoryLegacy : LegacyCompilationOperatorType.Unknown;
    /// Converts a string representation to UnifiedOperatorType.
    public static UnifiedOperatorType FromString(string operatorTypeString)
        if (string.IsNullOrEmpty(operatorTypeString))
            return UnifiedOperatorType.Unknown;
        // Try direct enum parsing first
        if (Enum.TryParse<UnifiedOperatorType>(operatorTypeString, true, out var directResult))
            return directResult;
        // Try legacy pipeline enum parsing
        if (Enum.TryParse<LegacyPipelineOperatorType>(operatorTypeString, true, out var pipelineResult))
            return FromPipeline(pipelineResult);
        // Try legacy compilation enum parsing
        if (Enum.TryParse<LegacyCompilationOperatorType>(operatorTypeString, true, out var compilationResult))
            return FromCompilation(compilationResult);
        // Try fuzzy matching
        return FuzzyMatch(operatorTypeString);
    /// Converts any enum value to UnifiedOperatorType using reflection.
    public static UnifiedOperatorType FromEnum(Enum operatorTypeEnum)
        if (operatorTypeEnum == null)
        var enumType = operatorTypeEnum.GetType();
        var enumValue = operatorTypeEnum.ToString();
        // Check if it's already UnifiedOperatorType
        if (enumType == typeof(UnifiedOperatorType))
            return (UnifiedOperatorType)operatorTypeEnum;
        // Try string conversion
        return FromString(enumValue);
    /// Gets all possible mappings between legacy types and unified types.
    public static Dictionary<string, UnifiedOperatorType> GetAllMappings()
        var mappings = new Dictionary<string, UnifiedOperatorType>();
        // Add pipeline mappings
        foreach (var kvp in PipelineToUnifiedMapping)
            mappings[$"Pipeline.{kvp.Key}"] = kvp.Value;
        // Add compilation mappings
        foreach (var kvp in CompilationToUnifiedMapping)
            mappings[$"Compilation.{kvp.Key}"] = kvp.Value;
        // Add unified mappings
        foreach (var value in Enum.GetValues<UnifiedOperatorType>())
            mappings[$"Unified.{value}"] = value;
        return mappings;
    /// Creates a migration report showing which legacy types map to which unified types.
    public static string CreateMigrationReport()
        var report = new System.Text.StringBuilder();
        report.AppendLine("OperatorType Migration Report");
        report.AppendLine("============================");
        report.AppendLine();
        report.AppendLine("Pipeline OperatorType → Unified OperatorType:");
        report.AppendLine("-----------------------------------------------");
            report.AppendLine($"  {kvp.Key,-20} → {kvp.Value}");
        report.AppendLine("Compilation OperatorType → Unified OperatorType:");
        report.AppendLine("New Unified OperatorTypes (not in legacy):");
        report.AppendLine("------------------------------------------");
        var legacyTypes = PipelineToUnifiedMapping.Values.Concat(CompilationToUnifiedMapping.Values).Distinct().ToHashSet();
        var unifiedTypes = Enum.GetValues<UnifiedOperatorType>();
        
        foreach (var unifiedType in unifiedTypes)
            if (!legacyTypes.Contains(unifiedType))
            {
                report.AppendLine($"  {unifiedType}");
            }
        return report.ToString();
    /// Performs fuzzy matching to find the closest UnifiedOperatorType.
    private static UnifiedOperatorType FuzzyMatch(string input)
        var allValues = Enum.GetValues<UnifiedOperatorType>();
        var inputLower = input.ToLowerInvariant();
        // Look for partial matches
        foreach (var value in allValues)
            var valueLower = value.ToString().ToLowerInvariant();
            if (valueLower.Contains(inputLower) || inputLower.Contains(valueLower))
                return value;
        // Look for specific patterns
        return inputLower switch
            var s when s.Contains("add") || s.Contains("plus") => UnifiedOperatorType.Add,
            var s when s.Contains("sub") || s.Contains("minus") => UnifiedOperatorType.Subtract,
            var s when s.Contains("mul") || s.Contains("times") => UnifiedOperatorType.Multiply,
            var s when s.Contains("div") => UnifiedOperatorType.Divide,
            var s when s.Contains("and") => UnifiedOperatorType.LogicalAnd,
            var s when s.Contains("or") => UnifiedOperatorType.LogicalOr,
            var s when s.Contains("not") => UnifiedOperatorType.LogicalNot,
            var s when s.Contains("equal") => UnifiedOperatorType.Equal,
            var s when s.Contains("greater") => UnifiedOperatorType.GreaterThan,
            var s when s.Contains("less") => UnifiedOperatorType.LessThan,
            var s when s.Contains("where") => UnifiedOperatorType.Where,
            var s when s.Contains("select") => UnifiedOperatorType.Select,
            var s when s.Contains("sum") => UnifiedOperatorType.Sum,
            var s when s.Contains("count") => UnifiedOperatorType.Count,
            var s when s.Contains("order") => UnifiedOperatorType.OrderBy,
            var s when s.Contains("group") => UnifiedOperatorType.GroupBy,
            var s when s.Contains("join") => UnifiedOperatorType.Join,
            _ => UnifiedOperatorType.Unknown
        };
/// Extension methods for seamless operator type conversion.
public static class OperatorTypeExtensions
    {
    /// Converts any operator type to UnifiedOperatorType.
    public static UnifiedOperatorType ToUnified(this Enum operatorType)
        {
        return OperatorTypeAdapter.FromEnum(operatorType);
    /// Converts a string to UnifiedOperatorType.
    public static UnifiedOperatorType ToUnified(this string operatorTypeString)
        return OperatorTypeAdapter.FromString(operatorTypeString);
    /// Converts UnifiedOperatorType to a legacy pipeline type.
    public static LegacyPipelineOperatorType ToLegacyPipeline(this UnifiedOperatorType unifiedType)
        return OperatorTypeAdapter.ToPipeline(unifiedType);
    /// Converts UnifiedOperatorType to a legacy compilation type.
    public static LegacyCompilationOperatorType ToLegacyCompilation(this UnifiedOperatorType unifiedType)
        return OperatorTypeAdapter.ToCompilation(unifiedType);
    /// Gets a human-readable description of the operator type.
    public static string GetDescription(this UnifiedOperatorType operatorType)
        return operatorType switch
            UnifiedOperatorType.Add => "Addition operation",
            UnifiedOperatorType.Subtract => "Subtraction operation",
            UnifiedOperatorType.Multiply => "Multiplication operation",
            UnifiedOperatorType.Divide => "Division operation",
            UnifiedOperatorType.Where => "Filtering operation (Where clause)",
            UnifiedOperatorType.Select => "Projection operation (Select clause)",
            UnifiedOperatorType.Sum => "Summation aggregation",
            UnifiedOperatorType.Count => "Count aggregation",
            UnifiedOperatorType.OrderBy => "Sorting operation",
            UnifiedOperatorType.GroupBy => "Grouping operation",
            UnifiedOperatorType.Join => "Join operation",
            UnifiedOperatorType.Mathematical => "Mathematical function",
            UnifiedOperatorType.Comparison => "Comparison operation",
            UnifiedOperatorType.Logical => "Logical operation",
            UnifiedOperatorType.MethodCall => "Method call operation",
            UnifiedOperatorType.Custom => "Custom user-defined operation",
            _ => $"{operatorType} operation"
