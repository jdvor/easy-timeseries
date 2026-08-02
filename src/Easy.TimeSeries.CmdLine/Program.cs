using ConsoleAppFramework;
using Easy.TimeSeries.CmdLine;

var app = ConsoleApp.Create();
app.ConfigureDefaultConfiguration(builder => Setup.ConfigureConfiguration(builder, args));
app.ConfigureLogging(Setup.ConfigureLogging);

app.Add("ref-files-ts", ReferenceFiles.GenerateTimeSeriesAsync);
app.Add("ref-files-parquet", ReferenceFiles.GenerateParquetAsync);
app.Add("convert-to-parquet", Conversions.ConvertToParquetAsync);

await app.RunAsync(args);
