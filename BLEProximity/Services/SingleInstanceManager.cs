using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace BLEProximity.Services;

/// <summary>
/// Manages single-instance enforcement using a named Mutex and IPC via named pipes.
/// The first instance acquires the mutex and listens for restore messages.
/// Subsequent instances send a restore message and terminate.
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string DefaultMutexName = "BLEProximity_SingleInstance";
    private const string DefaultPipeName = "BLEProximity_Pipe";
    private const string RestoreMessage = "RESTORE";

    private readonly string _mutexName;
    private readonly string _pipeName;

    private Mutex? _mutex;
    private bool _mutexAcquired;
    private CancellationTokenSource? _listenerCts;
    private Task? _listenerTask;
    private bool _disposed;

    /// <summary>
    /// Creates a SingleInstanceManager with default mutex and pipe names.
    /// </summary>
    public SingleInstanceManager() : this(DefaultMutexName, DefaultPipeName)
    {
    }

    /// <summary>
    /// Creates a SingleInstanceManager with custom mutex and pipe names (for testing).
    /// </summary>
    /// <param name="mutexName">The name of the system-wide mutex.</param>
    /// <param name="pipeName">The name of the named pipe for IPC.</param>
    public SingleInstanceManager(string mutexName, string pipeName)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
    }

    /// <summary>
    /// Attempts to acquire the named mutex for single-instance enforcement.
    /// </summary>
    /// <returns>true if the mutex was successfully acquired (this is the first instance); false otherwise.</returns>
    /// <exception cref="SingleInstanceException">
    /// Thrown when the mutex cannot be acquired due to permissions or system resource limits
    /// (not because another instance is running).
    /// </exception>
    public bool TryAcquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, name: _mutexName, createdNew: out bool createdNew);

            if (createdNew)
            {
                _mutexAcquired = true;
                return true;
            }

            // Another instance already holds the mutex
            _mutex.Dispose();
            _mutex = null;
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new SingleInstanceException(
                $"Cannot start application: insufficient permissions to create the instance lock. {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new SingleInstanceException(
                $"Cannot start application: system I/O error while creating the instance lock. {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            throw new SingleInstanceException(
                $"Cannot start application: failed to acquire instance lock. {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Starts listening on a named pipe for restore messages from new instances.
    /// When a restore message is received, the provided callback is invoked.
    /// </summary>
    /// <param name="onRestoreRequested">
    /// Action to invoke when a restore message is received.
    /// This will be called on a background thread; use Dispatcher.Invoke to marshal to the UI thread.
    /// </param>
    public void StartListening(Action onRestoreRequested)
    {
        if (!_mutexAcquired)
            throw new InvalidOperationException("Cannot start listening without first acquiring the mutex.");

        _listenerCts = new CancellationTokenSource();
        var token = _listenerCts.Token;

        _listenerTask = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server);
                    var message = await reader.ReadLineAsync(token);

                    if (string.Equals(message, RestoreMessage, StringComparison.OrdinalIgnoreCase))
                    {
                        onRestoreRequested?.Invoke();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                    break;
                }
                catch (IOException)
                {
                    // Pipe error, continue listening
                }
            }
        }, token);
    }

    /// <summary>
    /// Sends a restore message to the existing instance via named pipe.
    /// This is called by the second instance before it terminates.
    /// </summary>
    public void SendRestoreMessage()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 3000); // 3 second timeout

            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(RestoreMessage);
        }
        catch
        {
            // If we can't connect to the existing instance, terminate silently.
            // The existing instance will continue running regardless.
        }
    }

    /// <summary>
    /// Releases the mutex and stops the pipe listener.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _listenerCts?.Cancel();

        try
        {
            _listenerTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Task may have been cancelled
        }

        _listenerCts?.Dispose();
        _listenerCts = null;
        _listenerTask = null;

        if (_mutexAcquired && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was not owned by this thread (shouldn't happen)
            }
        }

        _mutex?.Dispose();
        _mutex = null;
        _mutexAcquired = false;
    }
}

/// <summary>
/// Exception thrown when the single-instance mutex cannot be acquired
/// due to permissions or system resource issues (not because another instance is running).
/// </summary>
public class SingleInstanceException : Exception
{
    public SingleInstanceException(string message) : base(message) { }
    public SingleInstanceException(string message, Exception innerException) : base(message, innerException) { }
}
