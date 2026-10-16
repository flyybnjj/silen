using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Silen.Windows;

public sealed partial class NativeOptimizationService
{
    private IEnumerable<ToggleSetting> InspectInputPreferences(List<OptimizationJournalEntry> entries)
    {
        foreach (var id in new[] { "input-filterkeys", "input-mouse-linear" })
        {
            var keyboard = id == "input-filterkeys";
            var name = keyboard ? "Repetición de teclado 130/10 ms (FilterKeys)" : "Ratón sin aceleración de Windows";
            var description = keyboard
                ? "Ajuste global de teclado: aceptación 0 ms, repetición inicial 130 ms e intervalo 10 ms; sin BounceKeys, atajo de Mayús ni sonido por pulsación. Puede producir repeticiones más frecuentes al escribir. No cambia polling USB ni garantiza menos latencia en juegos con entrada directa. Se guarda cada campo anterior."
                : "Ajuste global de ratón: desactiva aceleración y umbrales de Windows. Conserva sensibilidad, DPI y polling; los juegos con Raw Input pueden ignorarlo. Cambia la sensación del puntero, no mide ni garantiza menor latencia física. Se guarda el estado anterior.";
            ToggleSetting result;
            try
            {
                var value = ReadInputValue(id);
                var target = keyboard ? InputPreferences.Encode(InputPreferences.Filter(value.Text!).Gaming()) : InputPreferences.Encode(new MouseMotionPreference(0, 0, 0));
                var restore = entries.Any(entry => entry.ToolId == id && CanUndo(entry));
                result = new(id, name, description, value.Text == target, value.Text == target ? "Ya tiene este ajuste de entrada" : "Opcional · cambia la entrada de Windows hasta restaurar",
                    true, restore, "Entrada de Windows · efecto global", UsesBackupRestore: true);
            }
            catch (Exception ex) when (IsExpectedFailure(ex)) { result = new(id, name, description, null, FriendlyError(ex), false, false, "Entrada de Windows · efecto global", UsesBackupRestore: true); }
            yield return result;
        }
    }
    private RegistryValueState ReadInputValue(string id)
    {
        if (id == "input-filterkeys")
        {
            var value = platform.GetFilterKeys();
            _ = InputPreferences.Filter(InputPreferences.Encode(value));
            if ((value.Flags & 2) == 0) throw new InvalidOperationException("FilterKeys no está disponible en esta sesión de Windows.");
            return RegistryValueState.String(InputPreferences.Encode(value));
        }
        var mouse = platform.GetMouseMotion();
        _ = InputPreferences.Mouse(InputPreferences.Encode(mouse));
        return RegistryValueState.String(InputPreferences.Encode(mouse));
    }
    private Task<OptimizationOutcome> ApplyInputPreferenceAsync(string id, CancellationToken token) => ExecuteAsync(_ =>
    {
        var previous = ReadInputValue(id);
        var keyboard = id == "input-filterkeys";
        var intended = RegistryValueState.String(keyboard ? InputPreferences.Encode(InputPreferences.Filter(previous.Text!).Gaming()) : InputPreferences.Encode(new MouseMotionPreference(0, 0, 0)));
        return NewEntry(id, keyboard ? "Teclado · FilterKeys 130/10 ms" : "Ratón · sin aceleración de Windows",
            [new(keyboard ? "FilterKeys" : "MouseMotion", "", "", previous, intended)]);
    }, token);
}

internal sealed partial class NativeOptimizationPlatform
{
    public FilterKeysPreference GetFilterKeys()
    {
        var value = new FilterKeysNative { Size = (uint)Marshal.SizeOf<FilterKeysNative>() };
        if (!FilterKeysSpi(0x32, value.Size, ref value, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(value.Flags, value.Wait, value.Delay, value.Repeat, value.Bounce);
    }
    public void SetFilterKeys(FilterKeysPreference preference)
    {
        _ = InputPreferences.Filter(InputPreferences.Encode(preference));
        var value = new FilterKeysNative { Size = (uint)Marshal.SizeOf<FilterKeysNative>(), Flags = preference.Flags, Wait = preference.Wait, Delay = preference.Delay, Repeat = preference.Repeat, Bounce = preference.Bounce };
        if (!FilterKeysSpi(0x33, value.Size, ref value, 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public MouseMotionPreference GetMouseMotion()
    {
        var values = new int[3];
        if (!GetMouseSpi(3, 0, values, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(values[0], values[1], values[2]);
    }
    public void SetMouseMotion(MouseMotionPreference preference)
    {
        _ = InputPreferences.Mouse(InputPreferences.Encode(preference));
        if (!SetMouseSpi(4, 0, [preference.Threshold1, preference.Threshold2, preference.Acceleration], 3)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FilterKeysNative { public uint Size, Flags, Wait, Delay, Repeat, Bounce; }
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool FilterKeysSpi(uint action, uint parameter, ref FilterKeysNative value, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetMouseSpi(uint action, uint parameter, [Out] int[] values, uint flags);
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetMouseSpi(uint action, uint parameter, [In] int[] values, uint flags);
}
