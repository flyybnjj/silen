using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class FortniteGameTests
{
    [Fact]
    public void App_data_file_validation_uses_the_verified_parent_when_Windows_virtualizes_its_path()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SilenTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "fixture.ini");
        try
        {
            File.WriteAllText(path, "owned fixture");
            using var file = FortniteSafeFiles.Open(path, false);
            Assert.Equal("owned fixture", Encoding.UTF8.GetString(file.ReadBytes()));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Manifest_requires_Fortnite_name_and_executable_before_offering_installation()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("other", "OtherGame");
        Assert.False((await fixture.Service().DetectAsync()).Installed);
        fixture.Manifest("fortnite", "Fortnite");
        var result = await fixture.Service().DetectAsync();
        Assert.True(result.Installed);
        Assert.Equal(fixture.Install, result.InstallDirectory);
        File.Delete(fixture.Executable);
        Assert.False((await fixture.Service().DetectAsync()).Installed);
    }

    [Fact]
    public async Task Competitive_profile_changes_only_known_existing_values_and_undo_restores_original_bytes()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var original = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            "; keep comment\r\n[ScalabilityGroups]\r\nsg.ShadowQuality = 3 ; detail\r\nsg.TextureQuality=2\r\n" +
            "[/Script/FortniteGame.FortGameUserSettings]\r\nbUseVSync=True\r\nbMotionBlur=True\r\nbShowFPS=False\r\n" +
            "ResolutionSizeX=1920\r\nFrameRateLimit=144.000000\r\nPreferredRHI=dx12\r\nUnknown=á\r\n")).ToArray();
        File.WriteAllBytes(fixture.Settings, original);
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        var applied = await service.ApplyCompetitiveSettingsAsync();
        Assert.True(applied.Success, applied.Message);
        Assert.True(applied.Changed);
        Assert.Equal("; keep comment\r\n[ScalabilityGroups]\r\nsg.ShadowQuality = 0 ; detail\r\nsg.TextureQuality=0\r\n" +
            "[/Script/FortniteGame.FortGameUserSettings]\r\nbUseVSync=False\r\nbMotionBlur=False\r\nbShowFPS=True\r\n" +
            "ResolutionSizeX=1920\r\nFrameRateLimit=144.000000\r\nPreferredRHI=dx12\r\nUnknown=á\r\n", File.ReadAllText(fixture.Settings));
        Assert.True(File.ReadAllBytes(fixture.Settings).Take(3).SequenceEqual(new byte[] { 0xef, 0xbb, 0xbf }));
        var change = Assert.Single(await service.ReadChangesAsync());
        Assert.StartsWith("fortnite:", change.Id);
        Assert.True(change.CanUndo);
        Assert.True((await service.UndoAsync(change.Id)).Success);
        Assert.Equal(original, File.ReadAllBytes(fixture.Settings));
        Assert.False(Assert.Single(await service.ReadChangesAsync()).CanUndo);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Incomplete_or_unverified_manifest_cannot_be_rescued_by_fallback(bool incomplete, bool validation)
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite", incomplete, validation);
        Assert.False((await fixture.Service([fixture.Install]).DetectAsync()).Installed);
        fixture.Manifest("fortnite", "Fortnite");
        Assert.True((await fixture.Service([fixture.Install]).DetectAsync()).Installed);
    }

    [Fact]
    public async Task Explicit_folder_requires_exact_executable_and_does_not_scan_descendants()
    {
        using var fixture = new GameFixture();
        var service = fixture.Service();
        Assert.False((await service.DetectAsync(fixture.Root)).Installed);
        Assert.True((await service.DetectAsync(fixture.Install)).Installed);
    }

    [Fact]
    public async Task Apply_requires_a_detection_and_an_existing_config_file()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        File.Delete(fixture.Settings);
        Assert.True((await service.DetectAsync()).Installed);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        Assert.False(File.Exists(fixture.Settings));
    }

    [Fact]
    public async Task Config_changed_after_detection_is_preserved_until_reexamined()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        const string changed = "[ScalabilityGroups]\nsg.ShadowQuality=4\nAccount=keep\n";
        File.WriteAllText(fixture.Settings, changed);
        var result = await service.ApplyCompetitiveSettingsAsync();
        Assert.False(result.Success);
        Assert.Equal(changed, File.ReadAllText(fixture.Settings));
        Assert.Empty(await service.ReadChangesAsync());
        await service.DetectAsync();
        Assert.True((await service.ApplyCompetitiveSettingsAsync()).Changed);
    }

    [Fact]
    public async Task A_running_game_is_never_modified_or_terminated()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        fixture.Running = true;
        var service = fixture.Service();
        var detection = await service.DetectAsync();
        Assert.True(detection.Installed);
        Assert.True(detection.IsRunning);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(fixture.Settings));
        Assert.True(fixture.Running);
    }

    [Theory]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("latin1")]
    public async Task Existing_encoding_and_line_endings_are_retained(string encoding)
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var selected = encoding switch { "utf16-le" => Encoding.Unicode, "utf16-be" => Encoding.BigEndianUnicode, _ => Encoding.Latin1 };
        var original = selected.GetPreamble().Concat(selected.GetBytes("[ScalabilityGroups]\r\nsg.ShadowQuality=3\r\nUnknown=á\r\n")).ToArray();
        var expected = selected.GetPreamble().Concat(selected.GetBytes("[ScalabilityGroups]\r\nsg.ShadowQuality=0\r\nUnknown=á\r\n")).ToArray();
        File.WriteAllBytes(fixture.Settings, original);
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        var result = await service.ApplyCompetitiveSettingsAsync();
        Assert.True(result.Changed, result.Message);
        Assert.Equal(expected, File.ReadAllBytes(fixture.Settings));
        Assert.True((await service.UndoAsync(Assert.Single(await service.ReadChangesAsync()).Id)).Success);
        Assert.Equal(original, File.ReadAllBytes(fixture.Settings));
    }

    [Fact]
    public async Task Unknown_sections_are_preserved_and_no_known_keys_is_a_failure()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        const string unknown = "[Other]\nsg.ShadowQuality=3\nbUseVSync=True\n";
        File.WriteAllText(fixture.Settings, unknown);
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        Assert.Equal(unknown, File.ReadAllText(fixture.Settings));
        Assert.Empty(await service.ReadChangesAsync());
    }

    [Fact]
    public async Task Readonly_and_locked_config_is_not_modified()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        File.SetAttributes(fixture.Settings, FileAttributes.ReadOnly);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        File.SetAttributes(fixture.Settings, FileAttributes.Normal);
        using var held = new FileStream(fixture.Settings, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
    }

    [Fact]
    public async Task Undo_never_overwrites_configuration_changed_by_the_game_or_user()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        Assert.True((await service.ApplyCompetitiveSettingsAsync()).Changed);
        var id = Assert.Single(await service.ReadChangesAsync()).Id;
        File.WriteAllText(fixture.Settings, "[ScalabilityGroups]\nsg.ShadowQuality=2\n");
        Assert.False((await service.UndoAsync(id)).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=2\n", File.ReadAllText(fixture.Settings));
        Assert.False(Assert.Single(await service.ReadChangesAsync()).CanUndo);
    }

    [Fact]
    public async Task Already_configured_values_are_reported_without_creating_a_backup_change()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        File.WriteAllText(fixture.Settings, "[ScalabilityGroups]\nsg.ShadowQuality=0\n");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        var result = await service.ApplyCompetitiveSettingsAsync();
        Assert.True(result.Success);
        Assert.False(result.Changed);
        Assert.Empty(await service.ReadChangesAsync());
    }

    [Fact]
    public async Task Removed_installation_after_detection_rejects_write()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        File.Delete(fixture.Executable);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(fixture.Settings));
    }

    [Fact]
    public async Task Cancellation_before_apply_leaves_config_and_changes_intact()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyCompetitiveSettingsAsync(cancelled.Token));
        Assert.Empty(await service.ReadChangesAsync());
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(fixture.Settings));
    }

    [Fact]
    public async Task Detection_does_not_offer_readonly_configuration_as_editable()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        File.SetAttributes(fixture.Settings, FileAttributes.ReadOnly);
        var result = await fixture.Service().DetectAsync();
        Assert.True(result.Installed);
        Assert.Null(result.SettingsPath);
    }

    [Fact]
    public async Task An_installation_reached_through_a_junction_is_not_accepted()
    {
        using var fixture = new GameFixture();
        var link = Path.Combine(fixture.Root, "LinkedGame");
        CleanupTestLinks.CreateJunction(link, fixture.Install);
        try { Assert.False((await fixture.Service().DetectAsync(link)).Installed); }
        finally { Directory.Delete(link); }
        Assert.True((await fixture.Service().DetectAsync(fixture.Install)).Installed);
    }

    [Fact]
    public async Task A_hardlinked_configuration_is_rejected_without_changing_either_name()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var outside = Path.Combine(fixture.Root, "outside.ini");
        CleanupTestLinks.CreateHardLink(outside, fixture.Settings);
        var service = fixture.Service();
        var result = await service.DetectAsync();
        Assert.True(result.Installed);
        Assert.Null(result.SettingsPath);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(outside));
    }

    [Fact]
    public async Task A_journal_junction_is_rejected_before_backing_up_or_editing()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var outside = Path.Combine(fixture.Root, "outside-journal");
        Directory.CreateDirectory(outside);
        CleanupTestLinks.CreateJunction(fixture.Journal, outside);
        try
        {
            var service = fixture.Service();
            Assert.True((await service.DetectAsync()).Installed);
            Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside));
            Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(fixture.Settings));
        }
        finally { Directory.Delete(fixture.Journal); }
    }

    [Fact]
    public async Task Pending_configuration_write_is_recovered_by_its_hash_after_restart()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        Assert.True((await service.ApplyCompetitiveSettingsAsync()).Changed);
        var path = Path.Combine(fixture.Journal, "fortnite.json");
        var entries = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        entries[0]!["State"] = "Pending";
        File.WriteAllText(path, entries.ToJsonString());
        var recovered = Assert.Single(await fixture.Service().ReadChangesAsync());
        Assert.True(recovered.CanUndo);
        Assert.True((await fixture.Service().UndoAsync(recovered.Id)).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(fixture.Settings));
    }

    [Fact]
    public async Task A_corrupt_backup_is_never_restored()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        Assert.True((await service.ApplyCompetitiveSettingsAsync()).Changed);
        var id = Assert.Single(await service.ReadChangesAsync()).Id;
        File.WriteAllText(Path.Combine(fixture.Journal, id[9..] + ".original.bin"), "bad backup");
        Assert.False((await service.UndoAsync(id)).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=0\n", File.ReadAllText(fixture.Settings));
    }

    [Theory]
    [InlineData("Pending")]
    [InlineData("UndoPending")]
    public async Task An_interrupted_prefix_write_on_the_same_file_can_restore_its_durable_backup(string state)
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var original = Encoding.UTF8.GetBytes("[ScalabilityGroups]\nsg.ShadowQuality=3\nsg.TextureQuality=3\n");
        File.WriteAllBytes(fixture.Settings, original);
        var service = fixture.Service();
        await service.DetectAsync();
        Assert.True((await service.ApplyCompetitiveSettingsAsync()).Success);
        var intended = File.ReadAllBytes(fixture.Settings);
        var difference = Enumerable.Range(0, original.Length).First(i => original[i] != intended[i]);
        var incomplete = state == "Pending"
            ? intended[..(difference + 1)].Concat(original[(difference + 1)..]).ToArray()
            : original[..(difference + 1)].Concat(intended[(difference + 1)..]).ToArray();
        File.WriteAllBytes(fixture.Settings, incomplete);
        var path = Path.Combine(fixture.Journal, "fortnite.json");
        var entries = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        entries[0]!["State"] = state;
        File.WriteAllText(path, entries.ToJsonString());
        var recovery = fixture.Service();
        var change = Assert.Single(await recovery.ReadChangesAsync());
        Assert.True(change.CanUndo);
        Assert.True((await recovery.UndoAsync(change.Id)).Success);
        Assert.Equal(original, File.ReadAllBytes(fixture.Settings));
    }

    [Fact]
    public async Task A_journal_cannot_redirect_undo_to_a_different_file()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service();
        Assert.True((await service.DetectAsync()).Installed);
        Assert.True((await service.ApplyCompetitiveSettingsAsync()).Changed);
        var id = Assert.Single(await service.ReadChangesAsync()).Id;
        var outside = Path.Combine(fixture.Root, "outside.ini");
        File.WriteAllText(outside, "keep");
        var path = Path.Combine(fixture.Journal, "fortnite.json");
        var entries = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        entries[0]!["SettingsPath"] = outside;
        File.WriteAllText(path, entries.ToJsonString());
        Assert.False((await service.UndoAsync(id)).Success);
        Assert.Equal("keep", File.ReadAllText(outside));
    }

    [Fact]
    public async Task Fallback_is_bounded_and_never_recursively_searches_a_candidate()
    {
        using var fixture = new GameFixture();
        var absent = Enumerable.Range(0, 32).Select(x => Path.Combine(fixture.Root, "missing" + x));
        Assert.False((await fixture.Service(absent.Append(fixture.Install)).DetectAsync()).Installed);
        Assert.False((await fixture.Service([fixture.Root]).DetectAsync()).Installed);
        Assert.True((await fixture.Service([fixture.Install]).DetectAsync()).Installed);
    }

    [Fact]
    public async Task A_game_started_after_the_durable_backup_prevents_the_write()
    {
        using var fixture = new GameFixture();
        fixture.Manifest("fortnite", "Fortnite");
        var service = fixture.Service(running: () => File.Exists(Path.Combine(fixture.Journal, "fortnite.json")));
        Assert.True((await service.DetectAsync()).Installed);
        Assert.False((await service.ApplyCompetitiveSettingsAsync()).Success);
        Assert.Equal("[ScalabilityGroups]\nsg.ShadowQuality=3\n", File.ReadAllText(fixture.Settings));
        Assert.False(Assert.Single(await service.ReadChangesAsync()).CanUndo);
    }

    internal sealed class GameFixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "SilenFortniteTests", Guid.NewGuid().ToString("N"));
        internal string Install => Path.Combine(Root, "Fortnite");
        internal string Executable => Path.Combine(Install, "FortniteGame", "Binaries", "Win64", "FortniteClient-Win64-Shipping.exe");
        internal string Settings => Path.Combine(Root, "Local", "FortniteGame", "Saved", "Config", "WindowsClient", "GameUserSettings.ini");
        internal string Manifests => Path.Combine(Root, "Epic", "Manifests");
        internal string Journal => Path.Combine(Root, "Journal");
        internal bool Running { get; set; }
        internal GameFixture()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Executable)!);
            Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
            Directory.CreateDirectory(Manifests);
            File.WriteAllBytes(Executable, [1, 2, 3]);
            File.WriteAllText(Settings, "[ScalabilityGroups]\nsg.ShadowQuality=3\n");
        }
        internal FortniteGameService Service(IEnumerable<string>? fallback = null, Func<bool>? running = null) =>
            new(Manifests, Settings, Journal, fallback ?? [], running ?? (() => Running));
        internal void Manifest(string id, string appName, bool incomplete = false, bool needsValidation = false) =>
            File.WriteAllText(Path.Combine(Manifests, id + ".item"), JsonSerializer.Serialize(new
            {
                AppName = appName, InstallLocation = Install, bIsIncompleteInstall = incomplete, bNeedsValidation = needsValidation
            }));
        public void Dispose()
        {
            if (!Directory.Exists(Root)) return;
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
    }
}
