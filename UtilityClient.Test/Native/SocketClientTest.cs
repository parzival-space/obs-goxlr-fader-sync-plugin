using System.IO.Pipes;
using System.Text;
using GoXLRUtilityClient.Native;

namespace UtilityClient.Test.Native;

[TestFixture]
[TestOf(typeof(SocketClient))]
public class SocketClientTest
{
    private readonly string _pipeNamePrefix = "goxlr-utility-mock-pipe-";

    [Test]
    public void Connect_ShouldSetIsConnectedToTrue()
    {
        var pipeName = _pipeNamePrefix + Guid.NewGuid();
        var mockPipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut);
        var socketClient = new SocketClient(pipeName);

        Assert.That(socketClient.IsConnected, Is.False);
        
        // Act
        socketClient.Connect();

        // Assert
        Assert.That(socketClient.IsConnected);
        
        mockPipeServer.Dispose();
    }

    [Test]
    public void SendMessage_ShouldSendCorrectMessage()
    {
        var pipeName = _pipeNamePrefix + Guid.NewGuid();
        var testMessage = "Hello, World!";

        // connect client using a new thread
        var clientThread = new Thread(() =>
        {
            var socketClient = new SocketClient(pipeName);
            socketClient.Connect();
            socketClient.SendMessage(testMessage);
            socketClient.Dispose();
        });
        clientThread.Start();
        
        // connect server
        var mockNamedPipeServerStream = new NamedPipeServerStream(pipeName);
        var reader = new BinaryReader(mockNamedPipeServerStream);

        mockNamedPipeServerStream.WaitForConnection();
        Assert.That(mockNamedPipeServerStream.IsConnected, Is.True);

        var messageLength = -1;
        var messageLengthBytes = reader.ReadBytes(4);
        if (BitConverter.IsLittleEndian) Array.Reverse(messageLengthBytes);
        messageLength = BitConverter.ToInt32(messageLengthBytes);
        Assert.That(messageLength, Is.EqualTo(testMessage.Length));

        var messageBytes = reader.ReadBytes(messageLength);
        Assert.That(Encoding.UTF8.GetString(messageBytes), Is.EqualTo(testMessage));

        mockNamedPipeServerStream.Dispose();
        
        // wait for client to finish
        clientThread.Join();
    }
    
    [Test]
    public void ReadMessage_ShouldReadCorrectMessage()
    {
        var pipeName = _pipeNamePrefix + Guid.NewGuid();
        var testMessage = "Hello, World!";

        // connect server using a new thread
        var serverThread = new Thread(() =>
        {
            var mockNamedPipeServerStream = new NamedPipeServerStream(pipeName);
            var writer = new BinaryWriter(mockNamedPipeServerStream);
            
            mockNamedPipeServerStream.WaitForConnection();
            Assert.That(mockNamedPipeServerStream.IsConnected, Is.True);

            var messageBytes = Encoding.UTF8.GetBytes(testMessage);
            var messageLengthBytes = BitConverter.GetBytes(messageBytes.Length);
            if (BitConverter.IsLittleEndian) Array.Reverse(messageLengthBytes);
            
            writer.Write(messageLengthBytes);
            writer.Write(messageBytes);
            writer.Flush();
            
            mockNamedPipeServerStream.Dispose();
        });
        serverThread.Start();
        
        // connect client
        var socketClient = new SocketClient(pipeName);
        socketClient.Connect();
        var message = socketClient.ReadMessage();
        Assert.That(message, Is.EqualTo(testMessage));
        
        // wait for server to finish
        serverThread.Join();
    }
}