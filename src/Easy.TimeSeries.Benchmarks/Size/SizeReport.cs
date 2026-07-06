namespace Easy.TimeSeries.Benchmarks.Size;

using System.Globalization;
using System.Text;

/// <summary>
/// Renders <see cref="SizeResult"/> rows as a GitHub-flavored markdown table.
/// </summary>
public static class SizeReport
{
    public static string ToMarkdown(IReadOnlyList<SizeResult> results)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine("| Dataset | Rows | CSV [B] | Parquet (snappy) [B] | ETS [B] | Parquet/CSV | ETS/CSV | ETS/Parquet |");
        sb.AppendLine("| ------- | ---: | ------: | -------------------: | ------: | ----------: | ------: | ----------: |");

        foreach (var r in results)
        {
            sb.Append(CultureInfo.InvariantCulture, $"| {r.Dataset} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {r.Rows:N0} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {r.CsvBytes:N0} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {r.ParquetBytes:N0} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {r.EtsBytes:N0} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {Ratio(r.ParquetBytes, r.CsvBytes)} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {Ratio(r.EtsBytes, r.CsvBytes)} ");
            sb.Append(CultureInfo.InvariantCulture, $"| {Ratio(r.EtsBytes, r.ParquetBytes)} |");
            sb.AppendLine();
        }

        return sb.ToString();

        static string Ratio(long numerator, long denominator)
        {
            return denominator == 0
                ? "n/a"
                : ((double)numerator / denominator).ToString("P1", CultureInfo.InvariantCulture);
        }
    }
}
