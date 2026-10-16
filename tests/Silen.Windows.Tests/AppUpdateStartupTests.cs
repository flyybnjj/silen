using System.IO.Pipes;
using Xunit;

namespace Silen.Windows.Tests;

public sealed class AppUpdateStartupTests
{
    private static Task Never => Task.Delay(Timeout.InfiniteTimeSpan);

    [Fact]
    public async Task Healthy_start_requires_the_exact_launched_process_and_transaction()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        var confirmed = AppUpdateStartup.ConfirmAsync(pipe, session, Environment.ProcessId, Never, TimeSpan.FromSeconds(3));
        await AppUpdateStartup.ReportReadyAsync(new(session, Environment.ProcessId));
        await confirmed.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task An_open_process_without_an_ack_times_out()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        await Assert.ThrowsAsync<TimeoutException>(() => AppUpdateStartup.ConfirmAsync(pipe, session,
            Environment.ProcessId, Never, TimeSpan.FromMilliseconds(80)));
    }

    [Fact]
    public async Task Premature_exit_is_not_a_successful_launch()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        await Assert.ThrowsAsync<IOException>(() => AppUpdateStartup.ConfirmAsync(pipe, session,
            Environment.ProcessId, Task.CompletedTask, TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public async Task Another_process_cannot_acknowledge_the_launched_application()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        var confirmed = AppUpdateStartup.ConfirmAsync(pipe, session, int.MaxValue, Never, TimeSpan.FromSeconds(3));
        using var client = new NamedPipeClientStream(".", "Silen.Update.Startup." + session, PipeDirection.Out, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => confirmed);
    }

    [Fact]
    public async Task A_valid_process_with_a_foreign_nonce_is_rejected()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        var confirmed = AppUpdateStartup.ConfirmAsync(pipe, session, Environment.ProcessId, Never, TimeSpan.FromSeconds(3));
        using var client = new NamedPipeClientStream(".", "Silen.Update.Startup." + session, PipeDirection.Out, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000);
        await client.WriteAsync(Guid.NewGuid().ToByteArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => confirmed);
    }

    [Fact]
    public async Task The_application_rejects_a_pipe_owned_by_another_updater()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        var connection = pipe.WaitForConnectionAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => AppUpdateStartup.ReportReadyAsync(new(session, int.MaxValue)));
        await connection;
    }

    [Fact]
    public async Task Closing_the_channel_without_a_complete_ack_is_not_a_healthy_start()
    {
        var session = Guid.NewGuid().ToString("N");
        using var pipe = AppUpdateStartup.CreatePipe(session);
        var confirmed = AppUpdateStartup.ConfirmAsync(pipe, session, Environment.ProcessId, Never, TimeSpan.FromSeconds(3));
        using (var client = new NamedPipeClientStream(".", "Silen.Update.Startup." + session, PipeDirection.Out, PipeOptions.Asynchronous))
        { await client.ConnectAsync(1000); await client.WriteAsync(new byte[] { 1 }); }
        await Assert.ThrowsAnyAsync<IOException>(() => confirmed);
    }

    [Theory]
    [InlineData("../escape", "1")]
    [InlineData("11111111111111111111111111111111", "0")]
    [InlineData("11111111111111111111111111111111", "-1")]
    public void Invalid_arguments_are_rejected(string session, string pid) =>
        Assert.Throws<ArgumentException>(() => AppUpdateStartup.Parse([AppUpdateStartup.Argument, session, pid]));

    [Fact]
    public void Launch_uses_typed_arguments_and_never_requests_elevation()
    {
        var session = Guid.NewGuid().ToString("N");
        var start = AppUpdateStartup.StartInfo(@"C:\Silen con espacios\Silen.exe", session);
        Assert.False(start.UseShellExecute);
        Assert.Empty(start.Verb);
        var ticket = AppUpdateStartup.Parse(start.ArgumentList.ToArray());
        Assert.Equal(session, ticket.Session);
        Assert.Equal(Environment.ProcessId, ticket.UpdaterId);
        Assert.Throws<ArgumentException>(() => AppUpdateStartup.Parse([.. start.ArgumentList, "extra"]));
    }
}
