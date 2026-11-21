using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using Serilog;

namespace FluxKit.Platform.Watchers;

/// <summary>
/// Receives Live Activity updates from scripts, terminals and apps on this PC (ADR-016): a named pipe that only the
/// signed-in user can reach (<see cref="PipeOptions.CurrentUserOnly"/>, and the user's SID in its name). Each
/// connection sends one or more lines; they are handed on as text and never logged. The server waits for
/// connections without polling, and reads at most <see cref="MaxBytes"/> per connection.
/// </summary>
public sealed class LiveActivityServer(ILogger log) : IDisposable
{
    public const int MaxBytes = 16 * 1024;

    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger _log = log.ForContext<LiveActivityServer>();
    private CancellationTokenSource? _stop;

    /// <summary>The pipe's name for the signed-in user: <c>FluxKit.Island.&lt;SID&gt;</c>.</summary>
    public static string PipeName => "FluxKit.Island." + (WindowsIdentity.GetCurrent().User?.Value ?? "user");

    /// <summary>A line arrived. Raised on a thread-pool thread.</summary>
    public event EventHandler<string>? LineReceived;

    public void Start()
    {
        _stop = new CancellationTokenSource();
        CancellationToken token = _stop.Token;
        _ = Task.Run(() => ListenAsync(token), token);
    }

    public void Dispose()
    {
        _stop?.Cancel();
        _stop?.Dispose();
        _stop = null;
    }

    private async Task ListenAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop).ConfigureAwait(false);
                foreach (string line in await ReadLinesAsync(pipe, stop).ConfigureAwait(false))
                {
                    LineReceived?.Invoke(this, line);
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
            {
                // Another FluxKit owns the pipe, or a client went away mid-line: wait a moment and listen again.
                _log.Debug(ex, "Live Activity pipe error");
                await Task.Delay(TimeSpan.FromSeconds(2), stop).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Reads what one client sends, up to <see cref="MaxBytes"/> and a few seconds, split into lines.</summary>
    private static async Task<IReadOnlyList<string>> ReadLinesAsync(NamedPipeServerStream pipe, CancellationToken stop)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
        timeout.CancelAfter(ReadTimeout);
        byte[] buffer = new byte[MaxBytes];
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await pipe.ReadAsync(buffer.AsMemory(total), timeout.Token).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

/// <summary>Sends Live Activity lines to a running FluxKit (used by <c>FluxKit.exe island …</c>).</summary>
public static class LiveActivityClient
{
    /// <summary>Sends one line. False when FluxKit isn't running, Live Activities are off, or it didn't answer in time.</summary>
    public static bool Send(string line, TimeSpan timeout)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", LiveActivityServer.PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect((int)timeout.TotalMilliseconds);
            byte[] bytes = Encoding.UTF8.GetBytes(line + "\n");
            pipe.Write(bytes);
            pipe.Flush();
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
