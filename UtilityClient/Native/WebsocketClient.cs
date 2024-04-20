using System.Net.WebSockets;
using System.Text;

namespace GoXLRUtilityClient.client;

public class WebsocketClient : IDisposable
{
    private ClientWebSocket _client = new();

    private CancellationTokenSource _cancellationTokenSource = new();
    private Task _receiveMessageTask;
    private Task _connectionTask;

    private bool _isConnected = false;

    public event EventHandler<string>? OnConnected;
    public event EventHandler<string>? OnDisconnected;
    public event EventHandler<string>? OnMessage;
    public event EventHandler<Exception>? OnError;

    private async Task ReceiveMessageTask()
    {
        var memoryStream = new MemoryStream();
        var buffer = new byte[1024]; // Buffer to store received data
        while (!this._cancellationTokenSource.IsCancellationRequested)
        {
            if (this._client.State != WebSocketState.Open)
            {
                await Task.Delay(20);
                continue;
            }

            WebSocketReceiveResult result = null;
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
                        break;
                    }
                    else
                    {
                        memoryStream.Write(buffer, 0, result.Count);
                        if (result.EndOfMessage)
                        {
                            var message = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Position);
                            this.OnMessage?.Invoke(this, message);
                            memoryStream.SetLength(0);
                        }
                    }
                    break;
                
                case WebSocketMessageType.Close:
                    // If the server initiates the close handshake, handle it
                    Task.Run(DisconnectAsync);
                    break;
                
                case WebSocketMessageType.Binary:
                default:
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
                    if (this._isConnected) this.OnDisconnected?.Invoke(this, "Connection closed.");
                    this._isConnected = false;
                    break;
                
                default:
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

    public async Task<bool> ConnectAsync(Uri uri)
    {   
        this._receiveMessageTask = ReceiveMessageTask();
        this._connectionTask = ConnectionTask();
        await this._client.ConnectAsync(uri, this._cancellationTokenSource.Token);
        return this._client.State == WebSocketState.Open;
    }

    public async Task DisconnectAsync()
    {
        await this._client.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        this.OnDisconnected?.Invoke(this, "Connection closed.");
        
        this._cancellationTokenSource.Cancel();
        var shutdownSuccessfull = Task.WaitAll(new[] { this._receiveMessageTask, this._connectionTask },
            TimeSpan.FromSeconds(5));
        if (!shutdownSuccessfull)
        {
            this.OnError?.Invoke(this, new Exception("Failed to dispose tasks."));
            return;
        }
        
        this._receiveMessageTask.Dispose();
        this._connectionTask.Dispose();
    }

    public async Task SendMessage(string message)
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

        var shutdownSuccessfull = Task.WaitAll(new[] { this._receiveMessageTask, this._connectionTask },
            TimeSpan.FromSeconds(5));
        if (!shutdownSuccessfull)
        {
            this.OnError?.Invoke(this, new Exception("Failed to dispose tasks."));
            return;
        }
        
        this._cancellationTokenSource.Dispose();
        this._client.Dispose();
        this._receiveMessageTask.Dispose();
        this._connectionTask.Dispose();
    }
}