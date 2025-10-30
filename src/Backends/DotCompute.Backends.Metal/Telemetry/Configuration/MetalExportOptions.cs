// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

namespace DotCompute.Backends.Metal.Telemetry;

/// <summary>
/// Configuration options for Metal metrics export
/// </summary>
public sealed class MetalExportOptions
{
    /// <summary>
    /// Gets or sets the export timeout
    /// </summary>
    public TimeSpan ExportTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the auto-export interval
    /// </summary>
    public TimeSpan AutoExportInterval { get; set; } = TimeSpan.Zero; // Disabled by default

    /// <summary>
    /// Gets or sets the configured exporters
    /// </summary>
    public IList<ExporterConfiguration> Exporters { get; } = [];
}
