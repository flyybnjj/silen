using Silen.Core;
using Xunit;

namespace Silen.Tests;

public sealed class UserPreferencesThemeTests
{
    [Fact]
    public async Task First_use_defaults_to_dark_graphite()
    {
        Assert.Equal("Oscuro", new UserPreferences().Theme);
        Assert.Equal("Oscuro", (await new MemoryUserPreferencesStore().ReadAsync()).Theme);
    }

    [Fact]
    public async Task An_existing_light_preference_is_preserved()
    {
        var store = new MemoryUserPreferencesStore();
        await store.WriteAsync(new(true, "Claro"));
        Assert.Equal(new UserPreferences(true, "Claro"), await store.ReadAsync());
    }
}
