using System.Text.Json;
using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class BloomToolsTests
{
    private static BloomCall Call(string name, string args = "{}") => new(name, JsonDocument.Parse(args).RootElement.Clone());
    [Fact]
    public async Task Diagnostic_only_collects_when_executed_and_exports_a_positive_list_without_identifiers()
    {
        using var fixture = new OptimizationFixture(); var collected = false;
        const string secret = "NO-ENVIAR-IDENTIFICADOR";
        var snapshot = new SystemSnapshot
        {
            Machine = new() { Model = secret, SystemSku = secret, Manufacturer = secret, CpuName = secret, LogicalCores = 8, TotalMemoryGb = 16 },
            Startup = [new(secret, secret, secret)], Processes = [new(secret, 123456, 100)],
            Drivers = [new() { HardwareId = secret, DeviceName = secret, ProblemCode = 28 }],
            Drives = [new() { Name = secret, Label = secret, TotalGb = 100, FreeGb = 5 }],
            Networks = [new(secret, secret, 100)], Notes = [secret],
            Samples = [new() { CpuPercent = 25, MemoryPercent = 50 }]
        };
        var tools = new BloomWindowsTools(fixture.Service(), _ => { collected = true; return Task.FromResult(snapshot); });
        var action = await tools.PrepareAsync(Call("scan_pc"), default); Assert.False(collected);
        var result = await action.ExecuteAsync(default); Assert.True(collected);
        var json = result.GetRawText(); Assert.DoesNotContain(secret, json); Assert.DoesNotContain("123456", json);
        Assert.DoesNotContain(snapshot.Id.ToString(), json); Assert.DoesNotContain("CapturedAt", json);
        var summary = result.GetProperty("summary");
        Assert.Equal(8, summary.GetProperty("LogicalCores").GetInt32());
        Assert.Equal(1, summary.GetProperty("DriverProblems").GetInt32()); Assert.Equal(1, summary.GetProperty("LowSpaceDrives").GetInt32());
    }
    [Theory]
    [InlineData("scan_pc", "{\"command\":\"powershell\"}")]
    [InlineData("undo_last_change", "{\"id\":\"arbitrary\"}")]
    [InlineData("clean_temp_files", "{\"path\":\"C:\\\\Windows\"}")]
    [InlineData("run_powershell", "{}")]
    public async Task Adversarial_calls_cannot_expand_the_local_dispatcher(string name, string args)
    {
        using var fixture = new OptimizationFixture();
        var tools = new BloomWindowsTools(fixture.Service(), _ => throw new Exception("No debe recopilar"));
        await Assert.ThrowsAsync<BloomException>(() => tools.PrepareAsync(Call(name, args), default));
    }
    [Fact]
    public async Task Gaming_proposal_is_read_only_until_executed_and_every_change_can_be_undone()
    {
        using var fixture = new OptimizationFixture();
        var native = fixture.Service();
        var tools = new BloomWindowsTools(native, _ => Task.FromResult(new SystemSnapshot()));
        var action = await tools.PrepareAsync(Call("apply_optimizations", """{"categories":["gaming"],"preserve_audio":true}"""), default);
        Assert.Empty((await native.InspectAsync()).Changes);
        Assert.Contains("Modo Juego", action.Description);
        var result = await action.ExecuteAsync(default);
        Assert.True(result.GetProperty("success").GetBoolean());
        Assert.NotEmpty((await native.InspectAsync()).Changes);
        var undo = await tools.PrepareAsync(Call("undo_last_change"), default);
        Assert.Contains("Restaurar", undo.Description);
        Assert.True((await undo.ExecuteAsync(default)).GetProperty("success").GetBoolean());
    }
    [Theory]
    [InlineData("""{"categories":["registro-arbitrario"],"preserve_audio":true}""")]
    [InlineData("""{"categories":["servicios"],"preserve_audio":false}""")]
    [InlineData("""{"categories":["red"],"preserve_audio":true,"command":"bad"}""")]
    public async Task Unknown_categories_extra_arguments_and_audio_changes_are_rejected(string args)
    {
        using var fixture = new OptimizationFixture();
        var tools = new BloomWindowsTools(fixture.Service(), _ => Task.FromResult(new SystemSnapshot()));
        await Assert.ThrowsAsync<BloomException>(() => tools.PrepareAsync(Call("apply_optimizations", args), default));
        Assert.Empty((await fixture.Service().InspectAsync()).Changes);
    }
    [Fact]
    public async Task Undo_last_change_skips_an_already_restored_record_but_keeps_pending_ones()
    {
        using var fixture = new OptimizationFixture();
        var native = fixture.Service();
        await native.SetToggleAsync("game-mode", true);
        await native.SetToggleAsync("xbox-captures", false);
        var tools = new BloomWindowsTools(native, _ => Task.FromResult(new SystemSnapshot()));
        var first = await tools.PrepareAsync(Call("undo_last_change"), default);
        Assert.True((await first.ExecuteAsync(default)).GetProperty("success").GetBoolean());
        var second = await tools.PrepareAsync(Call("undo_last_change"), default);
        Assert.True((await second.ExecuteAsync(default)).GetProperty("success").GetBoolean());
    }
    [Fact]
    public async Task Cleanup_proposal_does_not_delete_and_only_approved_file_identity_is_cleaned()
    {
        using var fixture = new OptimizationFixture();
        var approved = Path.Combine(fixture.Temp, "approved.tmp"); await File.WriteAllTextAsync(approved, "old");
        File.SetLastWriteTimeUtc(approved, DateTime.UtcNow.AddDays(-2));
        var tools = new BloomWindowsTools(fixture.Service(), _ => Task.FromResult(new SystemSnapshot()));
        var action = await tools.PrepareAsync(Call("clean_temp_files"), default);
        Assert.True(File.Exists(approved)); Assert.Contains("recuperación local", action.Description);
        var late = Path.Combine(fixture.Temp, "late.tmp"); await File.WriteAllTextAsync(late, "late"); File.SetLastWriteTimeUtc(late, DateTime.UtcNow.AddDays(-2));
        await action.ExecuteAsync(default);
        Assert.False(File.Exists(approved)); Assert.True(File.Exists(late));
    }
    [Fact]
    public async Task Refreshing_temporaries_in_another_view_does_not_invalidate_an_approved_Bloom_batch()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var file = Path.Combine(fixture.Temp, "approved.tmp"); await File.WriteAllTextAsync(file, "old");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
        var tools = new BloomWindowsTools(native, _ => Task.FromResult(new SystemSnapshot()));
        var action = await tools.PrepareAsync(Call("clean_temp_files"), default);
        await native.PreviewTempAsync(); // Simula Actualizar ajustes en otra pestaña.
        var result = await action.ExecuteAsync(default);
        Assert.False(File.Exists(file)); Assert.Equal(1, result.GetProperty("Removed").GetInt32());
    }
    [Fact]
    public async Task Proxy_cleanup_keeps_its_approved_batch_and_reports_recoverable_bytes_without_claiming_freed_space()
    {
        using var fixture = new OptimizationFixture(); var native = fixture.Service();
        var proxy = new SystemActionOptimizationService(native, (_, _) => throw new Exception("La limpieza no requiere elevar"));
        var file = Path.Combine(fixture.Temp, "approved-proxy.tmp"); await File.WriteAllTextAsync(file, "old");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-2));
        var tools = new BloomWindowsTools(proxy, _ => Task.FromResult(new SystemSnapshot()));
        var action = await tools.PrepareAsync(Call("clean_temp_files"), default);
        await proxy.PreviewTempAsync();
        var result = await action.ExecuteAsync(default);
        Assert.False(File.Exists(file)); Assert.Equal(1, result.GetProperty("Removed").GetInt32());
        Assert.Equal(0, result.GetProperty("BytesFreed").GetInt64()); Assert.Equal(3, result.GetProperty("RecoverableBytes").GetInt64());
        Assert.Contains("recuperables", result.GetProperty("message").GetString());
        Assert.Single(await proxy.ListRecoveryAsync());
    }
}
