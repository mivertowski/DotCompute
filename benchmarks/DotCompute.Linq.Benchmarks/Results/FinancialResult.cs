// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Linq.Benchmarks;

/// <summary>
/// Financial analysis result.
/// </summary>
public class FinancialResult
{
    public double AverageReturn { get; set; }
    public double Volatility { get; set; }
    public double Sharpe { get; set; }
    public int DataPoints { get; set; }
}