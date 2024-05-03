using System.Text.Json;
using System.Text.Json.Nodes;
using GoXLRUtilityClient.client;
using Json.Patch;

namespace GoXLRUtilityClient;

public class Utility : WebsocketClient
{
    private uint _operationIncrement = 1;
    private event EventHandler<MessageData>? OnMessageReceived; 
    public event EventHandler<Exception>? OnException; 
    
    public JsonNode Status;

    public Utility() : base()
    {
        this.Status = JsonNode.Parse("{}")!;
        
        // basic message handler
        base.OnMessage += (object? sender, string message) =>
        {
            try
            {
                var jsonNode = JsonNode.Parse(message);

                // test if message is valid
                if (jsonNode!["id"] == null || jsonNode!["data"] == null) return;

                var isPatchMessage = jsonNode!["data"]!["Patch"] != null;
                var id = jsonNode!["id"]!.GetValue<ulong>();
                var data = jsonNode!["data"]!;

                this.OnMessageReceived?.Invoke(this, new MessageData(id, data, isPatchMessage));

                // handle patch messages
                if (isPatchMessage)
                {
                    var patchString = data["Patch"]!.ToJsonString();
                    var patch = JsonSerializer.Deserialize<JsonPatch>(patchString);
                    var resultResult = patch?.Apply(this.Status);
                    this.Status = resultResult!.Result!;
                }
            }
            catch (Exception je)
            {
                OnException?.Invoke(this, je);
            } // nothing
        };
    }

    private async Task<JsonNode?> AwaitResponse(uint operationId)
    {
        using var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var responseSource = new TaskCompletionSource<JsonNode>();

        this.OnMessageReceived += TempMessageHandler;
        void TempMessageHandler(object? _, MessageData message)
        {
            if (message.Id == operationId)
            {
                responseSource.SetResult(message.Data);
            }
        }

        try
        {
            var responseTask = responseSource.Task;
            var completedTask = await Task.WhenAny(responseTask, Task.Delay(-1, timeoutSource.Token));

            if (completedTask == responseTask)
                return await responseTask;

            return null;
        }
        catch
        {
            return null;
        }
        finally
        {
            this.OnMessageReceived -= TempMessageHandler;
        }
    }

    public new async Task ConnectAsync()
    {
        String pipeName = OperatingSystem.IsWindows() ? "@goxlr.socket" : "/tmp/goxlr.socket";
        SocketClient socketClient = new SocketClient(pipeName);
        socketClient.Connect();
        
        // try getting the websocket url
        socketClient.SendMessage("\"GetStatus\"");
        string response = socketClient.ReadMessage();
        
        // close socket client connection
        socketClient.Dispose();
        
        // try parsing the response
        JsonNode? status = JsonNode.Parse(response);
        if (status == null) throw new JsonException("Failed to parse status response.");
        
        // find socket address
        string host = status["Status"]?["config"]?["http_settings"]?["bind_address"]?.GetValue<string>() ?? "";
        Int32 port = status["Status"]?["config"]?["http_settings"]?["port"]?.GetValue<Int32>() ?? 0;
        await this.ConnectAsync($"ws://{(host == "0.0.0.0" ? "127.0.0.1" : host)}:{port}/api/websocket");
    }

    public new async Task ConnectAsync(string uri)
    {
        await this.ConnectAsync(new Uri(uri));
    }
    
    public new async Task ConnectAsync(Uri uri)
    {
        await base.ConnectAsync(uri);

        var requestId = this.GetNewId();
        
        // request status
        var statusRequest = JsonNode.Parse("{}");
        statusRequest!["id"] = requestId;
        statusRequest!["data"] = "GetStatus";
        await base.SendMessage(statusRequest.ToJsonString());

        var result = await this.AwaitResponse(requestId);
        if (result != null) this.Status = result["Status"]!;
        else throw new InvalidDataException("Server failed to respond with current status.");
        
        Console.WriteLine($"Connected to GoXLR Utility v{result["Status"]?["config"]?["daemon_version"]}");
    }

    private uint GetNewId() => Interlocked.Increment(ref this._operationIncrement);
    
    private class MessageData(ulong id, JsonNode node, bool isPatch)
    {
        public readonly ulong Id = id;
        public readonly JsonNode Data = node;
        public readonly bool IsPatch = isPatch;
    }
}