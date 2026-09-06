namespace Easy.TimeSeries.Tests;

using Easy.TimeSeries.Storage;
using System.IO;

/// <summary>
/// Guards the on-disk format against silent drift. Without these, the format is only ever round-tripped in-process
/// within a single build, so a change to any encoder would pass the whole suite while orphaning every file users
/// had already written.
/// </summary>
/// <remarks>
/// Two assertions per fixture, and they fail for different reasons:
///
/// - <see cref="Writing_the_fixture_input_reproduces_the_committed_bytes"/> catches encoder drift, and doubles as a
///   size canary - the byte count is part of the committed file.
/// - <see cref="Reading_the_committed_file_yields_the_expected_values"/> is the compatibility contract that matters
///   to users: files written by an older version must still decode correctly.
///
/// If an encoder is improved deliberately, regenerate with <c>./scripts/regen-golden.sh</c> and review the diff -
/// a changed fixture is a format change and must be treated as one.
/// </remarks>
public class GoldenFileTests
{
    public static TheoryData<string> FixtureNames
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var fixture in GoldenFixtures.All)
            {
                data.Add(fixture.Name);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public async Task Writing_the_fixture_input_reproduces_the_committed_bytes(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = Find(name);
        var path = GoldenPath(name);

        Assert.True(
            File.Exists(path),
            $"golden file '{path}' is missing; regenerate with ./scripts/regen-golden.sh");

        var expected = await File.ReadAllBytesAsync(path, ct);
        var actual = await ProduceAsync(fixture, ct);

        Assert.True(
            expected.AsSpan().SequenceEqual(actual),
            $"'{name}' no longer serializes to the committed bytes ({expected.Length} B committed, " +
            $"{actual.Length} B produced). This is an on-disk format change: confirm it is intended, then " +
            "regenerate with ./scripts/regen-golden.sh.");
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public async Task Reading_the_committed_file_yields_the_expected_values(string name)
    {
        var ct = TestContext.Current.CancellationToken;
        var fixture = Find(name);
        var path = GoldenPath(name);

        Assert.True(
            File.Exists(path),
            $"golden file '{path}' is missing; regenerate with ./scripts/regen-golden.sh");

        using var storage = new FileStorage(path);
        var materializer = new CapturingMaterializer();
        await Reader.ReadFromAsync(storage, materializer, null, ct);

        Assert.Equal(fixture.Expected.Length, materializer.Columns.Count);
        for (var column = 0; column < fixture.Expected.Length; column++)
        {
            Assert.True(
                materializer.Columns.TryGetValue(column, out var decoded),
                $"'{name}' column {column} was not read back");
            Assert.Equal(fixture.Expected[column], decoded!);
        }
    }

    [Fact]
    public void Every_fixture_declares_a_distinct_name()
    {
        var names = GoldenFixtures.All.Select(x => x.Name).ToArray();
        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static GoldenFixture Find(string name)
        => GoldenFixtures.All.Single(x => string.Equals(x.Name, name, StringComparison.Ordinal));

    /// <summary>Serializes a fixture through the same path the library uses, into memory.</summary>
    internal static async Task<byte[]> ProduceAsync(GoldenFixture fixture, CancellationToken ct)
    {
        using var storage = new InMemoryStorage();
        using (var writer = fixture.Build())
        {
            await writer.WriteToAsync(storage, ct);
        }

        return storage.WrittenSpan.ToArray();
    }

    internal static string GoldenPath(string name)
        => Path.Combine(GoldenDirectory(), $"{name}.ets");

    internal static string GoldenDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tests", "data", "golden", GoldenFixtures.FormatVersion);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new DirectoryNotFoundException(
            $"could not locate tests/data/golden/{GoldenFixtures.FormatVersion} above {AppContext.BaseDirectory}");
    }
}
