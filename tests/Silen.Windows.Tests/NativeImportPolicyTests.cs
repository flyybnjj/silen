using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class NativeImportPolicyTests
{
    [Fact]
    public void Own_native_imports_cannot_search_the_portable_or_current_directory()
    {
        var assembly = typeof(SystemActionHost).Assembly;
        var imports = assembly.GetTypes().SelectMany(type => type.GetMethods(
            BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(method => method.IsDefined(typeof(DllImportAttribute))).ToArray();
        Assert.NotEmpty(imports);
        foreach (var method in imports)
        {
            Assert.Contains(method.GetCustomAttribute<DllImportAttribute>()!.Value.ToLowerInvariant(),
                new[] { "kernel32.dll", "user32.dll", "powrprof.dll", "crypt32.dll", "advapi32.dll", "ole32.dll", "ntdll.dll", "srclient.dll" });
            var policy = method.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>() ??
                assembly.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>();
            Assert.True(policy?.Paths == DllImportSearchPath.System32,
                $"{method.DeclaringType?.FullName}.{method.Name} debe resolver exclusivamente desde System32.");
        }
    }
}
