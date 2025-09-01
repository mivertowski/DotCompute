// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using DotCompute.Generators.Models;
using DotCompute.Generators.Utils;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DotCompute.Generators.Backend.CPU;

/// <summary>
/// Abstract base class for CPU code generators providing common functionality.
/// </summary>
public abstract class CpuCodeGeneratorBase
{
    #region Constants
    
    protected const int SmallArrayThreshold = 32;
    protected const int MinVectorSize = 32;
    protected const int MinAvx512Size = 64;
    protected const int DefaultChunkSize = 1024;
    protected const int Avx2VectorSize = 8;  // 256-bit / 32-bit float
    protected const int Avx512VectorSize = 16; // 512-bit / 32-bit float
    
    protected const int BaseIndentLevel = 2;
    protected const int MethodBodyIndentLevel = 3;
    protected const int LoopBodyIndentLevel = 4;
    
    #endregion
    
    #region Fields
    
    protected readonly string MethodName;
    protected readonly IReadOnlyList<KernelParameter> Parameters;
    protected readonly MethodDeclarationSyntax MethodSyntax;
    protected readonly VectorizationInfo VectorizationInfo;
    
    #endregion
    
    #region Constructor
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CpuCodeGeneratorBase"/> class.
    /// </summary>
    protected CpuCodeGeneratorBase(
        string methodName,
        IReadOnlyList<KernelParameter> parameters,
        MethodDeclarationSyntax methodSyntax,
        VectorizationInfo vectorizationInfo)
    {
        MethodName = methodName ?? throw new ArgumentNullException(nameof(methodName));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        MethodSyntax = methodSyntax ?? throw new ArgumentNullException(nameof(methodSyntax));
        VectorizationInfo = vectorizationInfo ?? throw new ArgumentNullException(nameof(vectorizationInfo));
        
        ValidateParameters();
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// Generates the implementation for this specific generator.
    /// </summary>
    public abstract void Generate(StringBuilder sb);
    
    #endregion
    
    #region Protected Methods
    
    /// <summary>
    /// Validates all kernel parameters.
    /// </summary>
    protected virtual void ValidateParameters()
    {
        foreach (var parameter in Parameters)
        {
            parameter.Validate();
        }
    }
    
    /// <summary>
    /// Generates method documentation comments.
    /// </summary>
    protected static void GenerateMethodDocumentation(StringBuilder sb, string summary, string? remarks = null)
    {
        _ = sb.AppendLine("        /// <summary>");
        _ = sb.AppendLine($"        /// {summary}");
        _ = sb.AppendLine("        /// </summary>");
        
        if (!string.IsNullOrEmpty(remarks))
        {
            _ = sb.AppendLine("        /// <remarks>");
            _ = sb.AppendLine($"        /// {remarks}");
            _ = sb.AppendLine("        /// </remarks>");
        }
    }
    
    /// <summary>
    /// Generates a method signature.
    /// </summary>
    protected void GenerateMethodSignature(StringBuilder sb, string methodName, bool isPrivate,
        bool includeRange = false, bool includeLength = false)
    {
        _ = sb.AppendLine("        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        _ = sb.Append($"        {(isPrivate ? "private" : "public")} static void {methodName}(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.GetDeclaration())));
        
        if (includeRange)
        {
            _ = sb.Append(", int start, int end");
        }
        else if (includeLength)
        {
            _ = sb.Append(", int length");
        }
        
        _ = sb.AppendLine(")");
    }
    
    /// <summary>
    /// Generates a method body with the provided content generator.
    /// </summary>
    protected static void GenerateMethodBody(StringBuilder sb, Action contentGenerator)
    {
        _ = sb.AppendLine("        {");
        contentGenerator();
        _ = sb.AppendLine("        }");
        _ = sb.AppendLine();
    }
    
    /// <summary>
    /// Generates remainder handling code.
    /// </summary>
    protected void GenerateRemainderHandling(StringBuilder sb, string fallbackMethod)
    {
        _ = sb.AppendLine("            // Process remainder");
        _ = sb.AppendLine("            if (alignedEnd < end)");
        _ = sb.AppendLine("            {");
        _ = sb.AppendLine($"                {fallbackMethod}(");
        _ = sb.Append(string.Join(", ", Parameters.Select(p => p.Name)));
        _ = sb.AppendLine(", alignedEnd, end);");
        _ = sb.AppendLine("            }");
    }
    
    #endregion
}