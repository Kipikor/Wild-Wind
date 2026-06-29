using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using McpUnity.Utils;

namespace McpUnity.Unity
{
    internal sealed class McpUnityLocalWebSocketServer : IDisposable
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const int MaxHeaderBytes = 16 * 1024;
        private const int MaxPayloadBytes = 4 * 1024 * 1024;

        private readonly string _host;
        private readonly int _port;
        private readonly McpUnityServer _server;
        private readonly int _connectionGeneration;
        private readonly ConcurrentDictionary<string, ClientConnection> _clients = new ConcurrentDictionary<string, ClientConnection>();

        private TcpListener _listener;
        private CancellationTokenSource _cts;

        public McpUnityLocalWebSocketServer(string host, int port, McpUnityServer server, int connectionGeneration)
        {
            _host = host;
            _port = port;
            _server = server;
            _connectionGeneration = connectionGeneration;
        }

        public bool IsListening { get; private set; }

        public void Start()
        {
            var ipAddress = ResolveHost(_host);
            _cts = new CancellationTokenSource();
            _listener = new TcpListener(ipAddress, _port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _listener.Start();
            IsListening = true;
            _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void Stop(ushort? closeCode = null, string closeReason = null)
        {
            IsListening = false;

            try
            {
                _cts?.Cancel();
                _listener?.Stop();
            }
            catch (Exception ex)
            {
                McpLogger.LogWarning($"Error stopping local WebSocket listener: {ex.Message}");
            }

            CloseAllClients(closeCode, closeReason ?? "Server stopping");
        }

        public void CloseAllClients(ushort? closeCode, string closeReason)
        {
            foreach (var client in _clients.Values)
            {
                client.Close(closeCode, closeReason);
            }

            _clients.Clear();
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    _ = Task.Run(() => HandleClientAsync(tcpClient, token));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException) when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        McpLogger.LogError($"Local WebSocket accept failed: {ex.Message}");
                    }
                }
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken token)
        {
            ClientConnection client = null;

            try
            {
                tcpClient.NoDelay = true;
                var stream = tcpClient.GetStream();
                var headers = await ReadHeadersAsync(stream, token);

                if (!headers.TryGetValue("sec-websocket-key", out var key) || string.IsNullOrWhiteSpace(key))
                {
                    tcpClient.Close();
                    return;
                }

                await WriteHandshakeAsync(stream, key, token);

                var id = Guid.NewGuid().ToString("N");
                var clientName = headers.TryGetValue("x-client-name", out var name) ? name : string.Empty;
                client = new ClientConnection(id, clientName, tcpClient, stream);

                if (!_server.ShouldTrackClient(_connectionGeneration))
                {
                    client.Close(1001, "Server is restarting");
                    return;
                }

                _clients[id] = client;
                _server.Clients[id] = clientName;
                McpLogger.LogInfo($"WebSocket client connected (ID: {id}, Name: {(string.IsNullOrEmpty(clientName) ? "Unknown" : clientName)}, Total clients: {_server.Clients.Count})");

                while (!token.IsCancellationRequested && tcpClient.Connected)
                {
                    var frame = await ReadFrameAsync(stream, token);
                    if (frame == null || frame.Opcode == 0x8)
                    {
                        break;
                    }

                    if (frame.Opcode == 0x9)
                    {
                        client.SendControl(0xA, frame.Payload);
                        continue;
                    }

                    if (frame.Opcode != 0x1)
                    {
                        continue;
                    }

                    var message = Encoding.UTF8.GetString(frame.Payload);
                    _server.HandleWebSocketMessage(message, client.SendText);
                }
            }
            catch (IOException ex)
            {
                if (IsExpectedClientDisconnect(ex, token))
                {
                    McpLogger.LogInfo($"Local WebSocket client disconnected: {ex.Message}");
                }
                else
                {
                    McpLogger.LogWarning($"Local WebSocket IO closed: {ex.Message}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                McpLogger.LogError($"Local WebSocket client error: {ex.Message}");
            }
            finally
            {
                if (client != null)
                {
                    _clients.TryRemove(client.Id, out _);
                    _server.Clients.TryRemove(client.Id, out var clientName);
                    client.Close(null, null);
                    McpLogger.LogInfo($"WebSocket client '{clientName}' disconnected. (Remaining clients: {_server.Clients.Count})");
                }
                else
                {
                    tcpClient.Close();
                }
            }
        }

        private static bool IsExpectedClientDisconnect(IOException ex, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return true;
            }

            if (ex.InnerException is SocketException socketException)
            {
                return socketException.SocketErrorCode == SocketError.OperationAborted
                    || socketException.SocketErrorCode == SocketError.ConnectionAborted
                    || socketException.SocketErrorCode == SocketError.ConnectionReset
                    || socketException.SocketErrorCode == SocketError.Shutdown;
            }

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("forcibly closed", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("operation was aborted", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("Операция ввода/вывода была прервана", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IPAddress ResolveHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host) || host == "0.0.0.0")
            {
                return IPAddress.Any;
            }

            if (IPAddress.TryParse(host, out var address))
            {
                return address;
            }

            foreach (var candidate in Dns.GetHostAddresses(host))
            {
                if (candidate.AddressFamily == AddressFamily.InterNetwork)
                {
                    return candidate;
                }
            }

            return IPAddress.Loopback;
        }

        private static async Task<Dictionary<string, string>> ReadHeadersAsync(NetworkStream stream, CancellationToken token)
        {
            var buffer = new List<byte>(1024);
            var one = new byte[1];

            while (buffer.Count < MaxHeaderBytes)
            {
                var read = await stream.ReadAsync(one, 0, 1, token);
                if (read == 0)
                {
                    throw new IOException("Client closed before WebSocket handshake completed.");
                }

                buffer.Add(one[0]);
                var count = buffer.Count;
                if (count >= 4 &&
                    buffer[count - 4] == '\r' &&
                    buffer[count - 3] == '\n' &&
                    buffer[count - 2] == '\r' &&
                    buffer[count - 1] == '\n')
                {
                    break;
                }
            }

            var headerText = Encoding.ASCII.GetString(buffer.ToArray());
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);

            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                var colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                headers[line.Substring(0, colon).Trim()] = line.Substring(colon + 1).Trim();
            }

            return headers;
        }

        private static async Task WriteHandshakeAsync(NetworkStream stream, string key, CancellationToken token)
        {
            string accept;
            using (var sha1 = SHA1.Create())
            {
                var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key.Trim() + WebSocketGuid));
                accept = Convert.ToBase64String(hash);
            }

            var response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {accept}\r\n" +
                "\r\n";

            var bytes = Encoding.ASCII.GetBytes(response);
            await stream.WriteAsync(bytes, 0, bytes.Length, token);
        }

        private static async Task<WebSocketFrame> ReadFrameAsync(NetworkStream stream, CancellationToken token)
        {
            var header = await ReadExactOrNullAsync(stream, 2, token);
            if (header == null)
            {
                return null;
            }

            var opcode = (byte)(header[0] & 0x0F);
            var masked = (header[1] & 0x80) != 0;
            ulong length = (ulong)(header[1] & 0x7F);

            if (length == 126)
            {
                var extended = await ReadExactOrNullAsync(stream, 2, token);
                if (extended == null) return null;
                length = ((ulong)extended[0] << 8) | extended[1];
            }
            else if (length == 127)
            {
                var extended = await ReadExactOrNullAsync(stream, 8, token);
                if (extended == null) return null;
                length = 0;
                for (var i = 0; i < 8; i++)
                {
                    length = (length << 8) | extended[i];
                }
            }

            if (length > MaxPayloadBytes)
            {
                throw new IOException($"WebSocket payload too large: {length} bytes.");
            }

            var mask = masked ? await ReadExactOrNullAsync(stream, 4, token) : null;
            var payload = await ReadExactOrNullAsync(stream, (int)length, token) ?? Array.Empty<byte>();

            if (masked && mask != null)
            {
                for (var i = 0; i < payload.Length; i++)
                {
                    payload[i] = (byte)(payload[i] ^ mask[i % 4]);
                }
            }

            return new WebSocketFrame(opcode, payload);
        }

        private static async Task<byte[]> ReadExactOrNullAsync(NetworkStream stream, int count, CancellationToken token)
        {
            var buffer = new byte[count];
            var offset = 0;

            while (offset < count)
            {
                var read = await stream.ReadAsync(buffer, offset, count - offset, token);
                if (read == 0)
                {
                    return null;
                }

                offset += read;
            }

            return buffer;
        }

        private static byte[] BuildFrame(byte opcode, byte[] payload)
        {
            payload = payload ?? Array.Empty<byte>();
            using (var stream = new MemoryStream())
            {
                stream.WriteByte((byte)(0x80 | opcode));

                if (payload.Length < 126)
                {
                    stream.WriteByte((byte)payload.Length);
                }
                else if (payload.Length <= ushort.MaxValue)
                {
                    stream.WriteByte(126);
                    stream.WriteByte((byte)((payload.Length >> 8) & 0xFF));
                    stream.WriteByte((byte)(payload.Length & 0xFF));
                }
                else
                {
                    stream.WriteByte(127);
                    var length = (ulong)payload.Length;
                    for (var i = 7; i >= 0; i--)
                    {
                        stream.WriteByte((byte)((length >> (8 * i)) & 0xFF));
                    }
                }

                stream.Write(payload, 0, payload.Length);
                return stream.ToArray();
            }
        }

        private sealed class ClientConnection
        {
            private readonly object _writeLock = new object();
            private readonly TcpClient _tcpClient;
            private readonly NetworkStream _stream;

            public ClientConnection(string id, string name, TcpClient tcpClient, NetworkStream stream)
            {
                Id = id;
                Name = name;
                _tcpClient = tcpClient;
                _stream = stream;
            }

            public string Id { get; }
            public string Name { get; }

            public void SendText(string message)
            {
                SendFrame(0x1, Encoding.UTF8.GetBytes(message ?? string.Empty));
            }

            public void SendControl(byte opcode, byte[] payload)
            {
                SendFrame(opcode, payload ?? Array.Empty<byte>());
            }

            public void Close(ushort? closeCode, string reason)
            {
                try
                {
                    if (closeCode.HasValue)
                    {
                        var reasonBytes = Encoding.UTF8.GetBytes(reason ?? string.Empty);
                        var payload = new byte[2 + reasonBytes.Length];
                        payload[0] = (byte)((closeCode.Value >> 8) & 0xFF);
                        payload[1] = (byte)(closeCode.Value & 0xFF);
                        Buffer.BlockCopy(reasonBytes, 0, payload, 2, reasonBytes.Length);
                        SendFrame(0x8, payload);
                    }
                }
                catch
                {
                }

                try
                {
                    _tcpClient.Close();
                }
                catch
                {
                }
            }

            private void SendFrame(byte opcode, byte[] payload)
            {
                var frame = BuildFrame(opcode, payload);
                lock (_writeLock)
                {
                    _stream.Write(frame, 0, frame.Length);
                    _stream.Flush();
                }
            }
        }

        private sealed class WebSocketFrame
        {
            public WebSocketFrame(byte opcode, byte[] payload)
            {
                Opcode = opcode;
                Payload = payload;
            }

            public byte Opcode { get; }
            public byte[] Payload { get; }
        }
    }
}
