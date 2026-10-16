using System.ComponentModel;
using System.Security;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class RegistryPermissionTests
{
    [Fact]
    public void Missing_key_checks_nearest_existing_parent_without_creating_keys()
    {
        var requested = new List<string>();
        Assert.True(NativeOptimizationPlatform.ProbeWritableKey(@"Software\Policies\Microsoft\Edge", path =>
        {
            requested.Add(path);
            return path == @"Software\Policies" ? true : null;
        }));
        Assert.Equal([@"Software\Policies\Microsoft\Edge", @"Software\Policies\Microsoft", @"Software\Policies"], requested);
    }

    [Fact]
    public void Existing_denied_key_does_not_fall_back_to_a_writable_parent()
    {
        var attempts = 0;
        Assert.False(NativeOptimizationPlatform.ProbeWritableKey(@"Software\Policies\Microsoft\Edge", _ => { attempts++; throw new UnauthorizedAccessException(); }));
        Assert.Equal(1, attempts);
    }

    [Fact]
    public void Security_and_native_access_denials_are_unavailable()
    {
        Assert.False(NativeOptimizationPlatform.ProbeWritableKey(@"Software\Policies", _ => throw new SecurityException()));
        Assert.False(NativeOptimizationPlatform.ProbeWritableKey(@"Software\Policies", _ => throw new Win32Exception(5)));
        Assert.False(NativeOptimizationPlatform.ProbeWritableKey(@"Software\Policies", _ => false));
    }

    [Fact]
    public void Missing_entire_chain_fails_closed()
    {
        Assert.False(NativeOptimizationPlatform.ProbeWritableKey(@"Software\Policies\Microsoft\Edge", _ => null));
    }
}
