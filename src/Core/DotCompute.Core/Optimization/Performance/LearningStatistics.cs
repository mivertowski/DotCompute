// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Core.Optimization.Performance;

/// <summary>
/// Statistics about the learning effectiveness of the adaptive system.
/// </summary>
public class LearningStatistics
{
    public int TotalPerformanceSamples { get; set; }
    public int AverageSamplesPerWorkload { get; set; }
    public int WorkloadsWithSufficientHistory { get; set; }
    public float LearningEffectiveness { get; set; }
}