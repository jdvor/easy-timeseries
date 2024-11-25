namespace Easy.TimeSeries.Tests;

using System.Globalization;

internal static class Data
{
    private const string DataDirEnvVarName = "TS_TESTS_DATA";

    public static readonly Lazy<DirectoryInfo> DataDir = new(FindDataDir);

    private static DirectoryInfo FindDataDir()
    {
        var envSet = Environment.GetEnvironmentVariable(DataDirEnvVarName);
        if (!string.IsNullOrEmpty(envSet) && Directory.Exists(envSet))
        {
            return new DirectoryInfo(envSet);
        }

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var path = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "data"));
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            throw new Exception("Where is solution/tests/data directory?");
        }

        return dir;
    }

    public static List<PowerPlant> GetPowerPlants()
    {
        var result = new List<PowerPlant>(35_000);
        var path = Path.Combine(DataDir.Value.FullName, "power_plants.csv");
        using var reader = new StreamReader(path);
        string? line;
        var first = true;
        while ((line = reader.ReadLine()) is not null)
        {
            if (first)
            {
                // headers
                first = false;
                continue;
            }

            var parts = line.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                continue;
            }

            var capacity = float.Parse(parts[1], CultureInfo.InvariantCulture);
            var latitude = float.Parse(parts[2], CultureInfo.InvariantCulture);
            var longitude = float.Parse(parts[3], CultureInfo.InvariantCulture);
            result.Add(new PowerPlant(parts[0], capacity, latitude, longitude, parts[4]));
        }

        reader.Close();

        return result;
    }

    public static long GetPowerPlantsFileSize()
    {
        var path = Path.Combine(DataDir.Value.FullName, "power_plants.csv");
        return new FileInfo(path).Length;
    }
}
