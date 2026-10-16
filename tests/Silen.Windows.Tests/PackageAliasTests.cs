using Silen.Windows;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class PackageAliasTests
{
    [Theory]
    [InlineData(@"C:\Users\Fixture\AppData\Local\Silen\journal.lock", @"C:\Users\Fixture\AppData\Local\Packages\Fixture.Package_abc\LocalCache\Local\Silen\journal.lock", true)]
    [InlineData(@"C:\Users\Fixture\AppData\Local\Silen\journal.lock", @"C:\Users\Fixture\AppData\Local\Packages\Other.Package_abc\LocalCache\Local\Silen\journal.lock", false)]
    [InlineData(@"C:\Users\Fixture\AppData\LocalOther\Silen\journal.lock", @"C:\Users\Fixture\AppData\Local\Packages\Fixture.Package_abc\LocalCache\Local\Silen\journal.lock", false)]
    [InlineData(@"C:\Users\Fixture\AppData\Local\Silen\journal.lock", @"C:\Users\Fixture\AppData\Local\Packages\Fixture.Package_abc\LocalCache\Local\Other\journal.lock", false)]
    public void Only_the_exact_file_in_the_current_package_cache_is_a_valid_alias(string logical, string actual, bool expected)
    {
        Assert.Equal(expected, FortniteSafeFiles.IsPackageAlias(logical, actual, @"C:\Users\Fixture\AppData\Local", "Fixture.Package_abc"));
    }
}
