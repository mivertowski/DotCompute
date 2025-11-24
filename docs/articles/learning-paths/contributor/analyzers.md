# Analyzer Development

This module covers creating Roslyn analyzers to validate GPU kernel code and provide real-time feedback in the IDE.

## Why Analyzers?

Analyzers provide:

- **Real-time feedback** in the IDE as you type
- **Compile-time validation** catches errors before runtime
- **Automated code fixes** for common issues
- **Enforced best practices** across the codebase

## DotCompute Analyzer IDs

| ID | Description |
|----|-------------|
| DC001 | Kernel method must be static |
| DC002 | Kernel parameter type not supported |
| DC003 | Missing bounds check in kernel |
| DC004 | Unsupported operation in kernel |
| DC005 | Thread ID accessed outside bounds check |
| DC006 | Potential data race detected |
| DC007 | Inefficient memory access pattern |
| DC008 | Shared memory size exceeds limit |
| DC009 | Barrier in divergent code path |
| DC010 | Register pressure too high |
| DC011 | Invalid Ring Kernel configuration |
| DC012 | Message type not MemoryPackable |

## Analyzer Structure

### Basic Analyzer

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class KernelStaticAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "DC001",
        title: "Kernel method must be static",
        messageFormat: "Kernel method '{0}' must be declared static",
        category: "DotCompute.Kernel",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "GPU kernels cannot access instance state and must be static methods.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Check for [Kernel] attribute
        if (!HasKernelAttribute(method, context.SemanticModel))
            return;

        // Check if static
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
        {
            var diagnostic = Diagnostic.Create(
                Rule,
                method.Identifier.GetLocation(),
                method.Identifier.Text);

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasKernelAttribute(MethodDeclarationSyntax method, SemanticModel model)
    {
        var symbol = model.GetDeclaredSymbol(method);
        return symbol?.GetAttributes()
            .Any(a => a.AttributeClass?.Name is "KernelAttribute" or "RingKernelAttribute") == true;
    }
}
```

### Analyzer with Code Fix

```csharp
[ExportCodeFixProvider(LanguageNames.CSharp)]
public class KernelStaticCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create("DC001");

    public override FixAllProvider? GetFixAllProvider() =>
        WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var method = root?.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .First();

        if (method == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Make method static",
                createChangedDocument: c => MakeStaticAsync(context.Document, method, c),
                equivalenceKey: "MakeStatic"),
            diagnostic);
    }

    private static async Task<Document> MakeStaticAsync(
        Document document,
        MethodDeclarationSyntax method,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root == null) return document;

        var newModifiers = method.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
        var newMethod = method.WithModifiers(newModifiers);

        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
```

## Common Analyzer Patterns

### Pattern 1: Bounds Check Analyzer

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BoundsCheckAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "DC003",
        title: "Missing bounds check in kernel",
        messageFormat: "Kernel accesses index '{0}' without bounds checking",
        category: "DotCompute.Safety",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeKernel, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeKernel(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!HasKernelAttribute(method, context.SemanticModel))
            return;

        var body = method.Body ?? method.ExpressionBody?.Expression.Parent as BlockSyntax;
        if (body == null) return;

        // Find all array accesses
        var arrayAccesses = body.DescendantNodes()
            .OfType<ElementAccessExpressionSyntax>()
            .ToList();

        // Find all bounds checks
        var boundsChecks = FindBoundsChecks(body);

        foreach (var access in arrayAccesses)
        {
            var indexExpr = access.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (indexExpr == null) continue;

            // Check if access is protected by bounds check
            if (!IsProtectedByBoundsCheck(access, boundsChecks))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    access.GetLocation(),
                    indexExpr.ToString()));
            }
        }
    }

    private static bool IsProtectedByBoundsCheck(
        ElementAccessExpressionSyntax access,
        IEnumerable<IfStatementSyntax> boundsChecks)
    {
        foreach (var ifStatement in boundsChecks)
        {
            // Check if access is within the if statement body
            if (ifStatement.Statement.Contains(access) ||
                (ifStatement.Statement is BlockSyntax block &&
                 block.Statements.Any(s => s.Contains(access))))
            {
                return true;
            }
        }
        return false;
    }
}
```

### Pattern 2: Data Race Detection

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class DataRaceAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "DC006",
        title: "Potential data race detected",
        messageFormat: "Potential data race: '{0}' is written by multiple threads without synchronization",
        category: "DotCompute.Concurrency",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override void Initialize(AnalysisContext context)
    {
        context.RegisterSyntaxNodeAction(AnalyzeKernel, SyntaxKind.MethodDeclaration);
    }

    private void AnalyzeKernel(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!HasKernelAttribute(method, context.SemanticModel))
            return;

        var body = method.Body;
        if (body == null) return;

        // Find all assignments to output buffers
        var assignments = body.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(a => IsBufferAccess(a.Left))
            .ToList();

        foreach (var assignment in assignments)
        {
            var access = assignment.Left as ElementAccessExpressionSyntax;
            if (access == null) continue;

            var indexExpr = access.ArgumentList.Arguments.FirstOrDefault()?.Expression;

            // Check if index is based on thread ID (safe) or fixed value (race)
            if (!IsThreadIdBased(indexExpr, context.SemanticModel))
            {
                // Fixed index write - potential race
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    assignment.GetLocation(),
                    access.Expression.ToString()));
            }
        }
    }

    private static bool IsThreadIdBased(ExpressionSyntax? expr, SemanticModel model)
    {
        if (expr == null) return false;

        // Check for Kernel.ThreadId.X/Y/Z
        var text = expr.ToString();
        return text.Contains("ThreadId") ||
               text.Contains("BlockId") ||
               text.Contains("GlobalId");
    }
}
```

### Pattern 3: Memory Access Pattern Analyzer

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MemoryCoalescingAnalyzer : DiagnosticAnalyzer
{
    public static readonly DiagnosticDescriptor Rule = new(
        id: "DC007",
        title: "Inefficient memory access pattern",
        messageFormat: "Non-coalesced memory access detected: stride {0}",
        category: "DotCompute.Performance",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private void AnalyzeKernel(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!HasKernelAttribute(method, context.SemanticModel))
            return;

        var accesses = method.DescendantNodes()
            .OfType<ElementAccessExpressionSyntax>()
            .ToList();

        foreach (var access in accesses)
        {
            var indexExpr = access.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (indexExpr == null) continue;

            // Detect strided access patterns
            if (indexExpr is BinaryExpressionSyntax binary &&
                binary.OperatorToken.IsKind(SyntaxKind.AsteriskToken))
            {
                // Pattern: threadId * stride
                var stride = TryGetConstantValue(binary.Right, context.SemanticModel);
                if (stride > 1)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        Rule,
                        access.GetLocation(),
                        stride));
                }
            }
        }
    }
}
```

## Testing Analyzers

### Unit Testing

```csharp
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;

public class KernelStaticAnalyzerTests
{
    [Fact]
    public async Task ReportsNonStaticKernel()
    {
        var test = @"
using DotCompute.Generators.Kernel.Attributes;

public class MyClass
{
    [Kernel]
    public void {|DC001:NonStaticKernel|}(Span<float> data) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task NoReportForStaticKernel()
    {
        var test = @"
using DotCompute.Generators.Kernel.Attributes;

public class MyClass
{
    [Kernel]
    public static void StaticKernel(Span<float> data) { }
}";

        await VerifyAnalyzerAsync(test);
    }

    private static async Task VerifyAnalyzerAsync(string source)
    {
        var test = new CSharpAnalyzerTest<KernelStaticAnalyzer, XUnitVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80
        };

        test.TestState.AdditionalReferences.Add(
            MetadataReference.CreateFromFile(typeof(KernelAttribute).Assembly.Location));

        await test.RunAsync();
    }
}
```

### Code Fix Testing

```csharp
public class KernelStaticCodeFixTests
{
    [Fact]
    public async Task FixesNonStaticKernel()
    {
        var test = @"
using DotCompute.Generators.Kernel.Attributes;

public class MyClass
{
    [Kernel]
    public void {|DC001:NonStaticKernel|}(Span<float> data) { }
}";

        var fixedCode = @"
using DotCompute.Generators.Kernel.Attributes;

public class MyClass
{
    [Kernel]
    public static void NonStaticKernel(Span<float> data) { }
}";

        await VerifyCodeFixAsync(test, fixedCode);
    }

    private static async Task VerifyCodeFixAsync(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<KernelStaticAnalyzer, KernelStaticCodeFixProvider, XUnitVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource
        };

        await test.RunAsync();
    }
}
```

## Analyzer Configuration

### editorconfig Support

```csharp
public class ConfigurableAnalyzer : DiagnosticAnalyzer
{
    public override void Initialize(AnalysisContext context)
    {
        context.RegisterCompilationStartAction(compilationContext =>
        {
            // Read configuration
            var options = compilationContext.Options.AnalyzerConfigOptionsProvider;

            compilationContext.RegisterSyntaxNodeAction(nodeContext =>
            {
                var configOptions = options.GetOptions(nodeContext.Node.SyntaxTree);

                // Check for suppression
                if (configOptions.TryGetValue("dotnet_diagnostic.DC007.severity", out var severity) &&
                    severity == "none")
                {
                    return; // Suppressed
                }

                // Analyze...
            }, SyntaxKind.MethodDeclaration);
        });
    }
}
```

### User configuration example:

```ini
# .editorconfig
[*.cs]
dotnet_diagnostic.DC007.severity = suggestion
dotnet_diagnostic.DC003.severity = error

# Suppress in test files
[*Tests.cs]
dotnet_diagnostic.DC003.severity = none
```

## Exercises

### Exercise 1: Barrier Divergence Analyzer

Create an analyzer that detects barriers inside if statements.

### Exercise 2: Register Pressure Estimator

Create an analyzer that estimates register usage and warns when high.

### Exercise 3: Code Fix for Bounds Check

Create a code fix that wraps array access in bounds check.

## Key Takeaways

1. **Analyzers provide real-time feedback** in the IDE
2. **Code fixes** help users resolve issues quickly
3. **Thorough testing** ensures reliability
4. **Configuration support** allows customization
5. **Concurrent execution** improves IDE performance

## Next Module

[Testing and Benchmarking →](testing-benchmarking.md)

Learn to implement comprehensive tests and benchmarks.

## Further Reading

- [Analyzer System](../../architecture/analyzers.md) - Architecture details
- [Diagnostic Rules](../../reference/diagnostic-rules.md) - Complete rule reference
