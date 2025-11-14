// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DotCompute.Generators.Analyzers;

/// <summary>
/// Roslyn analyzer for validating Ring Kernel telemetry usage patterns.
/// Detects common mistakes like polling too frequently, missing SetTelemetryEnabledAsync,
/// or calling telemetry methods in the wrong order.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RingKernelTelemetryAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        KernelDiagnostics.TelemetryPollingTooFrequent,
        KernelDiagnostics.TelemetryNotEnabled,
        KernelDiagnostics.TelemetryEnabledBeforeLaunch,
        KernelDiagnostics.TelemetryResetWithoutEnable
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Analyze method bodies for telemetry call sequences
        context.RegisterOperationBlockAction(AnalyzeTelemetryCallSequence);

        // Analyze individual invocations for polling frequency
        context.RegisterOperationAction(AnalyzeGetTelemetryInvocation, OperationKind.Invocation);
    }

    /// <summary>
    /// Analyzes method bodies to detect incorrect telemetry call sequences.
    /// Validates that SetTelemetryEnabledAsync is called after LaunchAsync and before GetTelemetryAsync.
    /// </summary>
    private static void AnalyzeTelemetryCallSequence(OperationBlockAnalysisContext context)
    {
        var operations = context.OperationBlocks.SelectMany(block => block.Descendants()).ToList();

        // Track telemetry-related method calls in this block
        var launchCalls = new List<IInvocationOperation>();
        var enableCalls = new List<IInvocationOperation>();
        var getTelemetryCalls = new List<IInvocationOperation>();
        var resetTelemetryCalls = new List<IInvocationOperation>();

        foreach (var operation in operations)
        {
            if (operation is not IInvocationOperation invocation)
            {
                continue;
            }

            var methodName = invocation.TargetMethod.Name;

            if (methodName == "LaunchAsync")
            {
                launchCalls.Add(invocation);
            }
            else if (methodName == "SetTelemetryEnabledAsync")
            {
                enableCalls.Add(invocation);
            }
            else if (methodName == "GetTelemetryAsync")
            {
                getTelemetryCalls.Add(invocation);
            }
            else if (methodName == "ResetTelemetryAsync")
            {
                resetTelemetryCalls.Add(invocation);
            }
        }

        // DC016: Check if SetTelemetryEnabledAsync is called before LaunchAsync
        foreach (var enableCall in enableCalls)
        {
            var hasLaunchBefore = launchCalls.Any(launch =>
                launch.Syntax.SpanStart < enableCall.Syntax.SpanStart &&
                AreSameInstance(launch, enableCall));

            if (!hasLaunchBefore)
            {
                var wrapperName = GetWrapperInstanceName(enableCall);
                context.ReportDiagnostic(Diagnostic.Create(
                    KernelDiagnostics.TelemetryEnabledBeforeLaunch,
                    enableCall.Syntax.GetLocation(),
                    wrapperName));
            }
        }

        // DC015: Check if GetTelemetryAsync is called without prior SetTelemetryEnabledAsync(true)
        foreach (var getTelemetryCall in getTelemetryCalls)
        {
            var hasEnableBefore = enableCalls.Any(enable =>
                enable.Syntax.SpanStart < getTelemetryCall.Syntax.SpanStart &&
                AreSameInstance(enable, getTelemetryCall) &&
                IsEnabledTrue(enable));

            if (!hasEnableBefore)
            {
                var wrapperName = GetWrapperInstanceName(getTelemetryCall);
                context.ReportDiagnostic(Diagnostic.Create(
                    KernelDiagnostics.TelemetryNotEnabled,
                    getTelemetryCall.Syntax.GetLocation(),
                    wrapperName));
            }
        }

        // DC017: Check if ResetTelemetryAsync is called without prior SetTelemetryEnabledAsync(true)
        foreach (var resetCall in resetTelemetryCalls)
        {
            var hasEnableBefore = enableCalls.Any(enable =>
                enable.Syntax.SpanStart < resetCall.Syntax.SpanStart &&
                AreSameInstance(enable, resetCall) &&
                IsEnabledTrue(enable));

            if (!hasEnableBefore)
            {
                var wrapperName = GetWrapperInstanceName(resetCall);
                context.ReportDiagnostic(Diagnostic.Create(
                    KernelDiagnostics.TelemetryResetWithoutEnable,
                    resetCall.Syntax.GetLocation(),
                    wrapperName));
            }
        }
    }

    /// <summary>
    /// Analyzes GetTelemetryAsync invocations to detect excessive polling frequency.
    /// Detects patterns like tight loops or Task.Delay intervals &lt; 100μs.
    /// </summary>
    private static void AnalyzeGetTelemetryInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (invocation.TargetMethod.Name != "GetTelemetryAsync")
        {
            return;
        }

        // Check if GetTelemetryAsync is inside a tight loop
        var loopAncestor = invocation.Syntax.Ancestors().OfType<StatementSyntax>()
            .FirstOrDefault(s => s is WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax);

        if (loopAncestor != null)
        {
            // Check if there's a delay in the loop
            var loopBody = loopAncestor switch
            {
                WhileStatementSyntax whileLoop => whileLoop.Statement,
                ForStatementSyntax forLoop => forLoop.Statement,
                ForEachStatementSyntax foreachLoop => foreachLoop.Statement,
                _ => null
            };

            if (loopBody != null && !HasSufficientDelay(loopBody))
            {
                // Report warning for tight loop without delay
                context.ReportDiagnostic(Diagnostic.Create(
                    KernelDiagnostics.TelemetryPollingTooFrequent,
                    invocation.Syntax.GetLocation(),
                    "<100"));
            }
        }
    }

    /// <summary>
    /// Checks if a loop body contains Task.Delay with sufficient interval (>= 100μs).
    /// </summary>
    private static bool HasSufficientDelay(StatementSyntax loopBody)
    {
        var invocations = loopBody.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "Delay")
            {
                // Check if the delay argument is >= 1 (milliseconds)
                var delayArgument = invocation.ArgumentList.Arguments.FirstOrDefault();
                if (delayArgument?.Expression is LiteralExpressionSyntax literal &&
                    literal.Token.ValueText != null)
                {
                    if (int.TryParse(literal.Token.ValueText, out var delayMs) && delayMs >= 1)
                    {
                        return true; // Sufficient delay (>= 1ms)
                    }
                }

                // Check for TimeSpan.FromMicroseconds or TimeSpan.FromMilliseconds
                if (delayArgument?.Expression is InvocationExpressionSyntax timeSpanInvocation &&
                    timeSpanInvocation.Expression is MemberAccessExpressionSyntax timeSpanMember)
                {
                    var methodName = timeSpanMember.Name.Identifier.Text;
                    if (methodName is "FromMilliseconds" or "FromSeconds")
                    {
                        return true; // Milliseconds or seconds are always sufficient
                    }
                    else if (methodName == "FromMicroseconds")
                    {
                        var microArgument = timeSpanInvocation.ArgumentList.Arguments.FirstOrDefault();
                        if (microArgument?.Expression is LiteralExpressionSyntax microLiteral &&
                            int.TryParse(microLiteral.Token.ValueText, out var microValue) &&
                            microValue >= 100)
                        {
                            return true; // >= 100μs
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if two invocations are on the same wrapper instance.
    /// </summary>
    private static bool AreSameInstance(IInvocationOperation invocation1, IInvocationOperation invocation2)
    {
        // Compare the instance expressions (the object being called on)
        var instance1 = GetInstanceExpression(invocation1);
        var instance2 = GetInstanceExpression(invocation2);

        if (instance1 == null || instance2 == null)
        {
            return false;
        }

        // Simple comparison: same identifier name
        return instance1.ToString() == instance2.ToString();
    }

    /// <summary>
    /// Extracts the instance expression from an invocation (e.g., "wrapper" from "wrapper.LaunchAsync()").
    /// </summary>
    private static IOperation? GetInstanceExpression(IInvocationOperation invocation)
    {
        return invocation.Instance;
    }

    /// <summary>
    /// Gets the wrapper instance name from an invocation.
    /// </summary>
    private static string GetWrapperInstanceName(IInvocationOperation invocation)
    {
        var instance = GetInstanceExpression(invocation);
        return instance?.Syntax.ToString() ?? "wrapper";
    }

    /// <summary>
    /// Checks if SetTelemetryEnabledAsync is called with `true` argument.
    /// </summary>
    private static bool IsEnabledTrue(IInvocationOperation invocation)
    {
        if (invocation.Arguments.Length == 0)
        {
            return false;
        }

        var firstArg = invocation.Arguments[0];
        if (firstArg.Value.ConstantValue.HasValue &&
            firstArg.Value.ConstantValue.Value is bool boolValue)
        {
            return boolValue;
        }

        // Check for literal true
        if (firstArg.Value.Syntax is LiteralExpressionSyntax literal &&
            literal.Token.ValueText == "true")
        {
            return true;
        }

        return false;
    }
}
