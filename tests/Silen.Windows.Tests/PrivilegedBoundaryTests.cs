using Xunit;

namespace Silen.Windows.Tests;

public sealed class PrivilegedBoundaryTests
{
    [Fact]
    public async Task User_journal_cannot_authorize_machine_undo_even_with_valid_catalog_targets()
    {
        using var fixture = new OptimizationFixture();
        Assert.True((await fixture.Service().SetToggleAsync("delivery-optimization", true)).Success);
        var journal = new OptimizationJournal(fixture.Journal);
        Assert.Empty(await journal.ReadAsync(CancellationToken.None));
    }

    [Fact]
    public async Task User_journal_cannot_authorize_service_undo()
    {
        using var fixture = new OptimizationFixture();
        Directory.CreateDirectory(fixture.Journal);
        var journal = """
        [{"Id":"11111111111111111111111111111111","ToolId":"service-diagtrack","Title":"Falso","State":"Completed","Values":[{"Kind":"ServiceStart","Key":"DiagTrack","Name":"","Previous":{"Exists":true,"Kind":"DWord","Number":2},"Intended":{"Exists":true,"Kind":"DWord","Number":4}}]}]
        """;
        await File.WriteAllTextAsync(Path.Combine(fixture.Journal, "optimizations.json"), journal);
        Assert.Empty(await new OptimizationJournal(fixture.Journal).ReadAsync(CancellationToken.None));
    }
    [Fact]
    public async Task Mixed_legacy_history_preserves_exact_source_and_keeps_user_undo()
    {
        using var fixture = new OptimizationFixture();
        var old = fixture.Service();
        Assert.True((await old.SetToggleAsync("game-mode", true)).Success);
        Assert.True((await old.SetToggleAsync("delivery-optimization", true)).Success);
        var path = Path.Combine(fixture.Journal, "optimizations.json");
        var original = await File.ReadAllTextAsync(path);
        var scoped = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp, () => DateTimeOffset.UtcNow, authority: OptimizationAuthority.User);
        var overview = await scoped.InspectAsync();
        var user = Assert.Single(overview.Changes, change => change.CanUndo);
        Assert.Contains(overview.Changes, change => change.Id.StartsWith("legacy-system:") && !change.CanUndo);
        Assert.True((await scoped.UndoAsync(user.Id)).Success);
        Assert.False(fixture.Platform.ReadValue(OptimizationTargets.GameBar, "AutoGameModeEnabled").Exists);
        Assert.Equal(original, await File.ReadAllTextAsync(Path.Combine(fixture.Journal, "optimizations.legacy-untrusted.json")));
        Assert.False((await scoped.SetToggleAsync("delivery-optimization", true)).Changed);
    }

    [Fact]
    public async Task User_engine_rejects_machine_write_before_journaling_even_when_platform_allows_it()
    {
        using var fixture = new OptimizationFixture();
        var scoped = new NativeOptimizationService(fixture.Platform, fixture.Journal, fixture.Temp, () => DateTimeOffset.UtcNow, authority: OptimizationAuthority.User);
        var result = await scoped.SetToggleAsync("delivery-optimization", true);
        Assert.False(result.Success);
        Assert.False(result.Changed);
        Assert.False(fixture.Platform.ReadValue(@"HKLM\SOFTWARE\Policies\Microsoft\Windows\DeliveryOptimization", "DODownloadMode").Exists);
        Assert.Empty((await scoped.InspectAsync()).Changes);
    }

    [Fact]
    public void Administrator_journal_rejects_user_owned_or_user_writable_acl()
    {
        var admins = new System.Security.Principal.SecurityIdentifier("S-1-5-32-544");
        var user = new System.Security.Principal.SecurityIdentifier("S-1-5-21-1-2-3-1001");
        var acl = new System.Security.AccessControl.DirectorySecurity();
        acl.SetOwner(user);
        Assert.Throws<UnauthorizedAccessException>(() => ProtectedOptimizationDirectory.ValidateAcl(acl));
        acl.SetOwner(admins);
        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.Modify, System.Security.AccessControl.AccessControlType.Allow));
        Assert.Throws<UnauthorizedAccessException>(() => ProtectedOptimizationDirectory.ValidateAcl(acl));
        acl.RemoveAccessRuleAll(new System.Security.AccessControl.FileSystemAccessRule(user, System.Security.AccessControl.FileSystemRights.Modify, System.Security.AccessControl.AccessControlType.Allow));
        ProtectedOptimizationDirectory.ValidateAcl(acl);
    }
    [Fact]
    public void Administrator_directory_rejects_nonprivileged_delete_child_rights()
    {
        var acl = new System.Security.AccessControl.DirectorySecurity();
        acl.SetOwner(new System.Security.Principal.SecurityIdentifier("S-1-5-32-544"));
        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
            new System.Security.Principal.SecurityIdentifier("S-1-5-21-1-2-3-1001"),
            System.Security.AccessControl.FileSystemRights.DeleteSubdirectoriesAndFiles,
            System.Security.AccessControl.AccessControlType.Allow));
        Assert.Throws<UnauthorizedAccessException>(() => ProtectedOptimizationDirectory.ValidateAcl(acl));
    }
}
