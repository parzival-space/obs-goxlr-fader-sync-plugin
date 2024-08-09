using System.IO.Pipes;
using System.Text;

namespace GoXLRUtilityClient.client;

public class SocketClient : IDisposable
{
    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly NamedPipeClientStream _client;

    public bool IsConnected => this._client.IsConnected;
    public void Connect() => _client.Connect();
    
    /**
     * Sends and understands messages using sockets and NamedPipes in the following format:
     * [Message Length as unsigned 32bit BigEndian Integer][Text]
     */
    public SocketClient(string socketName)
    {
        this._client = new NamedPipeClientStream(socketName);
        this._reader = new BinaryReader(this._client);
        this._writer = new BinaryWriter(this._client);
    }

    public void SendMessage(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var lengthBytes = BitConverter.GetBytes(messageBytes.Length);
        
        // convert to big endian system
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

        _writer.Write(lengthBytes);
        _writer.Write(messageBytes);
        _writer.Flush();
    }

    public string ReadMessage()
    {
        // read message length
        byte[] lengthBytes;
        try { lengthBytes = _reader.ReadBytes(4); }
        catch (IOException) { return ""; }
        
        if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);
        var messageLength = BitConverter.ToUInt32(lengthBytes);
        
        // read message
        byte[] messageBytes;
        try { messageBytes = _reader.ReadBytes((int)messageLength); }
        catch (IOException) { return ""; }

        return Encoding.UTF8.GetString(messageBytes);
    }
    
    public void Dispose()
    {
        if (_client.IsConnected)
        {
            _reader.Close();
            _writer.Close();
            _client.Dispose();
        }
        
        _reader.Dispose();
        _writer.Dispose();
        _client.Dispose();
    }
}