using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class ReportBoundsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SilenTests", Guid.NewGuid().ToString("N"));
    private const int Limit = 16 * 1024 * 1024;

    [Fact]
    public async Task Oversized_report_is_rejected_before_parsing_and_valid_history_remains_available()
    {
        var store = new ReportStore(directory);
        await store.SaveAsync(new() { Machine = new() { CpuName = "Válido" } });
        var path = Path.Combine(directory, "oversized.json");
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.SetLength(Limit + 1L);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.LoadAsync(path));
        Assert.Contains("16 MiB", error.Message);
        Assert.Equal("Válido", Assert.Single(await store.ListAsync()).CpuName);
        Assert.Equal(Limit + 1L, new FileInfo(path).Length);
    }

    [Fact]
    public async Task Oversized_action_log_is_preserved_instead_of_read_or_rewritten()
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "actions.json");
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write)) file.SetLength(Limit + 1L);
        var store = new ReportStore(directory);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => store.RecordActionAsync(new(DateTimeOffset.Now, "one", "Guía", "Abierta")));
        Assert.Contains("16 MiB", error.Message);
        Assert.Equal(Limit + 1L, new FileInfo(path).Length);
    }

    [Fact]
    public async Task Oversized_UTF8_output_cannot_replace_an_existing_report()
    {
        var store = new ReportStore(directory);
        var original = new SystemSnapshot();
        var path = await store.SaveAsync(original);
        var bytes = await File.ReadAllBytesAsync(path);
        var tooLarge = original with { Notes = [new string('á', Limit / 2)] };
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(tooLarge));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
