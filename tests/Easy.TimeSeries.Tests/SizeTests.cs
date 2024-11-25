// namespace Easy.TimeSeries.Tests;
//
// using Sgp.Extensions.Encoding.Gorilla;
// using Xunit.Abstractions;
//
// public class SizeTests
// {
//     private readonly ITestOutputHelper output;
//     private const int precision = 3;
//
//     public SizeTests(ITestOutputHelper output)
//     {
//         this.output = output;
//     }
//
//     [Fact]
//     public void LinearDouble()
//     {
//         var testData = Numbers.LinearDoubleSeq(1000, 50d, 0d, precision).ToArray();
//         var bb = new BitBuffer();
//         var wd = new DoubleValueWriter(bb);
//         foreach (var value in testData)
//         {
//             wd.AppendValue((float)value);
//         }
//
//         var size = bb.ToArray().Length;
//         output.WriteLine($"use case: linear double, precision: {precision}, written: {size}B");
//     }
//
//     [Fact]
//     public void RandomDouble()
//     {
//         var testData = Numbers.RandDoubleSeq(1000, 1_000_000d, -1_000_000d, precision).ToArray();
//         var bb = new BitBuffer();
//         var wd = new DoubleValueWriter(bb);
//         foreach (var value in testData)
//         {
//             wd.AppendValue((float)value);
//         }
//
//         var size = bb.ToArray().Length;
//         output.WriteLine($"use case: random double, precision: {precision}, written: {size}B");
//     }
//
//     [Fact]
//     public void LinearFloat()
//     {
//         var testData = Numbers.LinearFloatSeq(1000, 50d, 0d, precision).ToArray();
//         var bb = new BitBuffer();
//         var wd = new FloatValueWriter(bb);
//         foreach (var value in testData)
//         {
//             wd.AppendValue(value);
//         }
//
//         var size = bb.ToArray().Length;
//         output.WriteLine($"use case: linear float, precision: {precision}, written: {size}B");
//     }
// }
