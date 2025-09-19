// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Custom column to display optimization impact in benchmark results.
/// </summary>
public class OptimizationImpactColumn : IColumn
{
    public string Id => nameof(OptimizationImpactColumn);
    public string ColumnName => "Impact";
    public bool AlwaysShow => true;
    public ColumnCategory Category => ColumnCategory.Custom;
    public int PriorityInCategory => 0;
    public bool IsNumeric => true;
    public UnitType UnitType => UnitType.Dimensionless;
    public string Legend => "Optimization impact as speedup factor";

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        var baseline = summary.GetBaseline();
        if (baseline == null) return "-";
        
        var baselineResult = summary[baseline];
        var currentResult = summary[benchmarkCase];
        
        if (baselineResult?.ResultStatistics?.Mean == null || 
            currentResult?.ResultStatistics?.Mean == null)
            return "-";
            
        var impact = baselineResult.ResultStatistics.Mean / currentResult.ResultStatistics.Mean;
        return $"{impact:F2}x";
    }

    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => GetValue(summary, benchmarkCase);
    public bool IsAvailable(Summary summary) => true;
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
}