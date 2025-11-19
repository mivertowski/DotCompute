// Copyright (c) 2025 Michael Ivertowski
// Licensed under the MIT License. See LICENSE file in the project root for license information.

using System.Runtime.InteropServices;

namespace DotCompute.Backends.CUDA.Monitoring
{
    /// <summary>
    /// CUPTI Event and Metric API P/Invoke declarations (partial class extension).
    /// </summary>
    public sealed partial class CuptiWrapper
    {
        // ========================================
        // CUPTI Event and Metric API P/Invoke Declarations
        // ========================================

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiDeviceGetAttribute(
            uint device,
            CuptiDeviceAttribute attrib,
            out nuint valueSize,
            ref IntPtr value);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiDeviceEnumEventDomains(
            IntPtr device,
            ref nuint arraySizeBytes,
            IntPtr domainArray);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiEventDomainEnumEvents(
            IntPtr eventDomain,
            ref nuint arraySizeBytes,
            IntPtr eventArray);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiDeviceEnumMetrics(
            IntPtr device,
            ref nuint arraySizeBytes,
            IntPtr metricArray);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiMetricGetAttribute(
            uint metric,
            CuptiMetricAttribute attrib,
            ref nuint valueSize,
            byte[] value);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiEventGetAttribute(
            uint eventId,
            CuptiEventAttribute attrib,
            ref nuint valueSize,
            byte[] value);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiEventGroupCreate(
            IntPtr context,
            ref IntPtr eventGroup,
            uint flags);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiEventGroupAddEvent(
            IntPtr eventGroup,
            uint eventId);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiEventGroupReadEvent(
            IntPtr eventGroup,
            CuptiReadEventFlags flags,
            uint eventId,
            ref ulong eventValueBuffer,
            out nuint eventValueBufferSizeBytes);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiMetricGetNumEvents(
            uint metric,
            ref nuint numEvents);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiMetricEnumEvents(
            uint metric,
            ref nuint arraySizeBytes,
            [MarshalAs(UnmanagedType.LPArray)] uint[] eventIdArray);

        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [DllImport(CUPTI_LIBRARY)]
        private static extern CuptiResult cuptiMetricGetValue(
            int deviceId,
            uint metric,
            nuint eventIdArraySizeBytes,
            [MarshalAs(UnmanagedType.LPArray)] ulong[] eventIdArray,
            ulong timeDuration,
            ref CuptiMetricValueKind metricValueKind,
            ref double metricValue);
    }

    // ========================================
    // CUPTI Event and Metric Enums
    // ========================================

    /// <summary>
    /// CUPTI device attributes.
    /// </summary>
    internal enum CuptiDeviceAttribute
    {
        /// <summary>
        /// Maximum event ID.
        /// </summary>
        MaxEventId = 0,
        /// <summary>
        /// Maximum event domain ID.
        /// </summary>
        MaxEventDomainId = 1
    }

    /// <summary>
    /// CUPTI event attributes.
    /// </summary>
    internal enum CuptiEventAttribute
    {
        /// <summary>
        /// Event name.
        /// </summary>
        Name = 0,
        /// <summary>
        /// Short description.
        /// </summary>
        ShortDescription = 1,
        /// <summary>
        /// Long description.
        /// </summary>
        LongDescription = 2,
        /// <summary>
        /// Event category.
        /// </summary>
        Category = 3
    }

    /// <summary>
    /// CUPTI metric attributes.
    /// </summary>
    internal enum CuptiMetricAttribute
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        Name = 0,
        /// <summary>
        /// Short description.
        /// </summary>
        ShortDescription = 1,
        /// <summary>
        /// Long description.
        /// </summary>
        LongDescription = 2,
        /// <summary>
        /// Metric category.
        /// </summary>
        Category = 3,
        /// <summary>
        /// Value kind.
        /// </summary>
        ValueKind = 4,
        /// <summary>
        /// Evaluation mode.
        /// </summary>
        EvaluationMode = 5
    }

    /// <summary>
    /// CUPTI read event flags.
    /// </summary>
    [Flags]
    internal enum CuptiReadEventFlags : uint
    {
        /// <summary>
        /// No flags.
        /// </summary>
        None = 0,
        /// <summary>
        /// Reset event value after reading.
        /// </summary>
        ResetOnRead = 1
    }

    /// <summary>
    /// CUPTI metric value kind.
    /// </summary>
    internal enum CuptiMetricValueKind
    {
        /// <summary>
        /// Double precision value.
        /// </summary>
        Double = 0,
        /// <summary>
        /// Unsigned 64-bit integer value.
        /// </summary>
        Uint64 = 1,
        /// <summary>
        /// Percentage value (0.0-1.0).
        /// </summary>
        Percent = 2,
        /// <summary>
        /// Throughput value (bytes/second).
        /// </summary>
        Throughput = 3,
        /// <summary>
        /// Integer value.
        /// </summary>
        Int64 = 4,
        /// <summary>
        /// Utilization level (0-10).
        /// </summary>
        UtilizationLevel = 5
    }
}
