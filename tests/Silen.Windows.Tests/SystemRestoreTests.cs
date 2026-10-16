using Xunit;

namespace Silen.Windows.Tests;

public sealed class SystemRestoreTests
{
    [Fact]
    public async Task Boolean_success_with_an_existing_id_is_reported_as_reused()
    {
        var date = DateTimeOffset.UtcNow.AddHours(-2);
        var api = new RestoreApi { Points = [new(7, date, "Punto anterior")], Sequence = 7 };
        var result = await new NativeSystemRestoreService(api).PrepareAsync();
        Assert.Equal(RestorePointState.Reused, result.State);
        Assert.Equal(date, result.CreatedAt);
        Assert.Contains("reutilizó", result.Message);
        Assert.DoesNotContain("protegido", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, api.Ends);
    }

    [Fact]
    public async Task Created_requires_a_new_verified_identity_with_the_local_description()
    {
        var api = new RestoreApi { AddNew = true };
        var result = await new NativeSystemRestoreService(api).PrepareAsync();
        Assert.Equal(RestorePointState.Created, result.State);
        Assert.Equal(8, result.SequenceNumber);
        Assert.NotNull(result.CreatedAt);
        Assert.Equal(1, api.Ends);
    }

    [Theory]
    [InlineData(1058, RestorePointState.Disabled)]
    [InlineData(112, RestorePointState.Failed)]
    [InlineData(1460, RestorePointState.Failed)]
    public async Task Native_failures_are_literal_and_never_report_protection(int error, RestorePointState expected)
    {
        var api = new RestoreApi { BeginError = error };
        var result = await new NativeSystemRestoreService(api).PrepareAsync();
        Assert.Equal(expected, result.State);
        Assert.DoesNotContain("protegido", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, api.Ends);
    }

    [Fact]
    public async Task Unverified_success_or_failed_completion_is_not_created()
    {
        Assert.Equal(RestorePointState.Failed, (await new NativeSystemRestoreService(new RestoreApi()).PrepareAsync()).State);
        Assert.Equal(RestorePointState.Failed, (await new NativeSystemRestoreService(new RestoreApi { AddNew = true, EndError = 13 }).PrepareAsync()).State);
    }

    [Fact]
    public async Task Enumeration_failure_does_not_start_a_native_change()
    {
        var api = new RestoreApi { ReadFails = true };
        Assert.Equal(RestorePointState.Unavailable, (await new NativeSystemRestoreService(api).PrepareAsync()).State);
        Assert.Equal(0, api.Begins);
    }

    [Fact]
    public async Task Security_initialization_precedes_WMI_and_failure_does_not_begin_a_native_change()
    {
        var api = new RestoreApi { RequireSecurity = true, AddNew = true };
        Assert.Equal(RestorePointState.Created, (await new NativeSystemRestoreService(api).PrepareAsync()).State);
        Assert.Equal(1, api.Initializations);
        api = new RestoreApi { RequireSecurity = true, SecurityFails = true };
        Assert.Equal(RestorePointState.Unavailable, (await new NativeSystemRestoreService(api).PrepareAsync()).State);
        Assert.Equal(0, api.Begins);
    }

    [Fact]
    public async Task Cancel_before_begin_does_nothing_and_cancel_during_begin_still_ends_the_native_change()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var api = new RestoreApi();
        Assert.Equal(RestorePointState.Cancelled, (await new NativeSystemRestoreService(api).PrepareAsync(cancellation.Token)).State);
        Assert.Equal(0, api.Begins);
        using var during = new CancellationTokenSource();
        api = new RestoreApi { OnBegin = during.Cancel, AddNew = true };
        Assert.Equal(RestorePointState.Cancelled, (await new NativeSystemRestoreService(api).PrepareAsync(during.Token)).State);
        Assert.Equal(1, api.Ends);
    }

    [Fact]
    public void Restore_request_is_fixed_and_rejects_arbitrary_targets()
    {
        var request = new SystemActionRequest(SystemActionKind.CreateRestorePoint);
        Assert.Equal(request, SystemActionPolicy.Parse(SystemActionPolicy.Serialize(request)));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Serialize(request with { TargetId = "cmd.exe" }));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Serialize(request with { Enabled = true }));
    }

    [Fact]
    public void Com_callbacks_only_allow_the_documented_local_principals()
    {
        var acl = new System.Security.AccessControl.RawAcl(SystemRestoreComSecurity.BuildAccessList(), 0);
        var expected = new HashSet<string> { "S-1-5-32-544", "S-1-5-19", "S-1-5-20", "S-1-5-10", "S-1-5-18" };
        Assert.Equal(expected.Count, acl.Count);
        foreach (System.Security.AccessControl.CommonAce ace in acl)
        {
            Assert.True(expected.Remove(ace.SecurityIdentifier.Value));
            Assert.Equal(3, ace.AccessMask);
            Assert.Equal(System.Security.AccessControl.AceQualifier.AccessAllowed, ace.AceQualifier);
        }
        Assert.Empty(expected);
    }

    [Fact]
    public async Task Client_requires_a_consistent_typed_result_and_never_infers_creation_from_a_boolean()
    {
        var point = new RestorePointResult(RestorePointState.Created, 9, DateTimeOffset.UtcNow);
        var response = new SystemActionResponse(new(true, "Correcto"));
        var calls = 0;
        var client = new SystemRestoreClient((request, _) =>
        {
            calls++; Assert.Equal(new(SystemActionKind.CreateRestorePoint), request);
            return Task.FromResult(response);
        });
        Assert.False((await client.PrepareAsync()).CanContinueAutomatically);
        response = new(new(false, "No completado"), RestorePoint: point);
        Assert.False((await client.PrepareAsync()).CanContinueAutomatically);
        response = new(new(true, "Correcto"), RestorePoint: point);
        Assert.True((await client.PrepareAsync()).CanContinueAutomatically);
        response = new(new(false, "Se canceló UAC"), RestorePoint: new(RestorePointState.Cancelled));
        Assert.Equal(RestorePointState.Cancelled, (await client.PrepareAsync()).State);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.Equal(RestorePointState.Cancelled, (await client.PrepareAsync(cancelled.Token)).State);
        Assert.Equal(4, calls);
    }

    private sealed class RestoreApi : ISystemRestoreApi
    {
        public List<RestorePointIdentity> Points { get; set; } = [];
        public long Sequence { get; set; } = 8;
        public int BeginError { get; set; }
        public int EndError { get; set; }
        public bool AddNew { get; set; }
        public bool ReadFails { get; set; }
        public bool RequireSecurity { get; set; }
        public bool SecurityFails { get; set; }
        public int Initializations { get; private set; }
        public Action? OnBegin { get; set; }
        public int Begins { get; private set; }
        public int Ends { get; private set; }
        public IDisposable Initialize()
        {
            Initializations++;
            if (SecurityFails) throw new System.Runtime.InteropServices.COMException("Seguridad ya configurada", unchecked((int)0x80010119));
            return new EmptyLease();
        }
        private sealed class EmptyLease : IDisposable { public void Dispose() { } }
        public IReadOnlyList<RestorePointIdentity> ReadPoints() => ReadFails || (RequireSecurity && Initializations == 0) ? throw new InvalidOperationException("WMI no disponible o seguridad sin inicializar") : Points.ToArray();
        public RestoreNativeResult Begin(string description)
        {
            Begins++; OnBegin?.Invoke();
            if (BeginError == 0 && AddNew) Points.Add(new(Sequence, DateTimeOffset.UtcNow, description));
            return new(BeginError == 0, BeginError, Sequence);
        }
        public RestoreNativeResult End(long sequence) { Ends++; return new(EndError == 0, EndError, sequence); }
    }
}
