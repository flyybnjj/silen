
using System.Globalization;

if (args.Length == 3 && args[0] == "--terminate_existing_session" && args[1] == "--session_name" && args[2].StartsWith("Silen-", StringComparison.Ordinal))
{
    var sessionDirectory = Environment.GetEnvironmentVariable("SILEN_TEST_SESSION_DIRECTORY")!;
    File.Delete(Path.Combine(sessionDirectory, args[2] + ".session"));
    return 0;
}

var expectedFlags = new HashSet<string> { "--process_id", "--timed", "--output_file", "--session_name", "--terminate_after_timed", "--no_console_stats", "--v1_metrics" };
var options = new Dictionary<string, string>();
for (var i = 0; i < args.Length; i++)
{
    if (!expectedFlags.Remove(args[i])) return 91;
    if (args[i] is "--terminate_after_timed" or "--no_console_stats" or "--v1_metrics") continue;
    options.Add(args[i], args[++i]);
}
if (expectedFlags.Count > 0 || !options["--session_name"].StartsWith("Silen-", StringComparison.Ordinal)) return 92;
if (!int.TryParse(options["--timed"], out var duration) || duration is < 15 or > 300) return 93;
if (!int.TryParse(options["--process_id"], out var pid) || pid <= 0) return 94;
var output = options["--output_file"];
if (!Path.IsPathFullyQualified(output) || File.Exists(output)) return 95;
if (pid is 4102 or 4107)
{
    Console.Error.WriteLine("Permiso ETW denegado de prueba.");
    if (pid == 4107)
    {
        Console.Error.Write(new string('e', 100_000));
        Console.Out.Write(new string('o', 100_000));
    }
    return 7;
}
if (pid == 4104) return 0;
if (pid == 4105)
{
    File.WriteAllText(output, "Application,ProcessID,MsBetweenPresents\ngame.exe,4105,NA\n");
    return 0;
}
File.WriteAllText(output, $"Application,ProcessID,MsBetweenPresents\ngame.exe,{pid},10\ngame.exe,{pid},20\n");
if (pid == 4106) File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!, "capture.csv"), "created by another writer");
if (pid is 4103 or 4109)
{
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(output)!, options["--session_name"] + ".session"), output);
    File.WriteAllText(output + ".pid", Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    await Task.Delay(Timeout.InfiniteTimeSpan);
}
return 0;
