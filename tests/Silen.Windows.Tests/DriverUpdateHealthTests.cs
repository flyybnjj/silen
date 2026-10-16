using Silen.Core;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class DriverUpdateHealthTests
{
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public async Task Paused_disabled_or_stopped_status_does_not_query_COM(bool paused, bool enabled, bool running)
    {
        var queries = 0;
        var health = new WindowsUpdateHealth(true, paused, null, enabled, running, false, "Estado detenido o pausado");
        var service = new DriverUpdateService(_ => Task.FromResult(health), _ => { queries++; return Task.FromResult(new DriverUpdateResult(true, [], "No offers")); });
        var result = await service.FindUpdatesAsync();
        Assert.Equal(0, queries); Assert.False(result.Success); Assert.Equal(health, result.WindowsUpdate);
    }
    [Fact]
    public async Task Healthy_status_searches_and_a_later_pause_invalidates_an_old_success()
    {
        var health = new WindowsUpdateHealth(true, false, null, true, true, false, "Disponible");
        var service = new DriverUpdateService(_ => Task.FromResult(health), _ =>
        {
            health = health with { Paused = true };
            return Task.FromResult(new DriverUpdateResult(true, [], "No offers"));
        });
        var result = await service.FindUpdatesAsync();
        Assert.False(result.Success); Assert.True(result.WindowsUpdate!.Paused);
    }
}
