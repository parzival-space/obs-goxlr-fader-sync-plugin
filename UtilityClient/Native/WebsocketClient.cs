using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace GoXLRUtilityClient.Native;

public class WebsocketClient : IDisposable
{
    private ClientWebSocket _client = new();
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task? _receiveMessageTask;
    private Task? _connectionTask;
    private bool _isConnected;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);  // For thread safety
    private readonly SemaphoreSlim _sendLock = new(1, 1);  // For thread safety on send

    public event EventHandler<string>? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnMessage;
    public event EventHandler<Exception>? OnError;

    private async Task ReceiveMessageTask()
    {
        var memoryStream = new MemoryStream();
        var buffer = new byte[1024]; // Buffer to store received data

        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (_client.State != WebSocketState.Open)
                {
                    await Task.Delay(20, _cancellationTokenSource.Token);
                    continue;
                }

                WebSocketReceiveResult? result = null;
                try
                {
                    result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    break;  // Task was canceled, exit gracefully
                }
                catch (Exception e)
                {
                    OnError?.Invoke(this, e);
                    break;
                }

                if (result is null) continue;
                switch (result.MessageType)
                {
                    case WebSocketMessageType.Text:
                        if (result.EndOfMessage && memoryStream.Position == 0)
                        {
                            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                            OnMessage?.Invoke(this, message);
                        }
                        else
                        {
                            await memoryStream.WriteAsync(buffer, 0, result.Count);
                            if (result.EndOfMessage)
                            {
                                var message = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Position);
                                OnMessage?.Invoke(this, message);
                                memoryStream.SetLength(0);
                            }
                        }
                        break;

                    case WebSocketMessageType.Close:
                        await HandleServerInitiatedClose();
                        return;

                    default:
                        await _client.CloseAsync(WebSocketCloseStatus.ProtocolError, "Only Text is supported.", CancellationToken.None);
                        OnDisconnected?.Invoke(this, "Connection closed because server tried to send binary or invalid message.");
                        return;
                }
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Ignore cancellation exceptions
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
        }
        finally
        {
            memoryStream.Dispose(); // Ensure memory stream is disposed
        }
    }

    private async Task HandleServerInitiatedClose()
    {
        if (_client.State == WebSocketState.CloseReceived)
        {
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing on server request", CancellationToken.None);
            await DisconnectAsync();  // Ensure proper cleanup
        }
    }

    private async Task ConnectionTask()
    {
        try
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                switch (_client.State)
                {
                    case WebSocketState.Open:
                        if (!_isConnected)
                        {
                            OnConnected?.Invoke(this, "Connected.");
                            _isConnected = true;
                        }
                        break;

                    case WebSocketState.Aborted:
                    case WebSocketState.Closed:
                        await DisconnectAsync();
                        return;  // Exit the loop and stop reconnection attempts
                }

                await Task.Delay(200, _cancellationTokenSource.Token);
            }
        }
        catch (OperationCanceledException) when (_cancellationTokenSource.Token.IsCancellationRequested)
        {
            // Ignore cancellation exceptions
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
        }
    }

    public bool IsConnectionAlive()
    {
        return _client.State == WebSocketState.Open;
    }

    public async Task<bool> ConnectAsync(string uri)
    {
        return await ConnectAsync(new Uri(uri));
    }

    protected async Task<bool> ConnectAsync(Uri uri)
    {
        await _connectionLock.WaitAsync();  // Ensure only one connection at a time
        try
        {
            if (_client.State == WebSocketState.Open) return true; // Already connected

            _cancellationTokenSource = new CancellationTokenSource();  // Reset cancellation token

            _receiveMessageTask = ReceiveMessageTask();
            _connectionTask = ConnectionTask();

            await _client.ConnectAsync(uri, _cancellationTokenSource.Token);

            return _client.State == WebSocketState.Open;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
            return false;
        }
        finally
        {
            _connectionLock.Release();  // Release the lock
        }
    }

    protected async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();  // Ensure thread safety for disconnection
        try
        {
            if (_client.State != WebSocketState.Aborted && _client.State != WebSocketState.Closed)
            {
                try
                {
                    await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", CancellationToken.None);
                }
                catch (Exception ex)
                {
                    OnError?.Invoke(this, ex);
                }
            }

            OnDisconnected?.Invoke(this, "Connection closed.");

            _cancellationTokenSource.Cancel();  // Cancel any ongoing operations

            await Task.WhenAny(Task.WhenAll(_receiveMessageTask ?? Task.CompletedTask, _connectionTask ?? Task.CompletedTask), Task.Delay(5000));

            DisposeTasks();

            // Dispose of WebSocket client and recreate it for next use
            _client.Dispose();
            _client = new ClientWebSocket();

            // Dispose and recreate the cancellation token source
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _isConnected = false;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, new Exception("An error occurred during disconnection.", ex));
        }
        finally
        {
            _connectionLock.Release();  // Release the lock
        }
    }

    private void DisposeTasks()
    {
        try
        {
            _receiveMessageTask?.Dispose();
            _receiveMessageTask = null;

            _connectionTask?.Dispose();
            _connectionTask = null;
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, new Exception("An error occurred while disposing tasks.", ex));
        }
    }

    protected async Task SendMessage(string message)
    {
        await _sendLock.WaitAsync();  // Ensure only one send operation at a time
        try
        {
            await _client.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                WebSocketMessageType.Text,
                true,
                _cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, ex);
        }
        finally
        {
            _sendLock.Release();  // Release the lock
        }
    }

    public void Dispose()
    {
        if (!_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }

        try
        {
            Task.WaitAll(new[] { _receiveMessageTask ?? Task.CompletedTask, _connectionTask ?? Task.CompletedTask }, TimeSpan.FromSeconds(5));
        }
        catch (Exception ex)
        {
            OnError?.Invoke(this, new Exception("Failed to dispose tasks.", ex));
        }

        _cancellationTokenSource.Dispose();
        _client.Dispose();
        _receiveMessageTask?.Dispose();
        _connectionTask?.Dispose();
        _connectionLock.Dispose();  // Dispose of the semaphore
        _sendLock.Dispose();  // Dispose of the semaphore
    }
}
