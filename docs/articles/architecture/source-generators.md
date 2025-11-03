# Source Generators Architecture

The Source Generators system provides compile-time code generation and validation for DotCompute kernels using Roslyn's Incremental Source Generators and Analyzers.

## Architecture Overview

```
User Code ([Kernel] attribute)
    ↓
┌─────────────────────────────────────────────────┐
│         Roslyn Compiler Pipeline                │
├─────────────────────────────────────────────────┤
│  - Syntax tree analysis                         │
│  - Semantic model construction                  │
│  - Incremental compilation                      │
└─────────────────────────────────────────────────┘
    ↓                                   ↓
KernelSourceGenerator         KernelMethodAnalyzer
(Code Generation)             (Diagnostics DC001-DC012)
    ↓                                   ↓
┌───────────────────┐         ┌───────────────────┐
│ Generated Code:   │         │ IDE Integration:  │
│ - CPU SIMD        │         │ - Error squiggles │
│ - CUDA kernels    │         │ - Quick fixes     │
│ - Metal shaders   │         │ - Code actions    │
│ - Registration    │         │ - Suggestions     │
└───────────────────┘         └───────────────────┘
    ↓
Compilation → Runtime Discovery → Execution
```

## Design Principles

### 1. **Incremental Compilation**
- **Caching**: Reuse previous generation results when inputs unchanged
- **Partial Regeneration**: Only regenerate affected kernels
- **Performance**: < 100ms for typical project builds

### 2. **Zero Runtime Overhead**
- **Compile-Time Generation**: All code generated at compile time
- **No Reflection**: Direct method calls, not reflection-based
- **AOT Compatible**: Fully Native AOT compatible

### 3. **Rich IDE Integration**
- **Real-Time Feedback**: Diagnostics as you type
- **Automated Fixes**: One-click code fixes for common issues
- **IntelliSense**: Full IntelliSense support for generated code

### 4. **Multi-Backend Support**
- **CPU**: SIMD-optimized C# code
- **CUDA**: CUDA C kernel source
- **Metal**: Metal Shading Language
- **Extensible**: Plugin architecture for new backends

## KernelSourceGenerator

### Incremental Source Generator

```csharp
[Generator]
public class KernelSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Find methods with [Kernel] attribute
        var kernelMethods = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (node, _) => IsKernelMethodCandidate(node),
                transform: static (ctx, _) => GetKernelMethodOrNull(ctx))
            .Where(static m => m is not null);

        // 2. Combine with compilation
        var compilationAndKernels = context.CompilationProvider.Combine(kernelMethods.Collect());

        // 3. Generate code
        context.RegisterSourceOutput(compilationAndKernels,
            static (spc, source) => Execute(source.Left, source.Right!, spc));
    }

    private static bool IsKernelMethodCandidate(SyntaxNode node)
    {
        // Fast syntax-only check (no semantic analysis)
        return node is MethodDeclarationSyntax method &&
               method.AttributeLists.Count > 0 &&
               method.Modifiers.Any(SyntaxKind.StaticKeyword);
    }

    private static MethodDeclarationSyntax? GetKernelMethodOrNull(
        GeneratorSyntaxContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        // Check if method has [Kernel] attribute (semantic analysis)
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);
        if (methodSymbol == null)
            return null;

        var hasKernelAttribute = methodSymbol.GetAttributes()
            .Any(a => a.AttributeClass?.Name == "KernelAttribute");

        return hasKernelAttribute ? methodDeclaration : null;
    }

    private static void Execute(
        Compilation compilation,
        ImmutableArray<MethodDeclarationSyntax> kernelMethods,
        SourceProductionContext context)
    {
        if (kernelMethods.IsDefaultOrEmpty)
            return;

        // Generate code for each kernel
        foreach (var method in kernelMethods)
        {
            var semanticModel = compilation.GetSemanticModel(method.SyntaxTree);
            var methodSymbol = semanticModel.GetDeclaredSymbol(method);

            if (methodSymbol == null)
                continue;

            // Extract kernel metadata
            var metadata = ExtractKernelMetadata(methodSymbol, method);

            // Generate backend implementations
            GenerateCpuImplementation(context, metadata);
            GenerateCudaImplementation(context, metadata);
            GenerateMetalImplementation(context, metadata);
            GenerateRegistration(context, metadata);
        }
    }
}
```

### Kernel Metadata Extraction

```csharp
private class KernelMetadata
{
    public string Name { get; init; }
    public string Namespace { get; init; }
    public string DeclaringType { get; init; }
    public List<ParameterInfo> Parameters { get; init; }
    public string ReturnType { get; init; }
    public KernelBackends SupportedBackends { get; init; }
    public bool IsParallel { get; init; }
    public string OriginalSource { get; init; }
}

private static KernelMetadata ExtractKernelMetadata(
    IMethodSymbol methodSymbol,
    MethodDeclarationSyntax methodSyntax)
{
    var metadata = new KernelMetadata
    {
        Name = methodSymbol.Name,
        Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
        DeclaringType = methodSymbol.ContainingType.Name,
        Parameters = ExtractParameters(methodSymbol),
        ReturnType = methodSymbol.ReturnType.ToDisplayString(),
        OriginalSource = methodSyntax.ToFullString()
    };

    // Analyze for backend support
    metadata.SupportedBackends = DetermineBackendSupport(methodSymbol, methodSyntax);
    metadata.IsParallel = IsParallelizable(methodSyntax);

    return metadata;
}

private static List<ParameterInfo> ExtractParameters(IMethodSymbol methodSymbol)
{
    return methodSymbol.Parameters
        .Select(p => new ParameterInfo
        {
            Name = p.Name,
            Type = p.Type.ToDisplayString(),
            IsInput = IsInputParameter(p),
            IsOutput = IsOutputParameter(p),
            IsScalar = IsScalarType(p.Type)
        })
        .ToList();
}

private static bool IsInputParameter(IParameterSymbol parameter)
{
    var typeString = parameter.Type.ToDisplayString();
    return typeString.StartsWith("ReadOnlySpan<") ||
           typeString.StartsWith("ReadOnlyMemory<") ||
           (!typeString.StartsWith("Span<") && !parameter.RefKind.HasFlag(RefKind.Out));
}

private static bool IsOutputParameter(IParameterSymbol parameter)
{
    var typeString = parameter.Type.ToDisplayString();
    return typeString.StartsWith("Span<") ||
           parameter.RefKind.HasFlag(RefKind.Out) ||
           parameter.RefKind.HasFlag(RefKind.Ref);
}
```

## Backend Code Generation

### CPU SIMD Generation

```csharp
private static void GenerateCpuImplementation(
    SourceProductionContext context,
    KernelMetadata metadata)
{
    var source = $@"
using System;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace {metadata.Namespace}
{{
    partial class {metadata.DeclaringType}
    {{
        private static void {metadata.Name}_CPU_SIMD({GetParameterList(metadata.Parameters)})
        {{
            {GenerateSimdLoop(metadata)}
        }}

        private static void {metadata.Name}_CPU_Scalar({GetParameterList(metadata.Parameters)})
        {{
            {GenerateScalarLoop(metadata)}
        }}
    }}
}}";

    context.AddSource($"{metadata.DeclaringType}.{metadata.Name}.CPU.g.cs", source);
}

private static string GenerateSimdLoop(KernelMetadata metadata)
{
    // Detect vectorizable operation
    if (IsSimpleVectorAddition(metadata))
    {
        return @"
int vectorSize = Vector<float>.Count;
int i = 0;

// Vectorized loop
for (; i <= result.Length - vectorSize; i += vectorSize)
{
    var va = new Vector<float>(a.Slice(i));
    var vb = new Vector<float>(b.Slice(i));
    (va + vb).CopyTo(result.Slice(i));
}

// Scalar remainder
for (; i < result.Length; i++)
{
    result[i] = a[i] + b[i];
}";
    }

    // More sophisticated operation detection...
    return GenerateScalarLoop(metadata);
}
```

### CUDA Kernel Generation

```csharp
private static void GenerateCudaImplementation(
    SourceProductionContext context,
    KernelMetadata metadata)
{
    var cudaSource = $@"
extern ""C"" __global__ void {metadata.Name}(
    {GetCudaParameterList(metadata.Parameters)})
{{
    int idx = blockIdx.x * blockDim.x + threadIdx.x;

    {GenerateCudaBody(metadata)}
}}";

    var wrapperSource = $@"
namespace {metadata.Namespace}
{{
    partial class {metadata.DeclaringType}
    {{
        private const string {metadata.Name}_CUDA_Source = @""{EscapeSource(cudaSource)}"";

        private static CudaKernelMetadata {metadata.Name}_CUDA_Metadata => new()
        {{
            EntryPoint = ""{metadata.Name}"",
            Source = {metadata.Name}_CUDA_Source,
            ThreadsPerBlock = 256,
            MinimumComputeCapability = new Version(5, 0)
        }};
    }}
}}";

    context.AddSource($"{metadata.DeclaringType}.{metadata.Name}.CUDA.g.cs", wrapperSource);
}

private static string GenerateCudaBody(KernelMetadata metadata)
{
    // Simple pattern matching for common operations
    if (IsSimpleVectorAddition(metadata))
    {
        return @"
if (idx < length)
{
    result[idx] = a[idx] + b[idx];
}";
    }

    // More complex translations...
    return TranslateToCuda(metadata.OriginalSource);
}
```

### Metal Shader Generation

```csharp
private static void GenerateMetalImplementation(
    SourceProductionContext context,
    KernelMetadata metadata)
{
    var metalSource = $@"
#include <metal_stdlib>
using namespace metal;

kernel void {metadata.Name}(
    {GetMetalParameterList(metadata.Parameters)},
    uint idx [[thread_position_in_grid]])
{{
    {GenerateMetalBody(metadata)}
}}";

    var wrapperSource = $@"
namespace {metadata.Namespace}
{{
    partial class {metadata.DeclaringType}
    {{
        private const string {metadata.Name}_Metal_Source = @""{EscapeSource(metalSource)}"";

        private static MetalKernelMetadata {metadata.Name}_Metal_Metadata => new()
        {{
            EntryPoint = ""{metadata.Name}"",
            Source = {metadata.Name}_Metal_Source,
            ThreadsPerThreadgroup = 256
        }};
    }}
}}";

    context.AddSource($"{metadata.DeclaringType}.{metadata.Name}.Metal.g.cs", wrapperSource);
}
```

### Registration Generation

```csharp
private static void GenerateRegistration(
    SourceProductionContext context,
    IEnumerable<KernelMetadata> allKernels)
{
    var source = $@"
using DotCompute.Abstractions;
using System;

namespace DotCompute.Generated
{{
    public static class GeneratedKernels
    {{
        public static void Register(IKernelRegistry registry)
        {{
{GenerateRegistrationCalls(allKernels)}
        }}
    }}
}}";

    context.AddSource("GeneratedKernels.g.cs", source);
}

private static string GenerateRegistrationCalls(IEnumerable<KernelMetadata> kernels)
{
    var sb = new StringBuilder();

    foreach (var kernel in kernels)
    {
        sb.AppendLine($@"
            registry.RegisterKernel(new KernelMetadata
            {{
                Name = ""{kernel.Name}"",
                Namespace = ""{kernel.Namespace}"",
                DeclaringType = ""{kernel.DeclaringType}"",
                Backends = KernelBackends.{kernel.SupportedBackends},
                CpuImplementation = {kernel.DeclaringType}.{kernel.Name}_CPU_SIMD,
                CudaMetadata = {kernel.DeclaringType}.{kernel.Name}_CUDA_Metadata,
                MetalMetadata = {kernel.DeclaringType}.{kernel.Name}_Metal_Metadata
            }});");
    }

    return sb.ToString();
}
```

## Kernel Analyzer (DC001-DC012)

### Analyzer Implementation

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class KernelMethodAnalyzer : DiagnosticAnalyzer
{
    // Diagnostic IDs
    private const string DC001 = "DC001"; // Kernel must be static
    private const string DC002 = "DC002"; // Kernel must return void
    private const string DC003 = "DC003"; // Kernel cannot be async
    private const string DC004 = "DC004"; // Kernel cannot use ref/out (except Span)
    private const string DC005 = "DC005"; // Kernel must have at least one parameter
    private const string DC006 = "DC006"; // Kernel cannot call non-inlineable methods
    private const string DC007 = "DC007"; // Kernel cannot use LINQ
    private const string DC008 = "DC008"; // Kernel cannot use reflection
    private const string DC009 = "DC009"; // Kernel should check bounds
    private const string DC010 = "DC010"; // Kernel should use Kernel.ThreadId
    private const string DC011 = "DC011"; // Kernel parameters should use Span<T>
    private const string DC012 = "DC012"; // Kernel should be documented

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DC001Descriptor, DC002Descriptor, DC003Descriptor, DC004Descriptor,
        DC005Descriptor, DC006Descriptor, DC007Descriptor, DC008Descriptor,
        DC009Descriptor, DC010Descriptor, DC011Descriptor, DC012Descriptor
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Register syntax node action for methods
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration);

        if (methodSymbol == null)
            return;

        // Only analyze methods with [Kernel] attribute
        if (!HasKernelAttribute(methodSymbol))
            return;

        // DC001: Must be static
        if (!methodSymbol.IsStatic)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC001Descriptor,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name
            ));
        }

        // DC002: Must return void
        if (methodSymbol.ReturnType.SpecialType != SpecialType.System_Void)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC002Descriptor,
                methodDeclaration.ReturnType.GetLocation(),
                methodSymbol.Name
            ));
        }

        // DC003: Cannot be async
        if (methodSymbol.IsAsync)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC003Descriptor,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name
            ));
        }

        // DC004: Cannot use ref/out (except Span<T>)
        foreach (var parameter in methodSymbol.Parameters)
        {
            if (parameter.RefKind != RefKind.None && !IsSpanType(parameter.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DC004Descriptor,
                    parameter.Locations[0],
                    parameter.Name
                ));
            }
        }

        // DC005: Must have at least one parameter
        if (methodSymbol.Parameters.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC005Descriptor,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name
            ));
        }

        // DC009: Should check bounds
        if (!HasBoundsCheck(methodDeclaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC009Descriptor,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name
            ));
        }

        // DC010: Should use Kernel.ThreadId
        if (!UsesKernelThreadId(methodDeclaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC010Descriptor,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name
            ));
        }

        // DC011: Parameters should use Span<T>
        foreach (var parameter in methodSymbol.Parameters)
        {
            if (parameter.Type is IArrayTypeSymbol && !IsSpanType(parameter.Type))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DC011Descriptor,
                    parameter.Locations[0],
                    parameter.Name
                ));
            }
        }

        // DC012: Should be documented
        if (!HasDocumentationComment(methodDeclaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DC012Descriptor,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name
            ));
        }
    }

    private bool HasBoundsCheck(MethodDeclarationSyntax method)
    {
        // Look for patterns like: if (idx < length)
        return method.DescendantNodes()
            .OfType<IfStatementSyntax>()
            .Any(ifStmt => ContainsBoundsCheck(ifStmt.Condition));
    }

    private bool UsesKernelThreadId(MethodDeclarationSyntax method)
    {
        // Look for Kernel.ThreadId.X/Y/Z usage
        return method.DescendantNodes()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(member => member.Expression.ToString().Contains("Kernel.ThreadId"));
    }
}
```

### Diagnostic Descriptors

```csharp
private static readonly DiagnosticDescriptor DC001Descriptor = new(
    id: "DC001",
    title: "Kernel method must be static",
    messageFormat: "Kernel method '{0}' must be declared static",
    category: "DotCompute.Design",
    defaultSeverity: DiagnosticSeverity.Error,
    isEnabledByDefault: true,
    description: "Kernel methods must be static to ensure they can be invoked without instance context."
);

private static readonly DiagnosticDescriptor DC009Descriptor = new(
    id: "DC009",
    title: "Kernel should check array bounds",
    messageFormat: "Kernel method '{0}' should check array bounds to prevent out-of-range access",
    category: "DotCompute.Reliability",
    defaultSeverity: DiagnosticSeverity.Warning,
    isEnabledByDefault: true,
    description: "Kernels should include bounds checking (e.g., 'if (idx < length)') to prevent crashes."
);

private static readonly DiagnosticDescriptor DC011Descriptor = new(
    id: "DC011",
    title: "Kernel parameters should use Span<T> instead of arrays",
    messageFormat: "Parameter '{0}' should use Span<T> or ReadOnlySpan<T> instead of array",
    category: "DotCompute.Performance",
    defaultSeverity: DiagnosticSeverity.Info,
    isEnabledByDefault: true,
    description: "Using Span<T> enables zero-copy operations and better performance."
);
```

## Code Fix Providers

### 5 Automated Code Fixes

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class KernelCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
        "DC001", // Add static modifier
        "DC002", // Change return type to void
        "DC003", // Remove async modifier
        "DC009", // Add bounds check
        "DC011"  // Convert array to Span<T>
    );

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var node = root.FindNode(diagnosticSpan);

        switch (diagnostic.Id)
        {
            case "DC001": // Add static modifier
                RegisterAddStaticFix(context, node);
                break;

            case "DC002": // Change return type to void
                RegisterChangeReturnTypeFix(context, node);
                break;

            case "DC003": // Remove async modifier
                RegisterRemoveAsyncFix(context, node);
                break;

            case "DC009": // Add bounds check
                RegisterAddBoundsCheckFix(context, node);
                break;

            case "DC011": // Convert array to Span<T>
                RegisterConvertToSpanFix(context, node);
                break;
        }
    }

    private void RegisterAddBoundsCheckFix(CodeFixContext context, SyntaxNode node)
    {
        var action = CodeAction.Create(
            title: "Add bounds check",
            createChangedDocument: c => AddBoundsCheckAsync(context.Document, node, c),
            equivalenceKey: "AddBoundsCheck"
        );

        context.RegisterCodeFix(action, context.Diagnostics);
    }

    private async Task<Document> AddBoundsCheckAsync(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        var method = node.FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (method == null)
            return document;

        // Generate bounds check statement
        var boundsCheck = SyntaxFactory.IfStatement(
            SyntaxFactory.BinaryExpression(
                SyntaxKind.LessThanExpression,
                SyntaxFactory.IdentifierName("idx"),
                SyntaxFactory.IdentifierName("length")
            ),
            method.Body!.Statements.First()
        );

        // Insert at beginning of method
        var newBody = method.Body.WithStatements(
            SyntaxFactory.List(new[] { boundsCheck }.Concat(method.Body.Statements.Skip(1)))
        );

        var newMethod = method.WithBody(newBody);
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root!.ReplaceNode(method, newMethod);

        return document.WithSyntaxRoot(newRoot);
    }

    private void RegisterConvertToSpanFix(CodeFixContext context, SyntaxNode node)
    {
        var action = CodeAction.Create(
            title: "Convert to Span<T>",
            createChangedDocument: c => ConvertToSpanAsync(context.Document, node, c),
            equivalenceKey: "ConvertToSpan"
        );

        context.RegisterCodeFix(action, context.Diagnostics);
    }

    private async Task<Document> ConvertToSpanAsync(
        Document document,
        SyntaxNode node,
        CancellationToken cancellationToken)
    {
        var parameter = node.FirstAncestorOrSelf<ParameterSyntax>();
        if (parameter == null)
            return document;

        // Convert float[] → ReadOnlySpan<float> or Span<float>
        var arrayType = parameter.Type as ArrayTypeSyntax;
        if (arrayType == null)
            return document;

        var elementType = arrayType.ElementType;
        var isOutput = parameter.Modifiers.Any(SyntaxKind.OutKeyword);

        var spanType = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier(isOutput ? "Span" : "ReadOnlySpan"),
            SyntaxFactory.TypeArgumentList(
                SyntaxFactory.SingletonSeparatedList(elementType)
            )
        );

        var newParameter = parameter.WithType(spanType);

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root!.ReplaceNode(parameter, newParameter);

        return document.WithSyntaxRoot(newRoot);
    }
}
```

## IDE Integration

### Real-Time Diagnostics

Diagnostics appear as you type:

```csharp
// DC001: Kernel method must be static
[Kernel]
public void VectorAdd(...)  // ❌ Error squiggle
{
}

// ✅ Quick fix applied
[Kernel]
public static void VectorAdd(...)
{
}
```

### Code Actions

```csharp
// DC011: Use Span<T> instead of array
[Kernel]
public static void Process(float[] data)  // 💡 Lightbulb appears
{
    // Quick action: "Convert to Span<T>"
}

// After applying fix:
[Kernel]
public static void Process(ReadOnlySpan<float> data)
{
}
```

### IntelliSense Support

Generated code has full IntelliSense:

```csharp
[Kernel]
public static void MyKernel(...)
{
    // IntelliSense shows:
    // - Kernel.ThreadId.X
    // - Kernel.ThreadId.Y
    // - Kernel.ThreadId.Z
    // - Kernel.BlockId
    // - Kernel.GridDim
}
```

## Performance Characteristics

### Generation Performance

| Metric | Typical | Maximum | Notes |
|--------|---------|---------|-------|
| **Clean build** | < 100ms | < 500ms | First generation |
| **Incremental** | < 10ms | < 50ms | Cached results |
| **Single kernel** | < 5ms | < 20ms | One method changed |
| **Large project** | < 200ms | < 1s | 100+ kernels |

### Analyzer Performance

| Operation | Time | Notes |
|-----------|------|-------|
| **Syntax check** | < 1ms | Fast path (no semantic) |
| **Full analysis** | < 5ms | Semantic analysis |
| **Code fix** | < 10ms | Document transformation |
| **Batch analysis** | < 100ms | 100+ methods |

## Testing Strategy

### Generator Tests

```csharp
[Fact]
public void GeneratesCorrectCpuCode()
{
    var source = @"
using DotCompute;
class C {
    [Kernel]
    public static void Add(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result) {
        int idx = Kernel.ThreadId.X;
        if (idx < result.Length) {
            result[idx] = a[idx] + b[idx];
        }
    }
}";

    var generated = GenerateSource(source);

    Assert.Contains("Add_CPU_SIMD", generated);
    Assert.Contains("Vector<float>", generated);
}
```

### Analyzer Tests

```csharp
[Fact]
public void DC001_ReportsError_ForNonStaticKernel()
{
    var code = @"
[Kernel]
public void {|DC001:MyKernel|}() { }";

    var expected = DiagnosticResult
        .CompilerError("DC001")
        .WithLocation(0)
        .WithArguments("MyKernel");

    VerifyAnalyzer(code, expected);
}
```

### Code Fix Tests

```csharp
[Fact]
public void DC001_CodeFix_AddsStaticModifier()
{
    var before = @"
[Kernel]
public void MyKernel() { }";

    var after = @"
[Kernel]
public static void MyKernel() { }";

    VerifyCodeFix(before, after, "DC001");
}
```

## Related Documentation

- [Architecture Overview](overview.md)
- [Kernel Development Guide](../guides/kernel-development.md)
- [Diagnostic Rules Reference](../reference/diagnostic-rules.md)
- [Kernel Attribute Reference](../reference/kernel-attribute.md)
