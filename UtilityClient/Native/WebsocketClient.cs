using System.Net.WebSockets;
using System.Text;

namespace GoXLRUtilityClient.Native;

public class WebsocketClient : IDisposable
{
    private ClientWebSocket _client = new();

    private CancellationTokenSource _cancellationTokenSource = new();
    private Task? _receiveMessageTask;
    private Task? _connectionTask;

    private bool _isConnected;

    public event EventHandler<string>? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnMessage;
    public event EventHandler<Exception>? OnError;

    private async Task ReceiveMessageTask()
    {
        var memoryStream = new MemoryStream();
        var hasReceivedCloseMessage = false;
        var buffer = new byte[1024]; // Buffer to store received data
        while (!this._cancellationTokenSource.IsCancellationRequested)
        {
            if (this._client.State != WebSocketState.Open)
            {
                await Task.Delay(20);
                continue;
            }

            WebSocketReceiveResult? result = null;
            try
            {
                result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer),
                    this._cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                /* do nothing */
            }
            catch (Exception e)
            {
                this.OnError?.Invoke(this, e);
            }
            
            if (result is null) continue;
            switch (result.MessageType)
            {
                case WebSocketMessageType.Text:
                    if (result.EndOfMessage && memoryStream.Position == 0)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        this.OnMessage?.Invoke(this, message);
                    }
                    else
                    {
                        await memoryStream.WriteAsync(buffer, 0, result.Count);
                        if (result.EndOfMessage)
                        {
                            var message = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Position);
                            this.OnMessage?.Invoke(this, message);
                            memoryStream.SetLength(0);
                        }
                    }
                    break;
                
                case WebSocketMessageType.Close:
                    if (hasReceivedCloseMessage) break;
                    hasReceivedCloseMessage = true;
    
                    // If the server initiates the close handshake, handle it
                    await Task.Run(DisconnectAsync);
                    break;
                
                default:
                    // This won't occur with the Utility, but we need to trigger DisconnectAsync to perform tidying up!
                    await _client.CloseAsync(WebSocketCloseStatus.ProtocolError, "Only Text is supported.", CancellationToken.None);
                    this.OnDisconnected?.Invoke(this, "Connection closed because server tried to send binary or invalid message.");
                    break;
            }
        }
    }

    private async Task ConnectionTask()
    {
        while (!this._cancellationTokenSource.IsCancellationRequested)
        {
            switch (this._client.State)
            {
                case WebSocketState.Open:
                    if (!this._isConnected) this.OnConnected?.Invoke(this, "Connected.");
                    this._isConnected = true;
                    break;
                    
                case WebSocketState.Aborted:
                case WebSocketState.Closed:
                    // Trigger an internal disconnect to clean resources.
                    await Task.Run(DisconnectAsync);
                    break;
            }

            await Task.Delay(200);
        }
    }

    public bool IsConnectionAlive()
    {
        return this._client.State == WebSocketState.Open;
    }

    public async Task<bool> ConnectAsync(string uri)
    {
        return await this.ConnectAsync(new Uri(uri));
    }

    protected async Task<bool> ConnectAsync(Uri uri)
    {   
        this._receiveMessageTask = ReceiveMessageTask();
        this._connectionTask = ConnectionTask();
        await this._client.ConnectAsync(uri, this._cancellationTokenSource.Token);
        return this._client.State == WebSocketState.Open;
    }

    protected async Task DisconnectAsync()
    {
        // Only attempt to close the socket if it's not already closed
        if (this._client.State != WebSocketState.Aborted && this._client.State != WebSocketState.Closed) {
            await this._client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        this.OnDisconnected?.Invoke(this, "Connection closed.");
        
        await _cancellationTokenSource.CancelAsync();
        var shutdownSuccessful = Task.WaitAll(new[] { this._receiveMessageTask, this._connectionTask },
            TimeSpan.FromSeconds(5));
        if (!shutdownSuccessful)
        {
            this.OnError?.Invoke(this, new Exception("Failed to dispose tasks."));
            return;
        }
        
        this._receiveMessageTask!.Dispose();
        this._connectionTask!.Dispose();

        // Dispose of, and create a new client / Token for future connections
        this._client.Dispose();
        this._client = new();
        this._cancellationTokenSource.Dispose();
        this._cancellationTokenSource = new();

        // Flag the connection as disconnected
        this._isConnected = false;
    }

    protected async Task SendMessage(string message)
    {
        await this._client.SendAsync(
            new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
            WebSocketMessageType.Text,
            true,
            this._cancellationTokenSource.Token);
    }
    
    public void Dispose()
    {
        this._cancellationTokenSource.Cancel();

        var shutdownSuccessful = Task.WaitAll(new[] { this._receiveMessageTask, this._connectionTask },
            TimeSpan.FromSeconds(5));
        if (!shutdownSuccessful)
        {
            this.OnError?.Invoke(this, new Exception("Failed to dispose tasks."));
            return;
        }
        
        this._cancellationTokenSource.Dispose();
        this._client.Dispose();
        this._receiveMessageTask!.Dispose();
        this._connectionTask!.Dispose();
    }
}