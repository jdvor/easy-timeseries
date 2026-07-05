namespace Easy.TimeSeries.TestData;

using System.Globalization;

public static class Data
{
    private const string DataDirEnvVarName = "TS_TESTS_DATA";

    public static readonly Lazy<DirectoryInfo> DataDir = new(FindDataDir);

    private static string FindSolutionDirPath()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (IsSolutionDir(dir))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return string.Empty;

        static bool IsSolutionDir(string dir)
        {
            return Directory.EnumerateFiles(dir, "*.sln", SearchOption.TopDirectoryOnly).Any()
                   || Directory.EnumerateFiles(dir, "*.slnx", SearchOption.TopDirectoryOnly).Any();
        }
    }

    private static DirectoryInfo FindDataDir()
    {
        var envSet = Environment.GetEnvironmentVariable(DataDirEnvVarName);
        if (!string.IsNullOrEmpty(envSet) && Directory.Exists(envSet))
        {
            return new DirectoryInfo(envSet);
        }


        var solutionDir = FindSolutionDirPath();
        var path = Path.GetFullPath(Path.Combine(solutionDir, "tests", "data"));
        var dir = new DirectoryInfo(path);
        if (!dir.Exists)
        {
            throw new InvalidOperationException("Where is solution/tests/data directory?");
        }

        return dir;
    }

    public static List<PowerPlant> GetPowerPlants()
    {
        var result = new List<PowerPlant>(35_000);
        var path = Path.Combine(DataDir.Value.FullName, "power_plants.csv");
        using var reader = new StreamReader(path);
        var first = true;
        while (reader.ReadLine() is { } line)
        {
            if (first)
            {
                // headers
                first = false;
                continue;
            }

            var parts = line.Split([','], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5)
            {
                continue;
            }

            var capacity = float.Parse(parts[1], CultureInfo.InvariantCulture);
            var latitude = float.Parse(parts[2], CultureInfo.InvariantCulture);
            var longitude = float.Parse(parts[3], CultureInfo.InvariantCulture);
            result.Add(new PowerPlant(parts[0].Trim(), capacity, latitude, longitude, parts[4].Trim()));
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
