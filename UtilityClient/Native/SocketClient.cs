using System.IO.Pipes;
using System.Text;

namespace GoXLRUtilityClient.Native;

public class SocketClient : IDisposable
{
    /// How long we wait for the GoXLR Utility to accept our connection.
    private const int DefaultConnectTimeout = 5000;

    private readonly BinaryReader _reader;
    private readonly BinaryWriter _writer;
    private readonly NamedPipeClientStream _client;

    public bool IsConnected => this._client.IsConnected;

    /**
     * Connects to the Utility. Never blocks forever: without a timeout a missing Utility would
     * hang the calling thread indefinitely instead of letting the caller retry.
     */
    public void Connect(int timeoutMilliseconds = DefaultConnectTimeout) => _client.Connect(timeoutMilliseconds);

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

        // ReadBytes returns a short buffer instead of throwing when the pipe closes mid-message
        if (lengthBytes.Length < 4) return "";

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
        // Disposing the reader/writer also disposes the underlying pipe stream, but only if we ever
        // got that far - dispose the pipe explicitly so a failed Connect() cannot leak the handle.
        try { _writer.Dispose(); } catch (Exception) { /* already torn down */ }
        try { _reader.Dispose(); } catch (Exception) { /* already torn down */ }
        _client.Dispose();

        GC.SuppressFinalize(this);
    }
}
