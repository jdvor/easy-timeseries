```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD EPYC 7763 2.64GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3


```
| Type                       | Method        | BitCount | RowCount | Cardinality | Shape    | Precision    | Mean         | Error      | StdDev     | Gen0   | Gen1   | Allocated |
|--------------------------- |-------------- |--------- |--------- |------------ |--------- |------------- |-------------:|-----------:|-----------:|-------:|-------:|----------:|
| **BlockBenchmark**             | **CreateBlock32** | **?**        | **?**        | **?**           | **?**        | **?**            |  **3,337.03 ns** |   **7.887 ns** |   **6.586 ns** |      **-** |      **-** |         **-** |
| BlockBenchmark             | CreateBlock64 | ?        | ?        | ?           | ?        | ?            |  4,208.27 ns |   2.988 ns |   2.333 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **1**        | **16**       | **?**           | **?**        | **?**            |     **79.05 ns** |   **0.210 ns** |   **0.186 ns** | **0.0067** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 1        | 16       | ?           | ?        | ?            |     27.07 ns |   0.021 ns |   0.018 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **1**        | **1024**     | **?**           | **?**        | **?**            |  **1,386.17 ns** |   **4.345 ns** |   **3.852 ns** | **0.0057** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 1        | 1024     | ?           | ?        | ?            |  1,403.22 ns |   2.138 ns |   1.895 ns |      - |      - |         - |
| **CategoryBenchmark**          | **Write**         | **?**        | **16**       | **8**           | **?**        | **?**            |    **114.95 ns** |   **0.295 ns** |   **0.262 ns** | **0.0100** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 16       | 8           | ?        | ?            |    718.30 ns |   3.853 ns |   3.416 ns | 0.1059 |      - |    1784 B |
| **CategoryBenchmark**          | **Write**         | **?**        | **1024**     | **8**           | **?**        | **?**            |  **2,800.52 ns** |   **8.205 ns** |   **7.274 ns** | **0.0076** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 1024     | 8           | ?        | ?            |  5,000.03 ns |   5.994 ns |   4.680 ns | 0.0992 |      - |    1784 B |
| **BoolBenchmark**              | **Write**         | **?**        | **16**       | **?**           | **?**        | **?**            |    **123.71 ns** |   **0.400 ns** |   **0.354 ns** | **0.0100** |      **-** |     **168 B** |
| DecimalBenchmark           | Write         | ?        | 16       | ?           | ?        | ?            |    448.37 ns |   0.771 ns |   0.684 ns | 0.0153 |      - |     256 B |
| BoolBenchmark              | Read          | ?        | 16       | ?           | ?        | ?            |     21.86 ns |   0.054 ns |   0.051 ns |      - |      - |         - |
| DecimalBenchmark           | Read          | ?        | 16       | ?           | ?        | ?            |    323.43 ns |   0.218 ns |   0.182 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **17**       | **16**       | **?**           | **?**        | **?**            |     **88.12 ns** |   **0.577 ns** |   **0.539 ns** | **0.0067** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 17       | 16       | ?           | ?        | ?            |     30.51 ns |   0.024 ns |   0.019 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **17**       | **1024**     | **?**           | **?**        | **?**            |  **1,977.37 ns** |   **9.113 ns** |   **8.078 ns** | **0.0038** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 17       | 1024     | ?           | ?        | ?            |  1,645.77 ns |   2.456 ns |   2.297 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **64**       | **16**       | **?**           | **?**        | **?**            |    **106.70 ns** |   **0.167 ns** |   **0.139 ns** | **0.0067** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 64       | 16       | ?           | ?        | ?            |     31.40 ns |   0.058 ns |   0.051 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **64**       | **1024**     | **?**           | **?**        | **?**            |  **3,388.85 ns** |   **6.234 ns** |   **5.526 ns** | **0.0038** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 64       | 1024     | ?           | ?        | ?            |  1,449.52 ns |   0.904 ns |   0.801 ns |      - |      - |         - |
| **CategoryBenchmark**          | **Write**         | **?**        | **16**       | **100**         | **?**        | **?**            |    **116.41 ns** |   **0.283 ns** |   **0.265 ns** | **0.0100** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 16       | 100         | ?        | ?            |  1,056.28 ns |   4.095 ns |   3.831 ns | 0.1526 |      - |    2584 B |
| **CategoryBenchmark**          | **Write**         | **?**        | **1024**     | **100**         | **?**        | **?**            |  **3,134.26 ns** |   **1.833 ns** |   **1.530 ns** | **0.0076** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 1024     | 100         | ?        | ?            | 10,152.68 ns |  20.300 ns |  18.989 ns | 0.7324 | 0.0153 |   12496 B |
| **BoolBenchmark**              | **Write**         | **?**        | **1024**     | **?**           | **?**        | **?**            |  **1,729.07 ns** |   **3.551 ns** |   **3.148 ns** | **0.0095** |      **-** |     **168 B** |
| DecimalBenchmark           | Write         | ?        | 1024     | ?           | ?        | ?            | 22,708.89 ns |  36.850 ns |  30.771 ns |      - |      - |     256 B |
| BoolBenchmark              | Read          | ?        | 1024     | ?           | ?        | ?            |  1,083.55 ns |   3.974 ns |   3.523 ns |      - |      - |         - |
| DecimalBenchmark           | Read          | ?        | 1024     | ?           | ?        | ?            | 20,414.65 ns |  57.764 ns |  51.206 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **16**       | **?**           | **Constant** | **?**            |    **120.98 ns** |   **0.334 ns** |   **0.296 ns** | **0.0129** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 16       | ?           | Constant | ?            |    130.04 ns |   0.242 ns |   0.202 ns | 0.0100 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 16       | ?           | Constant | ?            |     54.06 ns |   0.022 ns |   0.020 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 16       | ?           | Constant | ?            |     57.42 ns |   0.058 ns |   0.045 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **1024**     | **?**           | **Constant** | **?**            |  **3,849.83 ns** |   **3.223 ns** |   **2.691 ns** | **0.0076** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 1024     | ?           | Constant | ?            |  4,393.01 ns |   3.605 ns |   3.010 ns | 0.0076 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 1024     | ?           | Constant | ?            |  2,981.82 ns |   3.629 ns |   3.030 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 1024     | ?           | Constant | ?            |  2,995.08 ns |   1.418 ns |   1.326 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **16**       | **?**           | **Drift**    | **?**            |    **255.63 ns** |   **0.805 ns** |   **0.753 ns** | **0.0129** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 16       | ?           | Drift    | ?            |    335.46 ns |   0.382 ns |   0.319 ns | 0.0100 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 16       | ?           | Drift    | ?            |    131.48 ns |   0.113 ns |   0.094 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 16       | ?           | Drift    | ?            |    159.77 ns |   0.095 ns |   0.079 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **1024**     | **?**           | **Drift**    | **?**            | **12,182.71 ns** |  **22.211 ns** |  **19.689 ns** |      **-** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 1024     | ?           | Drift    | ?            | 15,221.48 ns |  19.046 ns |  14.870 ns |      - |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 1024     | ?           | Drift    | ?            |  7,047.05 ns |   1.801 ns |   1.596 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 1024     | ?           | Drift    | ?            |  8,175.43 ns |  29.010 ns |  24.224 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **16**       | **?**           | **Jumpy**    | **?**            |    **282.54 ns** |   **0.917 ns** |   **0.813 ns** | **0.0129** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 16       | ?           | Jumpy    | ?            |    369.11 ns |   0.513 ns |   0.455 ns | 0.0100 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 16       | ?           | Jumpy    | ?            |    151.64 ns |   0.348 ns |   0.291 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 16       | ?           | Jumpy    | ?            |    185.35 ns |   0.161 ns |   0.142 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **1024**     | **?**           | **Jumpy**    | **?**            | **12,064.21 ns** |   **7.847 ns** |   **6.553 ns** |      **-** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 1024     | ?           | Jumpy    | ?            | 19,803.57 ns |  29.013 ns |  24.227 ns |      - |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 1024     | ?           | Jumpy    | ?            |  6,586.85 ns |   8.929 ns |   8.352 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 1024     | ?           | Jumpy    | ?            |  9,208.08 ns |   9.525 ns |   7.954 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **16**       | **?**           | **?**        | **Milliseconds** |    **214.98 ns** |   **0.605 ns** |   **0.536 ns** | **0.0100** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 16       | ?           | ?        | Milliseconds |    349.24 ns |   0.411 ns |   0.343 ns | 0.0100 |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 16       | ?           | ?        | Milliseconds |    114.09 ns |   0.184 ns |   0.163 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 16       | ?           | ?        | Milliseconds |    239.70 ns |   0.079 ns |   0.062 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **1024**     | **?**           | **?**        | **Milliseconds** |  **9,151.87 ns** |  **18.102 ns** |  **15.116 ns** |      **-** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 1024     | ?           | ?        | Milliseconds | 16,912.11 ns | 136.557 ns | 127.736 ns |      - |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 1024     | ?           | ?        | Milliseconds |  6,774.89 ns |   5.144 ns |   4.296 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 1024     | ?           | ?        | Milliseconds | 10,308.85 ns |  12.847 ns |  10.728 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **16**       | **?**           | **?**        | **Seconds**      |    **181.52 ns** |   **0.729 ns** |   **0.682 ns** | **0.0100** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 16       | ?           | ?        | Seconds      |    340.18 ns |   0.854 ns |   0.798 ns | 0.0100 |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 16       | ?           | ?        | Seconds      |    105.02 ns |   0.299 ns |   0.250 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 16       | ?           | ?        | Seconds      |    192.66 ns |   0.332 ns |   0.277 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **1024**     | **?**           | **?**        | **Seconds**      |  **8,570.65 ns** |  **32.581 ns** |  **30.477 ns** |      **-** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 1024     | ?           | ?        | Seconds      | 14,916.54 ns |  32.533 ns |  30.432 ns |      - |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 1024     | ?           | ?        | Seconds      |  6,333.16 ns |  23.275 ns |  20.633 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 1024     | ?           | ?        | Seconds      |  9,584.99 ns |  25.944 ns |  22.999 ns |      - |      - |         - |
