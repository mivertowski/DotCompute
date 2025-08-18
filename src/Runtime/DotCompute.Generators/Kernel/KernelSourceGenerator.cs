// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DotCompute.Generators.Kernel
{
    /// <summary>
    /// Incremental source generator for DotCompute kernels.
    /// Generates backend-specific implementations for kernel methods.
    /// </summary>
    [Generator]
    public class KernelSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Find all methods marked with [Kernel] attribute
            var kernelMethods = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsKernelMethod(s),
                    transform: static (ctx, _) => GetKernelMethodInfo(ctx))
                .Where(static m => m is not null)
                .Select(static (m, _) => m!);

            // Find all classes implementing IKernel interface
            var kernelClasses = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => IsKernelClass(s),
                    transform: static (ctx, _) => GetKernelClassInfo(ctx))
                .Where(static c => c is not null)
                .Select(static (c, _) => c!);

            // Combine kernel methods and classes
            var kernelsToGenerate = kernelMethods
                .Collect()
                .Combine(kernelClasses.Collect())
                .Combine(context.CompilationProvider);

            // Generate source code
            context.RegisterSourceOutput(kernelsToGenerate, static (spc, source) => Execute(source.Left.Left, source.Left.Right, source.Right, spc));
        }

        private static bool IsKernelMethod(SyntaxNode node)
        {
            if (node is not MethodDeclarationSyntax methodDeclaration)
            {
                return false;
            }

            // Check for [Kernel] attribute
            return methodDeclaration.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString().Contains("Kernel"));
        }

        private static bool IsKernelClass(SyntaxNode node)
        {
            if (node is not ClassDeclarationSyntax classDeclaration)
            {
                return false;
            }

            // Check if class has kernel methods or implements IKernel
            return classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().Contains("Kernel")));
        }

        private static KernelMethodInfo? GetKernelMethodInfo(GeneratorSyntaxContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;
            var model = context.SemanticModel;
            var methodSymbol = model.GetDeclaredSymbol(methodDeclaration);

            if (methodSymbol is null)
            {
                return null;
            }

            // Extract kernel attribute data
            var kernelAttribute = methodSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "KernelAttribute");

            if (kernelAttribute is null)
            {
                return null;
            }

            // Extract kernel configuration
            var backends = GetBackendsFromAttribute(kernelAttribute);
            var vectorSize = GetVectorSizeFromAttribute(kernelAttribute);
            var isParallel = GetIsParallelFromAttribute(kernelAttribute);

            return new KernelMethodInfo
            {
                Name = methodSymbol.Name,
                ContainingType = methodSymbol.ContainingType.ToDisplayString(),
                Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
                Parameters = GetParameterInfo(methodSymbol),
                ReturnType = methodSymbol.ReturnType.ToDisplayString(),
                Backends = backends,
                VectorSize = vectorSize,
                IsParallel = isParallel,
                MethodDeclaration = methodDeclaration
            };
        }

        private static KernelClassInfo? GetKernelClassInfo(GeneratorSyntaxContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;
            var model = context.SemanticModel;
            var classSymbol = model.GetDeclaredSymbol(classDeclaration);

            if (classSymbol is null)
            {
                return null;
            }

            var kernelMethods = classSymbol.GetMembers()
                .OfType<IMethodSymbol>()
                .Where(m => m.GetAttributes().Any(a => a.AttributeClass?.Name == "KernelAttribute"))
                .ToList();

            if (kernelMethods.Count == 0)
            {
                return null;
            }

            return new KernelClassInfo
            {
                Name = classSymbol.Name,
                Namespace = classSymbol.ContainingNamespace.ToDisplayString(),
                KernelMethodNames = [.. kernelMethods.Select(m => m.Name)]
            };
        }

        private static void Execute(
            ImmutableArray<KernelMethodInfo> kernelMethods,
            ImmutableArray<KernelClassInfo> kernelClasses,
            Compilation compilation,
            SourceProductionContext context)
        {
            if (kernelMethods.IsDefaultOrEmpty && kernelClasses.IsDefaultOrEmpty)
            {
                return;
            }

            // Generate kernel registry
            GenerateKernelRegistry(kernelMethods, kernelClasses, context);

            // Generate backend-specific implementations
            foreach (var method in kernelMethods)
            {
                GenerateKernelImplementations(method, compilation, context);
            }

            // Generate kernel invokers
            foreach (var kernelClass in kernelClasses)
            {
                GenerateKernelInvoker(kernelClass, kernelMethods, context);
            }
        }

        private static void GenerateKernelRegistry(
            ImmutableArray<KernelMethodInfo> kernelMethods,
            ImmutableArray<KernelClassInfo> kernelClasses,
            SourceProductionContext context)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("using System;");
            source.AppendLine("using System.Collections.Generic;");
            source.AppendLine();
            source.AppendLine("namespace DotCompute.Generated");
            source.AppendLine("{");
            source.AppendLine("    // Temporary enum until proper reference is available");
            source.AppendLine("    public enum AcceleratorType");
            source.AppendLine("    {");
            source.AppendLine("        CPU = 0, CUDA = 1, Metal = 2, OpenCL = 3,");
            source.AppendLine("        OneAPI = 4, ROCm = 5, Vulkan = 6, WebGPU = 7, Custom = 99");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine("    public static class KernelRegistry");
            source.AppendLine("    {");
            source.AppendLine("        private static readonly Dictionary<string, KernelRegistration> _kernels = new()");
            source.AppendLine("        {");

            foreach (var method in kernelMethods)
            {
                var key = $"{method.ContainingType}.{method.Name}";
                source.AppendLine($"            [\"{key}\"] = new KernelRegistration");
                source.AppendLine("            {");
                source.AppendLine($"                Name = \"{method.Name}\",");
                source.AppendLine($"                FullName = \"{key}\",");
                source.AppendLine($"                ContainingType = typeof({method.ContainingType}),");
                source.AppendLine($"                SupportedBackends = new[] {{ {string.Join(", ", method.Backends.Select(b => $"AcceleratorType.{b}"))} }},");
                source.AppendLine($"                VectorSize = {method.VectorSize},");
                source.AppendLine($"                IsParallel = {(method.IsParallel ? "true" : "false")}");
                source.AppendLine("            },");
            }

            source.AppendLine("        };");
            source.AppendLine();
            source.AppendLine("        public static KernelRegistration GetKernel(string name) => _kernels[name];");
            source.AppendLine("        public static bool TryGetKernel(string name, out KernelRegistration kernel) => _kernels.TryGetValue(name, out kernel);");
            source.AppendLine("        public static IEnumerable<KernelRegistration> GetAllKernels() => _kernels.Values;");
            source.AppendLine("    }");
            source.AppendLine();
            source.AppendLine("    public class KernelRegistration");
            source.AppendLine("    {");
            source.AppendLine("        public string Name { get; set; } = string.Empty;");
            source.AppendLine("        public string FullName { get; set; } = string.Empty;");
            source.AppendLine("        public Type ContainingType { get; set; } = null!;");
            source.AppendLine("        public AcceleratorType[] SupportedBackends { get; set; } = Array.Empty<AcceleratorType>();");
            source.AppendLine("        public int VectorSize { get; set; }");
            source.AppendLine("        public bool IsParallel { get; set; }");
            source.AppendLine("    }");
            source.AppendLine("}");

            context.AddSource("KernelRegistry.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private static void GenerateKernelImplementations(
            KernelMethodInfo method,
            Compilation compilation,
            SourceProductionContext context)
        {
            foreach (var backend in method.Backends)
            {
                var source = backend switch
                {
                    "CPU" => GenerateCpuImplementation(method, compilation),
                    "CUDA" => GenerateCudaImplementation(method, compilation),
                    "Metal" => GenerateMetalImplementation(method, compilation),
                    "OpenCL" => GenerateOpenCLImplementation(method, compilation),
                    _ => null
                };

                if (source != null)
                {
                    var fileName = $"{method.ContainingType.Replace(".", "_")}_{method.Name}_{backend}.g.cs";
                    context.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
                }
            }
        }

        private static string GenerateCpuImplementation(KernelMethodInfo method, Compilation compilation)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("using System;");
            source.AppendLine("using System.Runtime.CompilerServices;");
            source.AppendLine("using System.Runtime.Intrinsics;");
            source.AppendLine("using System.Runtime.Intrinsics.X86;");
            source.AppendLine("using System.Threading.Tasks;");
            source.AppendLine("using System.Collections.Concurrent;");
            source.AppendLine();
            source.AppendLine($"namespace {method.Namespace}.Generated");
            source.AppendLine("{");
            source.AppendLine($"    public static unsafe class {method.Name}CpuKernel");
            source.AppendLine("    {");

            // Generate SIMD version
            if (method.VectorSize > 1)
            {
                GenerateSIMDMethod(source, method);
            }

            // Generate scalar version
            GenerateScalarMethod(source, method);

            // Generate parallel version
            if (method.IsParallel)
            {
                GenerateParallelMethod(source, method);
            }

            source.AppendLine("    }");
            source.AppendLine("}");

            return source.ToString();
        }

        private static void GenerateSIMDMethod(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine($"        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            
            // Check if length parameter already exists
            var hasLengthParam = method.Parameters.Any(p => p.Name == "length" && p.Type == "int");
            var paramList = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            if (!hasLengthParam)
            {
                paramList += ", int length";
            }
            
            source.AppendLine($"        public static unsafe void ExecuteSIMD({paramList})");
            source.AppendLine("        {");
            source.AppendLine($"            var vectorSize = Vector{method.VectorSize * 8}<float>.Count;");
            source.AppendLine("            var vectorCount = length / vectorSize;");
            source.AppendLine();
            source.AppendLine("            // Process vectorized elements");
            source.AppendLine("            for (int i = 0; i < vectorCount; i++)");
            source.AppendLine("            {");
            GenerateVectorizedMethodBody(source, method);
            source.AppendLine("            }");
            source.AppendLine();
            source.AppendLine("            // Process remaining scalar elements");
            source.AppendLine("            for (int i = vectorCount * vectorSize; i < length; i++)");
            source.AppendLine("            {");
            source.AppendLine("                // Process scalar element at index i");
            source.AppendLine("            }");
            source.AppendLine("        }");
            source.AppendLine();
        }

        private static void GenerateScalarMethod(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine($"        [MethodImpl(MethodImplOptions.AggressiveInlining)]");
            
            // Check if length parameter already exists
            var hasLengthParam = method.Parameters.Any(p => p.Name == "length" && p.Type == "int");
            var paramList = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            if (!hasLengthParam)
            {
                paramList += ", int length";
            }
            
            source.AppendLine($"        public static unsafe void ExecuteScalar({paramList})");
            source.AppendLine("        {");
            source.AppendLine("            // Extract and process the method body for scalar execution");
            GenerateScalarMethodBody(source, method);
            source.AppendLine("        }");
            source.AppendLine();
        }

        private static void GenerateParallelMethod(StringBuilder source, KernelMethodInfo method)
        {
            // Check if length parameter already exists
            var hasLengthParam = method.Parameters.Any(p => p.Name == "length" && p.Type == "int");
            var paramList = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            if (!hasLengthParam)
            {
                paramList += ", int length";
            }
            
            source.AppendLine($"        public static unsafe void ExecuteParallel({paramList})");
            source.AppendLine("        {");
            source.AppendLine("            var partitioner = Partitioner.Create(0, length);");
            source.AppendLine("            Parallel.ForEach(partitioner, range =>");
            source.AppendLine("            {");
            source.AppendLine("                // Process range.Item1 to range.Item2");
            source.AppendLine($"                // ExecuteSIMD with adjusted parameters for range");
            source.AppendLine("                for (int i = range.Item1; i < range.Item2; i++)");
            source.AppendLine("                {");
            source.AppendLine("                    // Process element at index i");
            source.AppendLine("                }");
            source.AppendLine("            });");
            source.AppendLine("        }");
            source.AppendLine();
        }

        private static string? GenerateCudaImplementation(KernelMethodInfo method, Compilation compilation) => GenerateCudaKernelCode(method, compilation);

        private static string? GenerateMetalImplementation(KernelMethodInfo method, Compilation compilation) => GenerateMetalShaderCode(method, compilation);

        private static string? GenerateOpenCLImplementation(KernelMethodInfo method, Compilation compilation) => GenerateOpenCLKernelCode(method, compilation);

        private static void GenerateKernelInvoker(
            KernelClassInfo kernelClass,
            ImmutableArray<KernelMethodInfo> allMethods,
            SourceProductionContext context)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("using System;");
            source.AppendLine("using DotCompute.Generated;"); // Use generated AcceleratorType
            source.AppendLine();
            source.AppendLine($"namespace {kernelClass.Namespace}.Generated");
            source.AppendLine("{");
            source.AppendLine($"    public static class {kernelClass.Name}Invoker");
            source.AppendLine("    {");
            source.AppendLine($"        public static void InvokeKernel(string methodName, AcceleratorType backend, params object[] args)");
            source.AppendLine("        {");
            source.AppendLine("            switch (methodName)");
            source.AppendLine("            {");

            foreach (var methodName in kernelClass.KernelMethodNames)
            {
                source.AppendLine($"                case \"{methodName}\":");
                source.AppendLine($"                    Invoke{methodName}(backend, args);");
                source.AppendLine($"                    break;");
            }

            source.AppendLine("                default:");
            source.AppendLine("                    throw new ArgumentException($\"Unknown kernel method: {methodName}\");");
            source.AppendLine("            }");
            source.AppendLine("        }");

            // Generate individual invoke methods
            foreach (var methodName in kernelClass.KernelMethodNames)
            {
                var method = allMethods.FirstOrDefault(m => m.Name == methodName && m.ContainingType.EndsWith(kernelClass.Name, StringComparison.Ordinal));
                if (method != null)
                {
                    GenerateInvokeMethod(source, method);
                }
            }

            source.AppendLine("    }");
            source.AppendLine("}");

            context.AddSource($"{kernelClass.Name}Invoker.g.cs", SourceText.From(source.ToString(), Encoding.UTF8));
        }

        private static void GenerateInvokeMethod(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine();
            source.AppendLine($"        private static unsafe void Invoke{method.Name}(AcceleratorType backend, object[] args)");
            source.AppendLine("        {");
            source.AppendLine("            // Validate argument count");
            source.AppendLine($"            if (args.Length != {method.Parameters.Count + 1}) // +1 for length parameter");
            source.AppendLine($"                throw new ArgumentException($\"Expected {method.Parameters.Count + 1} arguments, got {{args.Length}}\");");
            source.AppendLine();
            source.AppendLine("            // Cast arguments");

            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                // Skip if we already have a length parameter to avoid duplication
                if (param.Name == "length" && param.Type == "int")
                {
                    source.AppendLine($"            var length = (int)args[{i}];");
                }
                // Handle pointer types specially
                else if (param.Type.Contains("*"))
                {
                    source.AppendLine($"            // Pointer parameter {param.Name} would be handled by actual implementation");
                    source.AppendLine($"            var {param.Name} = args[{i}]; // Placeholder for pointer casting");
                }
                // Handle Span and ReadOnlySpan types with safe casting to avoid ambiguous conversions
                else if (param.Type.Contains("ReadOnlySpan<"))
                {
                    var elementType = ExtractSpanElementType(param.Type);
                    source.AppendLine($"            // Safe casting to avoid CS0457 ambiguous conversion error for {param.Type}");
                    source.AppendLine($"            {param.Type} {param.Name};");
                    source.AppendLine($"            if (args[{i}] is {elementType}[] arr{i})");
                    source.AppendLine($"                {param.Name} = new {param.Type}(arr{i});");
                    source.AppendLine($"            else if (args[{i}] is ArraySegment<{elementType}> segment{i})");
                    source.AppendLine($"                {param.Name} = new {param.Type}(segment{i}.Array, segment{i}.Offset, segment{i}.Count);");
                    source.AppendLine("            else");
                    source.AppendLine($"                throw new ArgumentException($\"Unsupported type for parameter {i}: {{args[{i}]?.GetType()}}\");");
                }
                else if (param.Type.Contains("Span<"))
                {
                    var elementType = ExtractSpanElementType(param.Type);
                    source.AppendLine($"            // Safe casting to avoid CS0457 ambiguous conversion error for {param.Type}");
                    source.AppendLine($"            {param.Type} {param.Name};");
                    source.AppendLine($"            if (args[{i}] is {elementType}[] arr{i})");
                    source.AppendLine($"                {param.Name} = new {param.Type}(arr{i});");
                    source.AppendLine($"            else if (args[{i}] is ArraySegment<{elementType}> segment{i})");
                    source.AppendLine($"                {param.Name} = new {param.Type}(segment{i}.Array, segment{i}.Offset, segment{i}.Count);");
                    source.AppendLine("            else");
                    source.AppendLine($"                throw new ArgumentException($\"Unsupported type for parameter {i}: {{args[{i}]?.GetType()}}\");");
                }
                else
                {
                    source.AppendLine($"            // Direct cast for {param.Type}");
                    source.AppendLine($"            var {param.Name} = ({param.Type})args[{i}];");
                }
            }
            
            // Only add length parameter if not already present
            var hasLengthParam = method.Parameters.Any(p => p.Name == "length" && p.Type == "int");
            if (!hasLengthParam)
            {
                source.AppendLine($"            var length = (int)args[{method.Parameters.Count}];");
            }

            source.AppendLine();
            source.AppendLine("            // Invoke appropriate backend implementation");
            source.AppendLine("            switch (backend)");
            source.AppendLine("            {");

            foreach (var backend in method.Backends)
            {
                source.AppendLine($"                case AcceleratorType.{backend}:");
                source.AppendLine($"                    // {method.Name}{backend}Kernel.Execute would be called here");
                source.AppendLine($"                    // Actual implementation depends on backend-specific kernel compilation");
                source.AppendLine($"                    break;");
            }

            source.AppendLine("                default:");
            source.AppendLine($"                    throw new NotSupportedException($\"Backend {{backend}} not supported for kernel {method.Name}\");");
            source.AppendLine("            }");
            source.AppendLine("        }");
        }

        private static List<string> GetBackendsFromAttribute(AttributeData attribute)
        {
            var backends = new List<string> { "CPU" }; // CPU is always supported

            if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "Backends").Value.Value is int backendsValue)
            {
                if ((backendsValue & 2) != 0)
                {
                    backends.Add("CUDA");
                }
                if ((backendsValue & 4) != 0)
                {
                    backends.Add("Metal");
                }
                if ((backendsValue & 8) != 0)
                {
                    backends.Add("OpenCL");
                }
            }

            return backends;
        }

        private static int GetVectorSizeFromAttribute(AttributeData attribute)
        {
            if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "VectorSize").Value.Value is int vectorSize)
            {
                return vectorSize;
            }
            return 8; // Default to 256-bit vectors
        }

        private static bool GetIsParallelFromAttribute(AttributeData attribute)
        {
            if (attribute.NamedArguments.FirstOrDefault(a => a.Key == "IsParallel").Value.Value is bool isParallel)
            {
                return isParallel;
            }
            return true; // Default to parallel execution
        }

        private static List<ParameterInfo> GetParameterInfo(IMethodSymbol method)
        {
            return [.. method.Parameters.Select(p => new ParameterInfo
            {
                Name = p.Name,
                Type = p.Type.ToDisplayString(),
                IsBuffer = IsBufferType(p.Type),
                IsReadOnly = p.RefKind == RefKind.In || p.Type.IsReadOnly
            })];
        }

        private static bool IsBufferType(ITypeSymbol type)
        {
            // Check if type implements IBuffer or is a pointer/span
            return type.Name.Contains("Buffer") ||
                   type.Name.Contains("Span") ||
                   type.TypeKind == TypeKind.Pointer ||
                   type.AllInterfaces.Any(i => i.Name == "IBuffer");
        }

        /// <summary>
        /// Generates vectorized method body by analyzing the original method syntax.
        /// </summary>
        private static void GenerateVectorizedMethodBody(StringBuilder source, KernelMethodInfo method)
        {
            // Analyze the method body for vectorizable patterns
            var methodBody = method.MethodDeclaration?.Body?.ToString() ?? "";

            // Generate SIMD-optimized implementation based on common patterns
            if (ContainsArithmeticOperations(methodBody))
            {
                GenerateArithmeticVectorization(source, method);
            }
            else if (ContainsMemoryOperations(methodBody))
            {
                GenerateMemoryVectorization(source, method);
            }
            else
            {
                // Generic vectorization fallback
                GenerateGenericVectorization(source, method);
            }
        }

        /// <summary>
        /// Generates scalar method body implementation.
        /// </summary>
        private static void GenerateScalarMethodBody(StringBuilder source, KernelMethodInfo method)
        {
            var methodBody = method.MethodDeclaration?.Body?.ToString() ?? "";

            source.AppendLine("            for (int i = 0; i < length; i++)");
            source.AppendLine("            {");

            // Transform the method body for scalar execution
            if (ContainsArithmeticOperations(methodBody))
            {
                GenerateScalarArithmetic(source, method);
            }
            else if (ContainsMemoryOperations(methodBody))
            {
                GenerateScalarMemoryOps(source, method);
            }
            else
            {
                source.AppendLine("                // Process element at index i");
                source.AppendLine($"                // Original operation: {methodBody.Replace(Environment.NewLine, " ").Trim()}");
            }

            source.AppendLine("            }");
        }

        /// <summary>
        /// Generates CUDA kernel code for GPU execution.
        /// </summary>
        private static string GenerateCudaKernelCode(KernelMethodInfo method, Compilation compilation)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("// CUDA Kernel Implementation");
            source.AppendLine();
            source.AppendLine("#include <cuda_runtime.h>");
            source.AppendLine("#include <device_launch_parameters.h>");
            source.AppendLine();

            // Generate kernel function
            source.AppendLine($"extern \"C\" __global__ void {method.Name}_cuda_kernel(");
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                var cudaType = ConvertToCudaType(param.Type);
                var modifier = param.IsBuffer ? "*" : "";
                source.Append($"    {cudaType}{modifier} {param.Name}");
                if (i < method.Parameters.Count - 1)
                {
                    source.Append(',');
                }
                source.AppendLine();
            }
            source.AppendLine("    , int length)");
            source.AppendLine("{");
            source.AppendLine("    int idx = blockIdx.x * blockDim.x + threadIdx.x;");
            source.AppendLine("    if (idx < length) {");

            // Generate CUDA kernel body
            GenerateCudaKernelBody(source, method);

            source.AppendLine("    }");
            source.AppendLine("}");

            // Generate host wrapper
            GenerateCudaHostWrapper(source, method);

            return source.ToString();
        }

        /// <summary>
        /// Generates Metal compute shader code.
        /// </summary>
        private static string GenerateMetalShaderCode(KernelMethodInfo method, Compilation compilation)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("// Metal Compute Shader Implementation");
            source.AppendLine("#include <metal_stdlib>");
            source.AppendLine("using namespace metal;");
            source.AppendLine();

            // Generate kernel function
            source.AppendLine($"kernel void {method.Name}_metal_kernel(");
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                var metalType = ConvertToMetalType(param.Type);
                var addressSpace = param.IsBuffer ? "device " : "constant ";
                var modifier = param.IsBuffer ? "*" : "&";
                source.Append($"    {addressSpace}{metalType}{modifier} {param.Name} [[buffer({i})]]");
                if (i < method.Parameters.Count - 1)
                {
                    source.Append(',');
                }
                source.AppendLine();
            }
            source.AppendLine("    , uint2 gid [[thread_position_in_grid]],");
            source.AppendLine("    uint2 grid_size [[threads_per_grid]])");
            source.AppendLine("{");
            source.AppendLine("    uint index = gid.x + gid.y * grid_size.x;");

            // Generate Metal kernel body
            GenerateMetalKernelBody(source, method);

            source.AppendLine("}");

            return source.ToString();
        }

        /// <summary>
        /// Generates OpenCL kernel code.
        /// </summary>
        private static string GenerateOpenCLKernelCode(KernelMethodInfo method, Compilation compilation)
        {
            var source = new StringBuilder();
            source.AppendLine("// <auto-generated/>");
            source.AppendLine("// OpenCL Kernel Implementation");
            source.AppendLine();

            // Generate kernel function
            source.AppendLine($"__kernel void {method.Name}_opencl_kernel(");
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var param = method.Parameters[i];
                var openclType = ConvertToOpenCLType(param.Type);
                var addressSpace = param.IsBuffer ? "__global " : "__constant ";
                var modifier = param.IsBuffer ? "*" : "";
                source.Append($"    {addressSpace}{openclType}{modifier} {param.Name}");
                if (i < method.Parameters.Count - 1)
                {
                    source.Append(',');
                }
                source.AppendLine();
            }
            source.AppendLine("    , int length)");
            source.AppendLine("{");
            source.AppendLine("    int idx = get_global_id(0);");
            source.AppendLine("    if (idx < length) {");

            // Generate OpenCL kernel body
            GenerateOpenCLKernelBody(source, method);

            source.AppendLine("    }");
            source.AppendLine("}");

            return source.ToString();
        }

        // Helper methods for pattern detection
        private static bool ContainsArithmeticOperations(string methodBody)
        {
            return methodBody.Contains("+") || methodBody.Contains("-") ||
                   methodBody.Contains("*") || methodBody.Contains("/") ||
                   methodBody.Contains("Math.") || methodBody.Contains("MathF.");
        }

        private static bool ContainsMemoryOperations(string methodBody)
        {
            return methodBody.Contains("[") && methodBody.Contains("]") ||
                   methodBody.Contains("Span") || methodBody.Contains("Memory");
        }

        // Vectorization generators
        private static void GenerateArithmeticVectorization(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("                // Vectorized arithmetic operations");
            source.AppendLine("                unsafe");
            source.AppendLine("                {");
            source.AppendLine("                    // TODO: Implement vectorized arithmetic based on actual kernel method");
            source.AppendLine("                    // This is a placeholder that avoids compilation errors");
            source.AppendLine("                    // Actual implementation should analyze method body and generate appropriate code");
            source.AppendLine("                }");
        }

        private static void GenerateMemoryVectorization(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("                // Vectorized memory operations");
            source.AppendLine("                unsafe");
            source.AppendLine("                {");
            source.AppendLine("                    // TODO: Implement memory vectorization based on actual kernel method");
            source.AppendLine("                    // This is a placeholder that avoids compilation errors");
            source.AppendLine("                }");
        }

        private static void GenerateGenericVectorization(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("                // Generic vectorized processing");
            source.AppendLine("                // TODO: Implement generic vectorization based on actual kernel method");
            source.AppendLine("                // This is a placeholder that avoids compilation errors");
        }

        // Scalar generators
        private static void GenerateScalarArithmetic(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("                // Scalar arithmetic operation");
            source.AppendLine("                // TODO: Implement scalar arithmetic based on actual kernel method");
            source.AppendLine("                // This is a placeholder that avoids compilation errors");
        }

        private static void GenerateScalarMemoryOps(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("                // Scalar memory operation");
            source.AppendLine("                // TODO: Implement scalar memory operations based on actual kernel method");
            source.AppendLine("                // This is a placeholder that avoids compilation errors");
        }

        // GPU kernel body generators
        private static void GenerateCudaKernelBody(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("        // CUDA kernel operation");
            source.AppendLine("        // Implementation depends on kernel operation type");
            source.AppendLine("        // output[idx] = operation(input[idx]);");
        }

        private static void GenerateMetalKernelBody(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("    // Metal kernel operation");
            source.AppendLine("    // Implementation depends on kernel operation type");
            source.AppendLine("    // output[index] = operation(input[index]);");
        }

        private static void GenerateOpenCLKernelBody(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine("        // OpenCL kernel operation");
            source.AppendLine("        // Implementation depends on kernel operation type");
            source.AppendLine("        // output[idx] = operation(input[idx]);");
        }

        private static void GenerateCudaHostWrapper(StringBuilder source, KernelMethodInfo method)
        {
            source.AppendLine();
            source.AppendLine("// Host wrapper function");
            source.AppendLine($"extern \"C\" void launch_{method.Name}_cuda(");
            source.AppendLine("    void** args, int length, int blockSize, int gridSize)");
            source.AppendLine("{");
            source.AppendLine($"    {method.Name}_cuda_kernel<<<gridSize, blockSize>>>(");
            for (var i = 0; i < method.Parameters.Count; i++)
            {
                source.Append($"        ({ConvertToCudaType(method.Parameters[i].Type)}*)args[{i}]");
                if (i < method.Parameters.Count - 1)
                {
                    source.Append(',');
                }
                source.AppendLine();
            }
            source.AppendLine("        , length);");
            source.AppendLine("    cudaDeviceSynchronize();");
            source.AppendLine("}");
        }

        // Helper method to extract element type from Span<T> or ReadOnlySpan<T>
        private static string ExtractSpanElementType(string spanType)
        {
            // Extract element type from "Span<T>" or "ReadOnlySpan<T>"
            var startIndex = spanType.IndexOf('<') + 1;
            var endIndex = spanType.LastIndexOf('>');
            if (startIndex > 0 && endIndex > startIndex)
            {
                return spanType.Substring(startIndex, endIndex - startIndex);
            }
            return "float"; // Fallback to float if parsing fails
        }

        // Type conversion helpers
        private static string ConvertToCudaType(string csharpType)
        {
            return csharpType switch
            {
                "float" => "float",
                "double" => "double",
                "int" => "int",
                "uint" => "unsigned int",
                "long" => "long long",
                "ulong" => "unsigned long long",
                "byte" => "unsigned char",
                "sbyte" => "char",
                "short" => "short",
                "ushort" => "unsigned short",
                _ => "float" // Default fallback
            };
        }

        private static string ConvertToMetalType(string csharpType)
        {
            return csharpType switch
            {
                "float" => "float",
                "double" => "double", // Note: Metal has limited double support
                "int" => "int",
                "uint" => "uint",
                "long" => "long",
                "ulong" => "ulong",
                "byte" => "uchar",
                "sbyte" => "char",
                "short" => "short",
                "ushort" => "ushort",
                _ => "float" // Default fallback
            };
        }

        private static string ConvertToOpenCLType(string csharpType)
        {
            return csharpType switch
            {
                "float" => "float",
                "double" => "double",
                "int" => "int",
                "uint" => "uint",
                "long" => "long",
                "ulong" => "ulong",
                "byte" => "uchar",
                "sbyte" => "char",
                "short" => "short",
                "ushort" => "ushort",
                _ => "float" // Default fallback
            };
        }
    }

    internal sealed class KernelMethodInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ContainingType { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public List<ParameterInfo> Parameters { get; set; } = [];
        public string ReturnType { get; set; } = string.Empty;
        public List<string> Backends { get; set; } = [];
        public int VectorSize { get; set; }
        public bool IsParallel { get; set; }
        public MethodDeclarationSyntax MethodDeclaration { get; set; } = null!;
    }

    internal sealed class KernelClassInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public List<string> KernelMethodNames { get; set; } = [];
    }

    internal sealed class ParameterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsBuffer { get; set; }
        public bool IsReadOnly { get; set; }
    }
}
