// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DotCompute.Generators.Models.Kernel;

namespace DotCompute.Generators.Kernel;

/// <summary>
/// Optimizes kernels for register pressure and implements spilling strategies.
/// </summary>
internal sealed class RegisterSpillingOptimizer
{
    private readonly KernelMethodInfo _kernelInfo;
    private readonly SemanticModel _semanticModel;
    private int _estimatedRegisterCount;
    private readonly List<string> _spillableVariables;
    private readonly Dictionary<string, int> _variableLifetimes;

    public RegisterSpillingOptimizer(KernelMethodInfo kernelInfo, SemanticModel semanticModel)
    {
        _kernelInfo = kernelInfo ?? throw new ArgumentNullException(nameof(kernelInfo));
        _semanticModel = semanticModel ?? throw new ArgumentNullException(nameof(semanticModel));
        _spillableVariables = [];
        _variableLifetimes = [];
        AnalyzeRegisterPressure();
    }

    /// <summary>
    /// Gets the estimated register count for the kernel.
    /// </summary>
    public int EstimatedRegisterCount => _estimatedRegisterCount;

    /// <summary>
    /// Gets whether the kernel has high register pressure.
    /// </summary>
    public bool HasHighRegisterPressure => _estimatedRegisterCount > 64;

    /// <summary>
    /// Gets the recommended maximum registers setting.
    /// </summary>
    public int RecommendedMaxRegisters
    {
        get
        {
            if (_estimatedRegisterCount <= 32)
            {
                return 32;
            }


            if (_estimatedRegisterCount <= 64)
            {
                return 64;
            }


            if (_estimatedRegisterCount <= 96)
            {
                return 96;
            }


            if (_estimatedRegisterCount <= 128)
            {
                return 128;
            }


            return 255; // Maximum allowed
        }
    }

    /// <summary>
    /// Analyzes the kernel for register pressure.
    /// </summary>
    private void AnalyzeRegisterPressure()
    {
        if (_kernelInfo.MethodDeclaration?.Body == null)
        {
            return;
        }


        var body = _kernelInfo.MethodDeclaration.Body;

        // Count local variables

        var localVariables = body.DescendantNodes()
            .OfType<LocalDeclarationStatementSyntax>()
            .SelectMany(ld => ld.Declaration.Variables)
            .ToList();

        // Count parameters (each parameter needs registers)
        var parameterCount = _kernelInfo.Parameters.Count;

        // Estimate register usage
        _estimatedRegisterCount = parameterCount * 2; // Parameters typically need 2 registers each

        // Analyze each local variable
        foreach (var variable in localVariables)
        {
            var variableName = variable.Identifier.Text;
            var declaration = variable.Parent as VariableDeclarationSyntax;
            var typeInfo = declaration != null ? _semanticModel.GetTypeInfo(declaration.Type) : default;

            // Estimate registers based on type

            var registersNeeded = typeInfo.Type != null ? EstimateRegistersForType(typeInfo.Type) : 1;
            _estimatedRegisterCount += registersNeeded;

            // Track variable lifetime for spilling decisions
            AnalyzeVariableLifetime(variable, variableName);

            // Mark long-lived variables as spillable

            if (_variableLifetimes.ContainsKey(variableName) && _variableLifetimes[variableName] > 10)
            {
                _spillableVariables.Add(variableName);
            }
        }

        // Add overhead for control flow
        var controlFlowStatements = body.DescendantNodes()
            .Count(n => n is IfStatementSyntax || n is WhileStatementSyntax ||

                       n is ForStatementSyntax || n is DoStatementSyntax);


        _estimatedRegisterCount += controlFlowStatements * 3; // Control flow typically needs extra registers

        // Add overhead for mathematical operations
        var mathOperations = body.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Count(IsMathOperation);


        _estimatedRegisterCount += mathOperations * 2; // Math operations need temporary registers
    }

    /// <summary>
    /// Estimates the number of registers needed for a type.
    /// </summary>
    private static int EstimateRegistersForType(ITypeSymbol type)
    {
        if (type == null)
        {
            return 1;
        }


        return type.SpecialType switch
        {
            SpecialType.System_Boolean => 1,
            SpecialType.System_Byte => 1,
            SpecialType.System_Int16 => 1,
            SpecialType.System_Int32 => 1,
            SpecialType.System_Int64 => 2,
            SpecialType.System_Single => 1,
            SpecialType.System_Double => 2,
            _ => type.IsValueType ? 2 : 1 // Structs typically need more registers
        };
    }

    /// <summary>
    /// Analyzes the lifetime of a variable for spilling decisions.
    /// </summary>
    private void AnalyzeVariableLifetime(VariableDeclaratorSyntax variable, string variableName)
    {
        var block = variable.FirstAncestorOrSelf<BlockSyntax>();
        if (block == null)
        {
            return;
        }


        var references = block.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(id => id.Identifier.Text == variableName)
            .ToList();

        _variableLifetimes[variableName] = references.Count;
    }

    /// <summary>
    /// Checks if an invocation is a math operation.
    /// </summary>
    private static bool IsMathOperation(InvocationExpressionSyntax invocation)
    {
        var expression = invocation.Expression.ToString();
        return expression.Contains("Math.") ||

               expression.Contains("MathF.") ||
               expression.Contains("Sin") ||
               expression.Contains("Cos") ||
               expression.Contains("Exp") ||
               expression.Contains("Log") ||
               expression.Contains("Sqrt");
    }

    /// <summary>
    /// Generates optimized CUDA code with register spilling strategies.
    /// </summary>
    public string GenerateOptimizedCode(string baseCode)
    {
        var sb = new StringBuilder();

        // Add spilling configuration if needed
        if (HasHighRegisterPressure)
        {
            _ = sb.AppendLine("// High register pressure detected - implementing spilling strategies");
            _ = sb.AppendLine($"// Estimated registers: {_estimatedRegisterCount}");
            _ = sb.AppendLine($"// Recommended max: {RecommendedMaxRegisters}");
            _ = sb.AppendLine();

            // Generate shared memory spill buffer
            if (_spillableVariables.Count > 0)
            {
                _ = sb.AppendLine("// Shared memory buffer for spilling long-lived variables");
                _ = sb.AppendLine($"__shared__ float __spill_buffer[{_spillableVariables.Count * 256}];");
                _ = sb.AppendLine("#define SPILL_OFFSET(tid) ((tid) * " + _spillableVariables.Count + ")");
                _ = sb.AppendLine();

                // Generate spill/reload macros for each variable
                for (var i = 0; i < _spillableVariables.Count; i++)
                {
                    var varName = _spillableVariables[i];
                    _ = sb.AppendLine($"#define SPILL_{varName}(val) __spill_buffer[SPILL_OFFSET(threadIdx.x) + {i}] = (val)");
                    _ = sb.AppendLine($"#define RELOAD_{varName}() __spill_buffer[SPILL_OFFSET(threadIdx.x) + {i}]");
                }
                _ = sb.AppendLine();
            }

            // Add compiler hints
            _ = sb.AppendLine("// Compiler hints for register optimization");
            _ = sb.AppendLine("#pragma unroll 1  // Disable loop unrolling to save registers");
            _ = sb.AppendLine("#if __CUDA_ARCH__ >= 750");
            _ = sb.AppendLine("  // Use shared memory for spilling on Turing+");
            _ = sb.AppendLine("  #pragma nv_diag_suppress 177  // Suppress spilling warnings");
            _ = sb.AppendLine("#endif");
            _ = sb.AppendLine();
        }

        // Add the base code with potential modifications
        _ = sb.Append(ModifyCodeForSpilling(baseCode));

        return sb.ToString();
    }

    /// <summary>
    /// Modifies the generated code to use spilling where appropriate.
    /// </summary>
    private string ModifyCodeForSpilling(string code)
    {
        if (!HasHighRegisterPressure || _spillableVariables.Count == 0)
        {

            return code;
        }

        // Use a proper code transformation approach with AST-aware modifications

        var codeTransformer = new SpillCodeTransformer(_spillableVariables);
        return codeTransformer.Transform(code);
    }

    /// <summary>
    /// Inner class for sophisticated code transformation with spilling.
    /// </summary>
    private sealed class SpillCodeTransformer
    {
        private readonly HashSet<string> _spillVars;
        private readonly Dictionary<string, SpillInfo> _spillInfoMap;
        private readonly StringBuilder _output;
        private int _currentLine;
        private readonly HashSet<string> _processedDeclarations;
        private readonly Stack<BlockContext> _blockStack;

        private sealed class SpillInfo
        {
            public int SpillSlot { get; set; }
            public string Type { get; set; } = "float";
            public int LastUse { get; set; }
            public bool IsSpilled { get; set; }
        }

        private sealed class BlockContext
        {
            public int StartLine { get; set; }
            public HashSet<string> LocalVars { get; } = [];
            public bool IsLoop { get; set; }
        }

        public SpillCodeTransformer(List<string> spillableVariables)
        {
            _spillVars = new HashSet<string>(spillableVariables);
            _spillInfoMap = [];
            _output = new StringBuilder();
            _processedDeclarations = [];
            _blockStack = new Stack<BlockContext>();

            // Assign spill slots

            var slot = 0;
            foreach (var var in spillableVariables)
            {
                _spillInfoMap[var] = new SpillInfo { SpillSlot = slot++ };
            }
        }

        public string Transform(string code)
        {
            var lines = code.Split('\n');
            _currentLine = 0;

            foreach (var line in lines)
            {
                _currentLine++;
                ProcessLine(line);
            }

            return _output.ToString();
        }

        private void ProcessLine(string line)
        {
            var trimmedLine = line.Trim();

            // Handle block boundaries

            if (trimmedLine.Contains("{"))
            {
                _blockStack.Push(new BlockContext { StartLine = _currentLine });
            }


            if (trimmedLine.Contains("}") && _blockStack.Count > 0)
            {
                var block = _blockStack.Pop();
                // Cleanup spilled variables at block exit
                foreach (var var in block.LocalVars.Where(_spillInfoMap.ContainsKey))
                {
                    if (_spillInfoMap[var].IsSpilled)
                    {
                        _ = _output.AppendLine($"    // Cleanup spill slot for {var}");
                        _spillInfoMap[var].IsSpilled = false;
                    }
                }
            }

            // Detect loop constructs
            if (IsLoopConstruct(trimmedLine) && _blockStack.Count > 0)
            {
                _blockStack.Peek().IsLoop = true;
            }

            // Process variable declarations with spilling
            if (IsVariableDeclaration(trimmedLine, out var varName, out var varType, out var initializer))
            {
                if (_spillVars.Contains(varName) && !_processedDeclarations.Contains(varName))
                {
                    ProcessSpillableDeclaration(line, varName, varType, initializer);
                    _ = _processedDeclarations.Add(varName);
                    if (_blockStack.Count > 0)
                    {
                        _ = _blockStack.Peek().LocalVars.Add(varName);
                    }
                    return;
                }
            }

            // Process variable assignments
            if (IsVariableAssignment(trimmedLine, out varName, out var value))
            {
                if (_spillVars.Contains(varName) && _spillInfoMap.ContainsKey(varName))
                {
                    ProcessSpillableAssignment(line, varName, value);
                    return;
                }
            }

            // Process variable uses
            var processedLine = ProcessVariableUses(line);
            _ = _output.AppendLine(processedLine);
        }

        private static bool IsLoopConstruct(string line)
        {
            return Regex.IsMatch(line, @"\b(for|while|do)\b\s*\(");
        }

        private static bool IsVariableDeclaration(string line, out string varName, out string varType, out string initializer)
        {
            varName = varType = initializer = string.Empty;

            // Match patterns like: float x = expr; or int y = value;

            var match = Regex.Match(line, @"\b(float|double|int|uint32_t|int32_t|half)\s+(\w+)\s*=\s*([^;]+);");
            if (match.Success)
            {
                varType = match.Groups[1].Value;
                varName = match.Groups[2].Value;
                initializer = match.Groups[3].Value;
                return true;
            }

            // Match declaration without initialization
            match = Regex.Match(line, @"\b(float|double|int|uint32_t|int32_t|half)\s+(\w+)\s*;");
            if (match.Success)
            {
                varType = match.Groups[1].Value;
                varName = match.Groups[2].Value;
                initializer = GetDefaultInitializer(varType);
                return true;
            }

            return false;
        }

        private static string GetDefaultInitializer(string type)
        {
            return type switch
            {
                "float" or "double" or "half" => "0.0f",
                "int" or "int32_t" => "0",
                "uint32_t" => "0u",
                _ => "0"
            };
        }

        private static bool IsVariableAssignment(string line, out string varName, out string value)
        {
            varName = value = string.Empty;

            // Match patterns like: x = expr;

            var match = Regex.Match(line, @"\b(\w+)\s*=\s*([^;]+);");
            if (match.Success && !line.Contains("float") && !line.Contains("int") && !line.Contains("double"))
            {
                varName = match.Groups[1].Value;
                value = match.Groups[2].Value;
                return true;
            }

            return false;
        }

        private void ProcessSpillableDeclaration(string line, string varName, string varType, string initializer)
        {
            var info = _spillInfoMap[varName];
            info.Type = varType;

            // Determine if we should spill immediately or keep in register

            var shouldSpillImmediately = _blockStack.Any(b => b.IsLoop) || _spillVars.Count > 8;


            if (shouldSpillImmediately)
            {
                _ = _output.AppendLine($"    // Spillable variable {varName} - using spill slot {info.SpillSlot}");
                _ = _output.AppendLine($"    {varType} {varName} = {initializer};");
                _ = _output.AppendLine($"    SPILL_{varName}({varName}); // Spill to shared memory");
                info.IsSpilled = true;
            }
            else
            {
                _ = _output.AppendLine($"    // Spillable variable {varName} - keeping in register initially");
                _ = _output.AppendLine($"    {varType} {varName} = {initializer};");
                info.IsSpilled = false;
            }
        }

        private void ProcessSpillableAssignment(string line, string varName, string value)
        {
            var info = _spillInfoMap[varName];


            if (info.IsSpilled)
            {
                // Variable is spilled, need to reload, modify, and spill again
                _ = _output.AppendLine($"    {info.Type} {varName}_temp = RELOAD_{varName}(); // Reload from spill");
                _ = _output.AppendLine($"    {varName}_temp = {value};");
                _ = _output.AppendLine($"    SPILL_{varName}({varName}_temp); // Spill back");
            }
            else
            {
                // Variable is in register
                _ = _output.AppendLine($"    {varName} = {value};");

                // Consider spilling if register pressure is high

                if (_blockStack.Any(b => b.IsLoop))
                {
                    _ = _output.AppendLine($"    SPILL_{varName}({varName}); // Spill in loop context");
                    info.IsSpilled = true;
                }
            }
        }

        private string ProcessVariableUses(string line)
        {
            var processedLine = line;


            foreach (var varName in _spillVars)
            {
                if (!_spillInfoMap.ContainsKey(varName))
                {
                    continue;
                }


                var info = _spillInfoMap[varName];

                // Skip if this line is the declaration

                if (line.Contains($"{info.Type} {varName}"))
                {
                    continue;
                }

                // Look for uses of the variable (not assignments)

                var usePattern = $@"\b{Regex.Escape(varName)}\b(?!\s*=)";


                if (Regex.IsMatch(line, usePattern))
                {
                    if (info.IsSpilled)
                    {
                        // Replace variable use with reload macro
                        processedLine = Regex.Replace(processedLine, usePattern, $"RELOAD_{varName}()");
                        info.LastUse = _currentLine;
                    }
                }
            }


            return processedLine;
        }
    }

    /// <summary>
    /// Gets optimization recommendations for the kernel.
    /// </summary>
    public List<string> GetOptimizationRecommendations()
    {
        var recommendations = new List<string>();

        if (_estimatedRegisterCount > 128)
        {
            recommendations.Add("Consider splitting this kernel into multiple smaller kernels");
        }

        if (_estimatedRegisterCount > 64)
        {
            recommendations.Add($"Set MaxRegisters attribute to {RecommendedMaxRegisters}");
            recommendations.Add("Consider using shared memory for intermediate results");
        }

        if (_spillableVariables.Count > 5)
        {
            recommendations.Add("Reduce variable scope to minimize lifetime");
            recommendations.Add("Consider recomputing values instead of storing them");
        }

        var hasComplexMath = _kernelInfo.MethodDeclaration?.Body?.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsMathOperation) ?? false;

        if (hasComplexMath && _estimatedRegisterCount > 48)
        {
            recommendations.Add("Use fast math approximations to reduce register pressure");
            recommendations.Add("Consider lookup tables for transcendental functions");
        }

        return recommendations;
    }

    /// <summary>
    /// Generates PTX assembly hints for manual optimization.
    /// </summary>
    public string GeneratePtxHints()
    {
        var sb = new StringBuilder();


        if (HasHighRegisterPressure)
        {
            _ = sb.AppendLine("// PTX optimization hints for high register pressure");
            _ = sb.AppendLine($"// .maxnreg {RecommendedMaxRegisters}");


            if (_spillableVariables.Count > 0)
            {
                _ = sb.AppendLine("// Consider using .local memory for spilling:");
                foreach (var var in _spillableVariables)
                {
                    _ = sb.AppendLine($"//   .local .f32 spill_{var};");
                }
            }


            _ = sb.AppendLine("// Use .pragma \"nounroll\" for loops to reduce register pressure");
            _ = sb.AppendLine("// Consider .volatile for frequently accessed shared memory");

            // Generate specific PTX directives for spilling

            _ = sb.AppendLine();
            _ = sb.AppendLine("// PTX directives for explicit spilling:");
            _ = sb.AppendLine("// .reg .f32 %f<N>; // Limit float registers");
            _ = sb.AppendLine("// .reg .b32 %r<N>; // Limit integer registers");
            _ = sb.AppendLine("// .reg .b64 %rd<N>; // Limit 64-bit registers");


            if (_spillableVariables.Count > 0)
            {
                _ = sb.AppendLine();
                _ = sb.AppendLine("// Explicit spill/fill operations:");
                foreach (var var in _spillableVariables)
                {
                    _ = sb.AppendLine($"// st.local.f32 [spill_{var}], %f_reg; // Spill {var}");
                    _ = sb.AppendLine($"// ld.local.f32 %f_reg, [spill_{var}]; // Reload {var}");
                }
            }
        }


        return sb.ToString();
    }


    /// <summary>
    /// Analyzes data flow to optimize spilling decisions.
    /// </summary>
    public sealed class DataFlowAnalyzer
    {
        private readonly Dictionary<string, VariableFlowInfo> _flowInfo;
        private readonly KernelMethodInfo _kernelInfo;


        public sealed class VariableFlowInfo
        {
            public string Name { get; set; } = string.Empty;
            public int FirstUse { get; set; }
            public int LastUse { get; set; }
            public int UseCount { get; set; }
            public bool IsLoopCarried { get; set; }
            public HashSet<string> Dependencies { get; } = [];
            public int LiveRangeLength => LastUse - FirstUse;
        }


        public DataFlowAnalyzer(KernelMethodInfo kernelInfo)
        {
            _kernelInfo = kernelInfo;
            _flowInfo = [];
            Analyze();
        }


        private void Analyze()
        {
            if (_kernelInfo.MethodDeclaration?.Body == null)
            {
                return;
            }


            var body = _kernelInfo.MethodDeclaration.Body;
            var lineNumber = 0;

            // First pass: collect all variable declarations and uses

            foreach (var statement in body.Statements)
            {
                lineNumber++;
                AnalyzeStatement(statement, lineNumber);
            }

            // Second pass: analyze loop-carried dependencies

            AnalyzeLoopCarriedDependencies(body);
        }


        private void AnalyzeStatement(StatementSyntax statement, int lineNumber)
        {
            // Analyze variable declarations
            if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                foreach (var variable in localDecl.Declaration.Variables)
                {
                    var name = variable.Identifier.Text;
                    if (!_flowInfo.ContainsKey(name))
                    {
                        _flowInfo[name] = new VariableFlowInfo
                        {
                            Name = name,
                            FirstUse = lineNumber,
                            LastUse = lineNumber,
                            UseCount = 1
                        };
                    }
                }
            }

            // Analyze variable uses in expressions

            var identifiers = statement.DescendantNodes().OfType<IdentifierNameSyntax>();
            foreach (var identifier in identifiers)
            {
                var name = identifier.Identifier.Text;
                if (_flowInfo.ContainsKey(name))
                {
                    var info = _flowInfo[name];
                    info.LastUse = Math.Max(info.LastUse, lineNumber);
                    info.UseCount++;

                    // Track dependencies

                    var assignment = identifier.FirstAncestorOrSelf<AssignmentExpressionSyntax>();
                    if (assignment != null && assignment.Left.ToString() != name)
                    {
                        var leftSide = assignment.Left.ToString();
                        if (_flowInfo.ContainsKey(leftSide))
                        {
                            _ = _flowInfo[leftSide].Dependencies.Add(name);
                        }
                    }
                }
            }
        }


        private void AnalyzeLoopCarriedDependencies(BlockSyntax body)
        {
            var loops = body.DescendantNodes().Where(n =>

                n is ForStatementSyntax ||

                n is WhileStatementSyntax ||

                n is DoStatementSyntax);


            foreach (var loop in loops)
            {
                var loopBody = loop switch
                {
                    ForStatementSyntax forLoop => forLoop.Statement,
                    WhileStatementSyntax whileLoop => whileLoop.Statement,
                    DoStatementSyntax doLoop => doLoop.Statement,
                    _ => null
                };


                if (loopBody == null)
                {
                    continue;
                }

                // Find variables modified in the loop

                var modifiedVars = loopBody.DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Select(a => a.Left.ToString())
                    .Where(_flowInfo.ContainsKey)
                    .ToList();
                var modifiedVarsSet = new HashSet<string>(modifiedVars);

                // Find variables used in the loop

                var usedVars = loopBody.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(i => i.Identifier.Text)
                    .Where(_flowInfo.ContainsKey)
                    .ToList();
                var usedVarsSet = new HashSet<string>(usedVars);

                // Mark loop-carried dependencies

                foreach (var var in modifiedVarsSet.Intersect(usedVarsSet))
                {
                    _flowInfo[var].IsLoopCarried = true;
                }
            }
        }


        public IEnumerable<VariableFlowInfo> GetSpillCandidates()
        {
            return _flowInfo.Values
                .Where(v => v.LiveRangeLength > 10 || v.IsLoopCarried)
                .OrderByDescending(v => v.LiveRangeLength * v.UseCount);
        }
    }
}