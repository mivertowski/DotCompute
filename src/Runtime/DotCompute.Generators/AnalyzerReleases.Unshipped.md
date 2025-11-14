; Unshipped analyzer release
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
DC0001 | DotCompute.Kernel | Error | Unsupported type in kernel
DC0002 | DotCompute.Kernel | Error | Kernel method missing buffer parameter
DC0003 | DotCompute.Kernel | Error | Invalid vector size
DC0004 | DotCompute.Kernel | Warning | Unsafe code context required
DC0005 | DotCompute.Performance | Warning | Potential performance issue
DC001 | DotCompute.Kernel | Error | Kernel methods must be static
DC002 | DotCompute.Kernel | Error | Kernel method has invalid parameters
DC003 | DotCompute.Kernel | Error | Kernel method uses unsupported language construct
DC004 | DotCompute.Performance | Info | Kernel can benefit from vectorization
DC005 | DotCompute.Performance | Warning | Kernel has suboptimal memory access pattern
DC006 | DotCompute.Performance | Warning | Kernel may experience register spilling
DC007 | DotCompute.Usage | Info | Method should have [Kernel] attribute
DC008 | DotCompute.Maintainability | Info | Kernel can be simplified
DC009 | DotCompute.Reliability | Warning | Potential thread safety issue in kernel
DC010 | DotCompute.Usage | Warning | Kernel uses incorrect threading model
DC011 | DotCompute.Reliability | Warning | Kernel missing bounds check
DC012 | DotCompute.Performance | Info | Kernel backend selection can be optimized
DC013 | DotCompute.RingKernel | Warning | RingKernel specifies unpublished backend
DC014 | DotCompute.Telemetry | Warning | Telemetry polling interval is too frequent
DC015 | DotCompute.Telemetry | Error | Telemetry must be enabled before polling
DC016 | DotCompute.Telemetry | Error | Telemetry cannot be enabled before kernel launch
DC017 | DotCompute.Telemetry | Warning | Telemetry reset requires enabled telemetry