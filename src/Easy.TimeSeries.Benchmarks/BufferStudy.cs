// namespace Easy.TimeSeries.Benchmarks;
//
// using BenchmarkDotNet.Attributes;
//
// [MemoryDiagnoser]
// public class BufferStudy
// {
//     // [IterationSetup]
//     // public void Setup()
//     // {
//     //     Array.Fill(SpanOnlyBuffer, (byte)0);
//     //     BufferProvider.Clear();
//     // }
//
//     [Benchmark]
//     public int SpanOnly()
//     {
//         var buffer = SpanOnlyBuffer.AsSpan();
//         var writer = new DateTimeWriter(buffer, TimePrecision.Milliseconds);
//         foreach (var time in Data)
//         {
//             writer.Write(time);
//         }
//
//         writer.Close();
//
//         return writer.WrittenBytes;
//     }
//
//     [Benchmark]
//     public int BufferWriter()
//     {
//         var bw = new BitWriter2(BufferProvider, useTotalBitsHeader: true);
//         var writer = new DateTimeWriter2(bw, TimePrecision.Milliseconds);
//         foreach (var time in Data)
//         {
//             writer.Write(time);
//         }
//
//         bw.Flush();
//
//         return bw.UsedBytes;
//     }
//
//     private const int PreClaimedSize = Constants.DefaultInitialBufferSize;
//
//     private static readonly byte[] SpanOnlyBuffer = new byte[PreClaimedSize];
//
//     private static readonly PooledArrayBufferWriter BufferProvider = CreateBufferProvider();
//
//     private static PooledArrayBufferWriter CreateBufferProvider()
//     {
//         var bp = new PooledArrayBufferWriter(PreClaimedSize, Constants.DefaultBufferGrowFactor, Constants.MaxAllowedBufferSize);
//         _ = bp.GetSpan(Constants.DefaultInitialBufferSize);
//         return bp;
//     }
//
//     private static readonly string[] DataStrs =
//     [
//         "2023-11-23T20:51:17.887Z", "2023-11-23T20:51:20.887Z", "2023-11-23T20:51:23.887Z", "2023-11-23T20:51:24.537Z",
//         "2023-11-23T20:51:25.331Z", "2023-11-23T20:51:25.332Z", "2023-11-23T21:05:25.332Z", "2023-11-23T21:08:25.332Z",
//         "2023-11-23T21:11:25.332Z", "2023-11-23T21:12:25.332Z", "2023-11-23T21:14:25.332Z", "2023-11-23T21:18:25.332Z",
//         "2023-11-23T21:26:25.332Z", "2023-11-23T21:38:25.332Z", "2023-11-23T22:02:25.332Z", "2023-11-23T22:34:25.332Z"
//     ];
//
//     private static readonly DateTime[] Data = DataStrs.Select(ParseDate).ToArray();
//
//     private static DateTime ParseDate(string dateStr)
//     {
//         var dt = DateTime.Parse(dateStr);
//         return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
//     }
// }
