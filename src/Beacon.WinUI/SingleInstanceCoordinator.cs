using System.IO.Pipes;
using System.Text;

namespace Beacon.WinUI;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\Beacon.Next.Singleton";
    private const string PipeName = "Beacon.Next.Activation";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stopping = new();
    private Action? _activation;
    private Task? _listener;

    private SingleInstanceCoordinator(Mutex mutex)
    {
        _mutex = mutex;
    }

    public static SingleInstanceCoordinator? TryAcquire()
    {
        var mutex = new Mutex(true, MutexName, out var createdNew);
        if (createdNew)
        {
            return new SingleInstanceCoordinator(mutex);
        }

        mutex.Dispose();
        return null;
    }

    public static async Task<bool> SignalExistingAsync()
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(3000);
            await pipe.WriteAsync("activate"u8.ToArray());
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    public void StartListening(Action activation)
    {
        _activation = activation;
        _listener = ListenAsync(_stopping.Token);
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellationToken);
                var buffer = new byte[16];
                var count = await pipe.ReadAsync(buffer, cancellationToken);
                if (Encoding.UTF8.GetString(buffer, 0, count) == "activate")
                {
                    _activation?.Invoke();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        try
        {
            _listener?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _stopping.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
