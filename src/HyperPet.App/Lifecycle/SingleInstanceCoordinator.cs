using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace HyperPet.App.Lifecycle;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private readonly CancellationTokenSource _stop = new();
    private bool _owns;
    public SingleInstanceCoordinator(string profile)
    {
        string user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        _pipeName = $"HyperPet-{user}-{profile}";
        _mutex = new Mutex(false, "Local\\" + _pipeName);
    }
    public bool TryAcquire()
    {
        try { return _owns = _mutex.WaitOne(0); }
        catch (AbandonedMutexException) { return _owns = true; }
    }
    public void StartActivationListener(Action activate)
    {
        _ = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    var oneByte = new byte[1];
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
                    timeout.CancelAfter(TimeSpan.FromSeconds(2));
                    if (await pipe.ReadAsync(oneByte, timeout.Token) == 1 && oneByte[0] == 1) activate();
                }
                catch (OperationCanceledException) { }
                catch (IOException) { if (!_stop.IsCancellationRequested) await Task.Delay(100, _stop.Token).ConfigureAwait(false); }
            }
        });
    }
    public async Task RequestActivationAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeout.Token);
            await pipe.WriteAsync(new byte[] { 1 }, timeout.Token);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or UnauthorizedAccessException) { }
    }
    public void Dispose()
    {
        _stop.Cancel();
        if (_owns) { _mutex.ReleaseMutex(); _owns = false; }
        _mutex.Dispose();
        // The listener owns pending cancellation registrations until it exits.
    }
}
