using Microsoft.Win32;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Silen.Windows;

internal static class OptimizationTargets
{
    internal const string GameBar = @"Software\Microsoft\GameBar";
    internal const string GameDvr = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";
    internal const string GameConfig = @"System\GameConfigStore";
    internal const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string GpuPreferences = @"Software\Microsoft\DirectX\UserGpuPreferences";
}

internal sealed record RegistryValueState(bool Exists, RegistryValueKind Kind = RegistryValueKind.None,
    string? Text = null, long Number = 0, string[]? Strings = null, byte[]? Bytes = null)
{
    internal static RegistryValueState Absent { get; } = new(false);
    internal static RegistryValueState Dword(int number) => new(true, RegistryValueKind.DWord, Number: number);
    internal static RegistryValueState String(string text, RegistryValueKind kind = RegistryValueKind.String) => new(true, kind, Text: text);

    internal bool Same(RegistryValueState other) => Exists == other.Exists && (!Exists ||
        Kind == other.Kind && Text == other.Text && Number == other.Number &&
        (Strings ?? []).SequenceEqual(other.Strings ?? []) && (Bytes ?? []).SequenceEqual(other.Bytes ?? []));

    internal void Validate()
    {
        if (!Exists) return;
        if (Kind is RegistryValueKind.String or RegistryValueKind.ExpandString && Text is not null) return;
        if (Kind == RegistryValueKind.DWord && Number >= int.MinValue && Number <= int.MaxValue) return;
        if (Kind == RegistryValueKind.QWord) return;
        if (Kind == RegistryValueKind.MultiString && Strings is not null && Strings.All(x => x is not null)) return;
        if (Kind is RegistryValueKind.Binary or RegistryValueKind.None && Bytes is not null) return;
        throw new InvalidDataException("El respaldo contiene un valor de registro incompatible.");
    }
}

internal interface IOptimizationPlatform
{
    FilterKeysPreference GetFilterKeys() => throw new InvalidOperationException("FilterKeys no disponible.");
    void SetFilterKeys(FilterKeysPreference value) => throw new InvalidOperationException("FilterKeys no disponible.");
    MouseMotionPreference GetMouseMotion() => throw new InvalidOperationException("Preferencia de ratón no disponible.");
    void SetMouseMotion(MouseMotionPreference value) => throw new InvalidOperationException("Preferencia de ratón no disponible.");
    ServiceSnapshot? ReadUpdateService() => null;
    bool CanManageUpdateService() => false;
    void SetUpdateServiceStartType(int startType) => throw new InvalidOperationException("Windows Update no está disponible.");
    void SetUpdateServiceRunning(bool running) => throw new InvalidOperationException("Windows Update no está disponible.");
    ServiceSnapshot? ReadService(string name) => null;
    bool CanManageService(string name) => false;
    void SetServiceStartType(string name, int startType) => throw new InvalidOperationException("Servicio no disponible.");
    void SetServiceRunning(string name, bool running) => throw new InvalidOperationException("Servicio no disponible.");
    bool CanWriteKey(string path);
    RegistryValueState ReadValue(string path, string name);
    void WriteValue(string path, string name, RegistryValueState value);
    IReadOnlyDictionary<string, RegistryValueState> ReadRunEntries();
    bool? GetAnimations();
    void SetAnimations(bool enabled);
    List<PowerScheme> ReadPowerSchemes();
    Guid GetActivePowerScheme();
    void SetActivePowerScheme(Guid id);
    Guid GetPowerMode() => throw new InvalidOperationException("El modo de energía no está disponible.");
    void SetPowerMode(Guid id) => throw new InvalidOperationException("El modo de energía no está disponible.");
    bool GetVisualEffect(uint action) => throw new InvalidOperationException("El efecto visual no está disponible.");
    void SetVisualEffect(uint action, bool enabled) => throw new InvalidOperationException("El efecto visual no está disponible.");
}

internal sealed partial class NativeOptimizationPlatform : IOptimizationPlatform
{
    private static (RegistryKey Root, string Relative) RegistryLocation(string path) =>
        path.StartsWith(@"HKLM\", StringComparison.OrdinalIgnoreCase)
            ? (Registry.LocalMachine, path[5..]) : (Registry.CurrentUser, path);

    public bool CanWriteKey(string path) => ProbeWritableKey(path, candidate =>
    {
        if (candidate.Equals("HKLM", StringComparison.OrdinalIgnoreCase)) return false;
        var (root, relative) = RegistryLocation(candidate);
        using var key = root.OpenSubKey(relative, writable: true);
        return key is null ? null : true;
    });

    internal static bool ProbeWritableKey(string path, Func<string, bool?> openWritable)
    {
        try
        {
            var candidate = path;
            while (!string.IsNullOrEmpty(candidate))
            {
                if (openWritable(candidate) is bool allowed) return allowed;
                var separator = candidate.LastIndexOf('\\');
                candidate = separator < 0 ? "" : candidate[..separator];
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or Win32Exception) { }
        return false;
    }

    public Guid GetPowerMode()
    {
        RequireAcPower();
        if (GetActivePowerScheme() != Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"))
            throw new InvalidOperationException("El modo de energía requiere el plan Equilibrado de Windows; se conserva el plan del fabricante.");
        var error = PowerGetActualOverlayScheme(out var mode);
        if (error != 0) throw new Win32Exception((int)error);
        return mode;
    }
    public void SetPowerMode(Guid id)
    {
        _ = GetPowerMode();
        var error = PowerSetActiveOverlayScheme(id);
        if (error != 0) throw new Win32Exception((int)error);
    }
    private static void RequireAcPower()
    {
        if (!GetSystemPowerStatus(out var status) || status.Ac != 1)
            throw new InvalidOperationException("Conecta el equipo a corriente para aplicar o restaurar el modo de rendimiento.");
    }
    public bool GetVisualEffect(uint action)
    {
        if (!VisualEffects.Allows(action)) throw new ArgumentException("Efecto no permitido.");
        if (!SystemParametersInfoGet(action, 0, out var enabled, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return enabled;
    }
    public void SetVisualEffect(uint action, bool enabled)
    {
        if (!VisualEffects.Allows(action)) throw new ArgumentException("Efecto no permitido.");
        if (!SystemParametersInfoSet(action + 1, 0, enabled ? new IntPtr(1) : IntPtr.Zero, 3))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public RegistryValueState ReadValue(string path, string name)
    {
        var (root, relative) = RegistryLocation(path);
        using var key = root.OpenSubKey(relative, writable: false);
        if (key is null || !key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase)) return RegistryValueState.Absent;
        var kind = key.GetValueKind(name);
        var value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => RegistryValueState.String((string)value!, kind),
            RegistryValueKind.DWord => RegistryValueState.Dword((int)value!),
            RegistryValueKind.QWord => new(true, kind, Number: (long)value!),
            RegistryValueKind.MultiString => new(true, kind, Strings: (string[])value!),
            RegistryValueKind.Binary or RegistryValueKind.None => new(true, kind, Bytes: (byte[])value!),
            _ => throw new InvalidDataException("Windows devolvió un tipo de registro no compatible con un respaldo exacto.")
        };
    }

    public void WriteValue(string path, string name, RegistryValueState value)
    {
        value.Validate();
        if (!value.Exists)
        {
            var (existingRoot, existingRelative) = RegistryLocation(path);
            using var existing = existingRoot.OpenSubKey(existingRelative, writable: true);
            existing?.DeleteValue(name, throwOnMissingValue: false);
            return;
        }
        var (root, relative) = RegistryLocation(path);
        using var key = root.CreateSubKey(relative, writable: true);
        object data = value.Kind switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => value.Text!,
            RegistryValueKind.DWord => checked((int)value.Number),
            RegistryValueKind.QWord => value.Number,
            RegistryValueKind.MultiString => value.Strings!,
            RegistryValueKind.Binary or RegistryValueKind.None => value.Bytes!,
            _ => throw new InvalidDataException("Tipo de registro no compatible.")
        };
        key.SetValue(name, data, value.Kind);
        key.Flush();
    }

    public IReadOnlyDictionary<string, RegistryValueState> ReadRunEntries()
    {
        using var key = Registry.CurrentUser.OpenSubKey(OptimizationTargets.Run, writable: false);
        return key?.GetValueNames().Where(x => !string.IsNullOrWhiteSpace(x)).ToDictionary(x => x,
            x => ReadValue(OptimizationTargets.Run, x), StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, RegistryValueState>();
    }

    public bool? GetAnimations()
    {
        if (!SystemParametersInfoGet(0x1042, 0, out var enabled, 0)) return null;
        return enabled;
    }

    public void SetAnimations(bool enabled)
    {
        if (!SystemParametersInfoSet(0x1043, 0, enabled ? new IntPtr(1) : IntPtr.Zero, 3))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public List<PowerScheme> ReadPowerSchemes()
    {
        var active = GetActivePowerScheme();
        var result = new List<PowerScheme>();
        for (uint index = 0; index < 1024; index++)
        {
            var bytes = new byte[16];
            uint size = 16;
            var error = PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16, index, bytes, ref size);
            if (error == 259) return result;
            if (error != 0 || size != 16) throw new Win32Exception((int)error);
            var id = new Guid(bytes);
            result.Add(new(id.ToString(), ReadFriendlyName(id), id == active));
        }
        throw new InvalidDataException("Windows devolvió demasiados planes de energía.");
    }

    public Guid GetActivePowerScheme()
    {
        var error = PowerGetActiveScheme(IntPtr.Zero, out var pointer);
        if (error != 0) throw new Win32Exception((int)error);
        try { return Marshal.PtrToStructure<Guid>(pointer); }
        finally { LocalFree(pointer); }
    }

    public void SetActivePowerScheme(Guid id)
    {
        if (!ReadPowerSchemes().Any(x => Guid.Parse(x.Id) == id))
            throw new InvalidOperationException("El plan de energía ya no existe.");
        var error = PowerSetActiveScheme(IntPtr.Zero, ref id);
        if (error != 0) throw new Win32Exception((int)error);
    }

    private static string ReadFriendlyName(Guid id)
    {
        uint size = 0;
        var error = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, null, ref size);
        if (error != 0 || size is 0 or > 65536) return id.ToString();
        var data = new byte[size];
        error = PowerReadFriendlyName(IntPtr.Zero, ref id, IntPtr.Zero, IntPtr.Zero, data, ref size);
        return error == 0 ? Encoding.Unicode.GetString(data, 0, (int)size).TrimEnd('\0') : id.ToString();
    }

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoGet(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfoSet(uint action, uint parameter, IntPtr value, uint flags);
    [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerGetActualOverlayScheme(out Guid scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveOverlayScheme(Guid scheme);
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus { public byte Ac, BatteryFlags, BatteryPercent, SystemStatus; public uint BatteryLife, FullBatteryLife; }
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
    [DllImport("powrprof.dll")] private static extern uint PowerEnumerate(IntPtr root, IntPtr scheme, IntPtr subgroup, uint accessor, uint index, byte[] buffer, ref uint size);
    [DllImport("powrprof.dll")] private static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr subgroup, IntPtr setting, byte[]? buffer, ref uint size);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
}
