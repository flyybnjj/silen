using System.Text;
using Xunit;
namespace Silen.Windows.Tests;
public sealed class SystemActionPolicyTests
{
    [Theory]
    [InlineData("{\"Action\":\"SetToggle\",\"TargetId\":\"delivery-optimization\",\"Enabled\":true,\"RegistryPath\":\"HKLM\\\\evil\"}")]
    [InlineData("{\"Action\":\"SetToggle\",\"TargetId\":\"game-mode\",\"Enabled\":true}")]
    [InlineData("{\"Action\":\"Undo\",\"TargetId\":\"../../evil\"}")]
    [InlineData("{\"Action\":2,\"TargetId\":\"11111111111111111111111111111111\"}")]
    [InlineData("{\"Action\":\"ResumeWindowsUpdate\",\"Enabled\":true}")]
    [InlineData("{\"Action\":\"SetToggle\",\"Action\":\"ResumeWindowsUpdate\"}")]
    public void Malformed_or_generic_operations_are_rejected(string json) =>
        Assert.ThrowsAny<ArgumentException>(() => SystemActionPolicy.Parse(Encoding.UTF8.GetBytes(json)));
    [Fact] public void Request_is_bounded() => Assert.ThrowsAny<ArgumentException>(() => SystemActionPolicy.Parse(new byte[2049]));
    [Theory]
    [InlineData("S-1-5-21-1", "S-1-5-21-2", 1, 1, false)]
    [InlineData("S-1-5-21-1", "S-1-5-21-1", 1, 2, false)]
    [InlineData("S-1-5-21-1", "S-1-5-21-1", 1, 1, true)]
    public void Wrong_user_session_or_remote_peer_is_denied(string expected, string actual, int session, int peerSession, bool remote) =>
        Assert.Throws<UnauthorizedAccessException>(() => SystemActionPolicy.ValidatePeer(expected, actual, session, peerSession, remote));
    [Fact] public void Driver_download_only_contains_bounded_typed_identities()
    {
        var request = new SystemActionRequest(SystemActionKind.DownloadDrivers, Updates: [new(Guid.NewGuid(), 1)]);
        var parsed = SystemActionPolicy.Parse(SystemActionPolicy.Serialize(request));
        Assert.Equal(request.Updates![0], Assert.Single(parsed.Updates!));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Validate(request with { Updates = [] }));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Validate(request with { Updates = Enumerable.Range(0,21).Select(_ => new SystemDriverUpdateIdentity(Guid.NewGuid(),1)).ToArray() }));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Validate(request with { Updates = [new(Guid.Empty,1)] }));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Validate(request with { Updates = [new(Guid.NewGuid(),0)] }));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Validate(request with { Updates = [request.Updates![0], request.Updates[0]] }));
        Assert.Throws<ArgumentException>(() => SystemActionPolicy.Validate(request with { Action = SystemActionKind.ResumeWindowsUpdate }));
    }
    [Fact] public void Known_operation_round_trips()
    {
        var request = new SystemActionRequest(SystemActionKind.SetToggle, "service-fax", true);
        Assert.Equal(request, SystemActionPolicy.Parse(SystemActionPolicy.Serialize(request)));
    }
}
