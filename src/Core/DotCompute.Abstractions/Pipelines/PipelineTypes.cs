// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

#pragma warning disable CS0246 // Type or namespace name not found (incomplete pipeline types)

//
// IMPORTANT: This file provides backward compatibility by re-exporting types from
// their new organized locations. New code should reference types directly from:
// - DotCompute.Abstractions.Pipelines.Types
// - DotCompute.Abstractions.Pipelines.Configuration
// - DotCompute.Abstractions.Pipelines.Streaming
// - DotCompute.Abstractions.Pipelines.Optimization
// - DotCompute.Abstractions.Pipelines.Caching
// - DotCompute.Abstractions.Pipelines.Execution
//

// Re-export core pipeline types
global using DotCompute.Abstractions.Pipelines.Types.PipelineState;
global using DotCompute.Abstractions.Pipelines.Types.EventSeverity;
global using DotCompute.Abstractions.Pipelines.Types.PipelineEventType;
global using DotCompute.Abstractions.Pipelines.Types.PipelineEvent;
global using DotCompute.Abstractions.Pipelines.Types.PipelineExecutionStartedEvent;
global using DotCompute.Abstractions.Pipelines.Types.PipelineExecutionCompletedEvent;
global using DotCompute.Abstractions.Pipelines.Types.StageExecutionStartedEvent;
global using DotCompute.Abstractions.Pipelines.Types.StageExecutionCompletedEvent;

// Re-export configuration types
global using DotCompute.Abstractions.Pipelines.Configuration.IPipelineConfiguration;
global using DotCompute.Abstractions.Pipelines.Configuration.PipelineStageOptions;
global using DotCompute.Abstractions.Pipelines.Configuration.MemoryAllocationHints;
global using DotCompute.Abstractions.Pipelines.Configuration.RetryConfiguration;
global using DotCompute.Abstractions.Pipelines.Configuration.BackoffStrategy;
global using DotCompute.Abstractions.Pipelines.Configuration.ErrorHandlingStrategy;
global using DotCompute.Abstractions.Pipelines.Configuration.ResourceAllocationPreferences;
global using DotCompute.Abstractions.Pipelines.Configuration.ResourceSharingPolicy;

// Re-export streaming types
global using DotCompute.Abstractions.Pipelines.Streaming.IStreamingExecutionContext;
global using DotCompute.Abstractions.Pipelines.Streaming.StreamingConfiguration;
global using DotCompute.Abstractions.Pipelines.Streaming.BackpressureStrategy;
global using DotCompute.Abstractions.Pipelines.Streaming.StreamingErrorHandling;

// Re-export optimization types
global using DotCompute.Abstractions.Pipelines.Optimization.OptimizationStrategy;
global using DotCompute.Abstractions.Pipelines.Optimization.OptimizationContext;
global using DotCompute.Abstractions.Pipelines.Optimization.PerformanceGoals;
global using DotCompute.Abstractions.Pipelines.Optimization.PerformanceWeights;
global using DotCompute.Abstractions.Pipelines.Optimization.OptimizationConstraints;

// Re-export caching types
global using DotCompute.Abstractions.Pipelines.Caching.CachePolicy;
global using DotCompute.Abstractions.Pipelines.Caching.LRUCachePolicy;
global using DotCompute.Abstractions.Pipelines.Caching.TTLCachePolicy;
global using DotCompute.Abstractions.Pipelines.Caching.ICacheEntry;

// Re-export execution types
global using DotCompute.Abstractions.Pipelines.Execution.IPipelineExecutionContext;
global using DotCompute.Abstractions.Pipelines.Execution.IPipelineExecutionResult;
global using DotCompute.Abstractions.Pipelines.Execution.MemoryAllocationStrategy;

namespace DotCompute.Abstractions.Pipelines;

// This namespace now serves as a compatibility layer.
// All types have been reorganized into focused sub-namespaces for better maintainability.
