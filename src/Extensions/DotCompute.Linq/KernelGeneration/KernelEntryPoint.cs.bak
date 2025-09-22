// <copyright file="KernelEntryPoint.cs" company="DotCompute Project">
// Copyright (c) 2025 DotCompute Project Contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using DotCompute.Linq.Operators.Parameters;
namespace DotCompute.Linq.KernelGeneration;
/// <summary>
/// Defines the execution model for kernels.
/// </summary>
public enum KernelExecutionModel
{
    /// <summary>Sequential execution model.</summary>
    Sequential,
    /// <summary>Data parallel execution model.</summary>
    DataParallel,
    /// <summary>Task parallel execution model.</summary>
    TaskParallel,
    /// <summary>GPU execution model.</summary>
    GPU,
    /// <summary>Vectorized execution model.</summary>
    Vectorized
}
/// Represents a kernel entry point with metadata for compilation and execution.
/// Contains all information needed to call a compiled kernel function.
public sealed class KernelEntryPoint : IEquatable<KernelEntryPoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelEntryPoint"/> class.
    /// </summary>
    /// <param name="functionName">The name of the kernel function.</param>
    /// <param name="parameters">The kernel parameters.</param>
    /// <param name="returnType">The return type of the kernel function.</param>
    /// <exception cref="ArgumentNullException">Thrown when functionName or returnType is null.</exception>
    /// <exception cref="ArgumentException">Thrown when functionName is empty or whitespace.</exception>
    public KernelEntryPoint(
        string functionName,
        IReadOnlyList<KernelParameter> parameters,
        Type returnType)
    {
        if (string.IsNullOrWhiteSpace(functionName))
        {
            throw new ArgumentException("Function name cannot be null or whitespace.", nameof(functionName));
        }
        FunctionName = functionName;
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        ReturnType = returnType ?? throw new ArgumentNullException(nameof(returnType));
        Signature = GenerateSignature();
    }
    /// Initializes a new instance of the <see cref="KernelEntryPoint"/> class with execution model.
    /// <param name="kernelName">The name of the kernel.</param>
    /// <param name="methodName">The name of the entry method.</param>
    /// <param name="executionModel">The execution model for the kernel.</param>
    public KernelEntryPoint(string kernelName, string methodName, KernelExecutionModel executionModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kernelName);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        FunctionName = methodName;
        Parameters = new List<KernelParameter>();
        ReturnType = typeof(void);
        Signature = $"{methodName}()";
        ExecutionModel = executionModel;
        KernelName = kernelName;
    }
    /// Gets the name of the kernel function.
    public string FunctionName { get; }
    /// Gets the kernel parameters in order.
    public IReadOnlyList<KernelParameter> Parameters { get; }
    /// Gets the return type of the kernel function.
    public Type ReturnType { get; }
    /// Gets the function signature string for compilation.
    public string Signature { get; }
    /// Gets the execution model for the kernel.
    public KernelExecutionModel ExecutionModel { get; private set; }
    /// Gets the kernel name.
    public string KernelName { get; private set; } = string.Empty;
    /// Gets the total number of parameters.
    public int ParameterCount => Parameters.Count;
    /// Gets parameters filtered by direction.
    /// <param name="direction">The parameter direction to filter by.</param>
    /// <returns>Parameters with the specified direction.</returns>
    public IEnumerable<KernelParameter> GetParametersByDirection(ParameterDirection direction)
    {
        return Parameters.Where(p => p.Direction == direction);
    }
    /// Gets input parameters (In and InOut).
    /// <returns>Input parameters.</returns>
    public IEnumerable<KernelParameter> GetInputParameters()
    {
        return Parameters.Where(p => p.Direction == ParameterDirection.In || p.Direction == ParameterDirection.InOut);
    }
    /// Gets output parameters (Out and InOut).
    /// <returns>Output parameters.</returns>
    public IEnumerable<KernelParameter> GetOutputParameters()
    {
        return Parameters.Where(p => p.Direction == ParameterDirection.Out || p.Direction == ParameterDirection.InOut);
    }
    /// Validates that all required parameters are provided.
    /// <param name="providedParameters">The parameters provided for execution.</param>
    /// <returns>True if all required parameters are provided; otherwise, false.</returns>
    public bool ValidateParameters(IReadOnlyDictionary<string, object> providedParameters)
    {
        if (providedParameters == null)
            return Parameters.Count == 0;
        foreach (var parameter in Parameters)
        {
            if (!providedParameters.ContainsKey(parameter.Name))
            {
                return false;
            }
            var providedValue = providedParameters[parameter.Name];
            if (providedValue != null && !parameter.Type.IsAssignableFrom(providedValue.GetType()))
            {
                return false;
            }
        }
        return true;
    }
    /// Generates the function signature string.
    /// <returns>The function signature.</returns>
    private string GenerateSignature()
    {
        var returnTypeName = GetTypeName(ReturnType);
        var parameterStrings = Parameters.Select(p => $"{GetDirectionString(p.Direction)} {GetTypeName(p.Type)} {p.Name}");
        return $"{returnTypeName} {FunctionName}({string.Join(", ", parameterStrings)})";
    }
    /// Gets a readable type name for the signature.
    /// <param name="type">The type to get the name for.</param>
    /// <returns>The type name.</returns>
    private static string GetTypeName(Type type)
    {
        if (type == typeof(void))
            return "void";
        if (type == typeof(int))
            return "int";
        if (type == typeof(float))
            return "float";
        if (type == typeof(double))
            return "double";
        if (type == typeof(bool))
            return "bool";
        if (type.IsArray)
            return $"{GetTypeName(type.GetElementType()!)}[]";
        if (type.IsGenericType)
        {
            var genericName = type.Name.Split('`')[0];
            var genericArgs = string.Join(", ", type.GetGenericArguments().Select(GetTypeName));
            return $"{genericName}<{genericArgs}>";
        }
        return type.Name;
    }
    /// Gets the direction string for parameter signatures.
    /// <param name="direction">The parameter direction.</param>
    /// <returns>The direction string.</returns>
    private static string GetDirectionString(ParameterDirection direction)
    {
        return direction switch
        {
            ParameterDirection.In => "in",
            ParameterDirection.Out => "out",
            ParameterDirection.InOut => "inout",
            _ => string.Empty
        };
    }
    /// <inheritdoc/>
    public bool Equals(KernelEntryPoint? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return FunctionName == other.FunctionName &&
               ReturnType == other.ReturnType &&
               Parameters.Count == other.Parameters.Count &&
               Parameters.SequenceEqual(other.Parameters, new KernelParameterComparer());
    }
    public override bool Equals(object? obj) => obj is KernelEntryPoint other && Equals(other);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FunctionName);
        hash.Add(ReturnType);
        hash.Add(Parameters.Count);
        foreach (var parameter in Parameters)
        {
            hash.Add(parameter.Name);
            hash.Add(parameter.Type);
            hash.Add(parameter.Direction);
        }
        return hash.ToHashCode();
    }
    public override string ToString() => Signature;
    /// Comparer for kernel parameters used in equality checks.
    private sealed class KernelParameterComparer : IEqualityComparer<KernelParameter>
    {
        public bool Equals(KernelParameter? x, KernelParameter? y)
        {
            if (ReferenceEquals(x, y))
                return true;
            if (x is null || y is null)
                return false;
            return x.Name == y.Name &&
                   x.Type == y.Type &&
                   x.Direction == y.Direction;
        }

        public int GetHashCode(KernelParameter obj)
        {
            return HashCode.Combine(obj.Name, obj.Type, obj.Direction);
        }
    }
}
