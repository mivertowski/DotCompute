```

BenchmarkDotNet v0.15.2, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
Intel Core Ultra 7 165H, 1 CPU, 22 logical and 11 physical cores
.NET SDK 9.0.203
  [Host]     : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2
  Job-OIOVDM : .NET 9.0.4 (9.0.425.16305), X64 RyuJIT AVX2

IterationCount=3  RunStrategy=ColdStart  WarmupCount=1  

```
| Method         | DataSize | Mean       | Error        | StdDev     | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------- |--------- |-----------:|-------------:|-----------:|----------:|------:|--------:|----------:|------------:|
| StandardLinq   | 10000    | 2,141.3 μs |  64,201.4 μs | 3,519.1 μs | 123.93 μs | 13.37 |   25.38 |      48 B |        1.00 |
| ParallelLinq   | 10000    | 4,616.9 μs | 120,445.5 μs | 6,602.0 μs | 841.57 μs | 28.84 |   48.79 |   12696 B |      264.50 |
| SimdOptimized  | 10000    |   584.0 μs |  17,452.7 μs |   956.6 μs |  32.33 μs |  3.65 |    6.90 |         - |        0.00 |
| TheoreticalGpu | 10000    |   609.8 μs |  18,108.7 μs |   992.6 μs |  37.44 μs |  3.81 |    7.17 |         - |        0.00 |
