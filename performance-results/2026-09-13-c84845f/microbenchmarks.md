```

BenchmarkDotNet v0.15.8, Linux Ubuntu 22.04.5 LTS (Jammy Jellyfish)
AMD EPYC 7763 3.21GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 10.0.400
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3
  Job-IJPESX : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v3

IterationCount=5  LaunchCount=1  WarmupCount=3  

```
| Type                       | Method        | BitCount | RowCount | Cardinality | Shape    | Precision    | Mean         | Error      | StdDev     | Gen0   | Gen1   | Allocated |
|--------------------------- |-------------- |--------- |--------- |------------ |--------- |------------- |-------------:|-----------:|-----------:|-------:|-------:|----------:|
| **BlockBenchmark**             | **CreateBlock32** | **?**        | **?**        | **?**           | **?**        | **?**            |  **3,334.67 ns** |  **13.772 ns** |   **2.131 ns** |      **-** |      **-** |         **-** |
| BlockBenchmark             | CreateBlock64 | ?        | ?        | ?           | ?        | ?            |  4,209.98 ns |   9.971 ns |   1.543 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **1**        | **16**       | **?**           | **?**        | **?**            |     **76.73 ns** |   **0.724 ns** |   **0.188 ns** | **0.0067** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 1        | 16       | ?           | ?        | ?            |     27.06 ns |   0.019 ns |   0.003 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **1**        | **1024**     | **?**           | **?**        | **?**            |  **1,479.60 ns** |   **7.752 ns** |   **1.200 ns** | **0.0057** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 1        | 1024     | ?           | ?        | ?            |  1,404.61 ns |   5.050 ns |   0.782 ns |      - |      - |         - |
| **CategoryBenchmark**          | **Write**         | **?**        | **16**       | **8**           | **?**        | **?**            |    **110.51 ns** |   **0.464 ns** |   **0.120 ns** | **0.0100** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 16       | 8           | ?        | ?            |    646.70 ns |  13.989 ns |   3.633 ns | 0.1059 |      - |    1784 B |
| **CategoryBenchmark**          | **Write**         | **?**        | **1024**     | **8**           | **?**        | **?**            |  **2,790.60 ns** |  **73.720 ns** |  **11.408 ns** | **0.0076** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 1024     | 8           | ?        | ?            |  4,994.85 ns |  22.140 ns |   3.426 ns | 0.0992 |      - |    1784 B |
| **BoolBenchmark**              | **Write**         | **?**        | **16**       | **?**           | **?**        | **?**            |    **120.01 ns** |   **1.065 ns** |   **0.165 ns** | **0.0100** |      **-** |     **168 B** |
| DecimalBenchmark           | Write         | ?        | 16       | ?           | ?        | ?            |    448.44 ns |   6.787 ns |   1.763 ns | 0.0153 |      - |     256 B |
| BoolBenchmark              | Read          | ?        | 16       | ?           | ?        | ?            |     21.50 ns |   0.157 ns |   0.041 ns |      - |      - |         - |
| DecimalBenchmark           | Read          | ?        | 16       | ?           | ?        | ?            |    322.99 ns |   0.358 ns |   0.055 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **17**       | **16**       | **?**           | **?**        | **?**            |     **82.46 ns** |   **1.125 ns** |   **0.292 ns** | **0.0067** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 17       | 16       | ?           | ?        | ?            |     30.91 ns |   0.588 ns |   0.091 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **17**       | **1024**     | **?**           | **?**        | **?**            |  **2,047.14 ns** |  **33.302 ns** |   **8.648 ns** | **0.0038** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 17       | 1024     | ?           | ?        | ?            |  1,653.86 ns |  15.239 ns |   3.958 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **64**       | **16**       | **?**           | **?**        | **?**            |    **104.95 ns** |   **1.550 ns** |   **0.240 ns** | **0.0067** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 64       | 16       | ?           | ?        | ?            |     31.28 ns |   0.346 ns |   0.054 ns |      - |      - |         - |
| **BitBufferBenchmark**         | **Write**         | **64**       | **1024**     | **?**           | **?**        | **?**            |  **3,318.81 ns** |   **6.627 ns** |   **1.026 ns** | **0.0038** |      **-** |     **112 B** |
| BitBufferBenchmark         | Read          | 64       | 1024     | ?           | ?        | ?            |  1,450.74 ns |   2.852 ns |   0.441 ns |      - |      - |         - |
| **CategoryBenchmark**          | **Write**         | **?**        | **16**       | **100**         | **?**        | **?**            |    **110.82 ns** |   **0.964 ns** |   **0.149 ns** | **0.0100** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 16       | 100         | ?        | ?            |  1,069.02 ns |  46.382 ns |  12.045 ns | 0.1526 |      - |    2584 B |
| **CategoryBenchmark**          | **Write**         | **?**        | **1024**     | **100**         | **?**        | **?**            |  **3,144.83 ns** |  **34.980 ns** |   **9.084 ns** | **0.0076** |      **-** |     **168 B** |
| CategoryBenchmark          | Read          | ?        | 1024     | 100         | ?        | ?            | 10,272.19 ns |  94.506 ns |  24.543 ns | 0.7324 | 0.0153 |   12496 B |
| **BoolBenchmark**              | **Write**         | **?**        | **1024**     | **?**           | **?**        | **?**            |  **1,734.48 ns** |  **21.956 ns** |   **5.702 ns** | **0.0095** |      **-** |     **168 B** |
| DecimalBenchmark           | Write         | ?        | 1024     | ?           | ?        | ?            | 22,878.27 ns |  51.047 ns |   7.900 ns |      - |      - |     256 B |
| BoolBenchmark              | Read          | ?        | 1024     | ?           | ?        | ?            |  1,199.91 ns |  10.260 ns |   1.588 ns |      - |      - |         - |
| DecimalBenchmark           | Read          | ?        | 1024     | ?           | ?        | ?            | 18,998.18 ns |  27.601 ns |   4.271 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **16**       | **?**           | **Constant** | **?**            |    **113.67 ns** |   **3.640 ns** |   **0.563 ns** | **0.0129** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 16       | ?           | Constant | ?            |    130.21 ns |   6.239 ns |   1.620 ns | 0.0100 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 16       | ?           | Constant | ?            |     56.94 ns |   8.634 ns |   2.242 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 16       | ?           | Constant | ?            |     60.36 ns |   1.522 ns |   0.395 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **1024**     | **?**           | **Constant** | **?**            |  **3,839.75 ns** |  **19.156 ns** |   **2.964 ns** | **0.0076** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 1024     | ?           | Constant | ?            |  4,398.68 ns |  56.527 ns |   8.748 ns | 0.0076 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 1024     | ?           | Constant | ?            |  2,994.26 ns |  11.939 ns |   1.848 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 1024     | ?           | Constant | ?            |  2,982.33 ns |   2.258 ns |   0.586 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **16**       | **?**           | **Drift**    | **?**            |    **280.42 ns** |   **2.744 ns** |   **0.425 ns** | **0.0129** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 16       | ?           | Drift    | ?            |    314.83 ns |   3.212 ns |   0.834 ns | 0.0100 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 16       | ?           | Drift    | ?            |    138.08 ns |   1.487 ns |   0.230 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 16       | ?           | Drift    | ?            |    154.71 ns |   0.921 ns |   0.143 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **1024**     | **?**           | **Drift**    | **?**            | **12,227.82 ns** |  **98.878 ns** |  **25.678 ns** |      **-** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 1024     | ?           | Drift    | ?            | 15,240.32 ns |  74.447 ns |  11.521 ns |      - |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 1024     | ?           | Drift    | ?            |  7,079.88 ns |  14.425 ns |   3.746 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 1024     | ?           | Drift    | ?            |  9,031.87 ns |  87.495 ns |  13.540 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **16**       | **?**           | **Jumpy**    | **?**            |    **286.22 ns** |  **12.431 ns** |   **3.228 ns** | **0.0129** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 16       | ?           | Jumpy    | ?            |    365.65 ns |   1.786 ns |   0.276 ns | 0.0100 |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 16       | ?           | Jumpy    | ?            |    157.84 ns |   0.585 ns |   0.152 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 16       | ?           | Jumpy    | ?            |    182.81 ns |   0.871 ns |   0.226 ns |      - |      - |         - |
| **Int32Benchmark**             | **Write**         | **?**        | **1024**     | **?**           | **Jumpy**    | **?**            | **12,071.16 ns** |  **60.662 ns** |   **9.387 ns** |      **-** |      **-** |     **216 B** |
| Int64Benchmark             | Write         | ?        | 1024     | ?           | Jumpy    | ?            | 19,779.79 ns | 222.939 ns |  57.896 ns |      - |      - |     168 B |
| Int32Benchmark             | Read          | ?        | 1024     | ?           | Jumpy    | ?            |  7,412.76 ns |  10.972 ns |   2.850 ns |      - |      - |         - |
| Int64Benchmark             | Read          | ?        | 1024     | ?           | Jumpy    | ?            |  9,226.98 ns |  16.826 ns |   4.370 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **16**       | **?**           | **?**        | **Milliseconds** |    **212.88 ns** |   **1.777 ns** |   **0.461 ns** | **0.0100** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 16       | ?           | ?        | Milliseconds |    341.82 ns |   2.948 ns |   0.766 ns | 0.0100 |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 16       | ?           | ?        | Milliseconds |    116.27 ns |   0.278 ns |   0.043 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 16       | ?           | ?        | Milliseconds |    247.26 ns |   0.714 ns |   0.186 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **1024**     | **?**           | **?**        | **Milliseconds** |  **9,090.59 ns** | **306.136 ns** |  **79.502 ns** |      **-** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 1024     | ?           | ?        | Milliseconds | 16,875.47 ns | 586.919 ns | 152.421 ns |      - |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 1024     | ?           | ?        | Milliseconds |  6,783.11 ns |  21.681 ns |   5.630 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 1024     | ?           | ?        | Milliseconds |  9,957.41 ns | 131.497 ns |  34.149 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **16**       | **?**           | **?**        | **Seconds**      |    **176.89 ns** |   **2.149 ns** |   **0.558 ns** | **0.0100** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 16       | ?           | ?        | Seconds      |    339.59 ns |   3.953 ns |   1.027 ns | 0.0100 |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 16       | ?           | ?        | Seconds      |    111.56 ns |   0.176 ns |   0.027 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 16       | ?           | ?        | Seconds      |    192.65 ns |   1.238 ns |   0.321 ns |      - |      - |         - |
| **DateTimeOrderedBenchmark**   | **Write**         | **?**        | **1024**     | **?**           | **?**        | **Seconds**      |  **8,474.54 ns** |  **21.129 ns** |   **3.270 ns** |      **-** |      **-** |     **168 B** |
| DateTimeUnorderedBenchmark | Write         | ?        | 1024     | ?           | ?        | Seconds      | 14,443.41 ns | 146.710 ns |  38.100 ns |      - |      - |     168 B |
| DateTimeOrderedBenchmark   | Read          | ?        | 1024     | ?           | ?        | Seconds      |  6,382.29 ns | 170.183 ns |  44.196 ns |      - |      - |         - |
| DateTimeUnorderedBenchmark | Read          | ?        | 1024     | ?           | ?        | Seconds      |  9,173.61 ns |  14.639 ns |   3.802 ns |      - |      - |         - |
