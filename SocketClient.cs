using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CSharp_ImGui_Client
{
    public sealed class SocketClient : IDisposable
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private readonly object _lock = new();
        private CancellationTokenSource? _pollCts;

        public bool Connected
        {
            get { lock (_lock) { return _client != null && _client.Connected; } }
        }

        public event EventHandler<bool>? OnConnectionChanged;
        public event EventHandler<MatchData>? OnMatchDataReceived;
        public event EventHandler<PlayerData[]>? OnPlayersReceived;
        public event EventHandler<string>? OnDebugMessage;

        public async Task<bool> Connect(string ip, int port)
        {
            return await Task.Run(() =>
            {
                lock (_lock)
                {
                    try
                    {
                        Disconnect_Locked();
                        _client = new TcpClient();
                        _client.NoDelay = true;
                        _client.SendTimeout = 10000;
                        _client.ReceiveTimeout = 60000;

                        var result = _client.BeginConnect(ip, port, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                        if (!success)
                        {
                            try { _client.Close(); } catch { }
                            _client = null;
                            return false;
                        }
                        _client.EndConnect(result);
                        _stream = _client.GetStream();
                        OnConnectionChanged?.Invoke(this, true);
                        return true;
                    }
                    catch
                    {
                        try { _client?.Close(); } catch { }
                        _client = null;
                        _stream = null;
                        return false;
                    }
                }
            });
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                Disconnect_Locked();
            }
            OnConnectionChanged?.Invoke(this, false);
        }

        private void Disconnect_Locked()
        {
            try { _stream?.Close(); } catch { }
            _stream = null;
            try { _client?.Close(); } catch { }
            _client = null;
        }

        public void Send(Request req)
        {
            lock (_lock)
            {
                if (_client == null || !_client.Connected || _stream == null)
                    return;

                try
                {
                    byte[] payload = StructConverter.StructToBytes(req);
                    int size = payload.Length;
                    byte[] lenPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(size));
                    _stream.Write(lenPrefix, 0, 4);
                    _stream.Write(payload, 0, size);
                    _stream.Flush();

                    byte[] resLenBuf = new byte[4];
                    if (ReadExact_Locked(resLenBuf, 4) != 4)
                    {
                        Disconnect_Locked();
                        OnConnectionChanged?.Invoke(this, false);
                        return;
                    }

                    int resSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(resLenBuf, 0));
                    if (resSize <= 0 || resSize > 500000) return;

                    byte[] resPayload = new byte[resSize];
                    ReadExact_Locked(resPayload, resSize);
                }
                catch
                {
                    try { Disconnect_Locked(); } catch { }
                    OnConnectionChanged?.Invoke(this, false);
                }
            }
        }

        public async Task<Response?> SendPoll(int mode, int screenWidth = 0, int screenHeight = 0)
        {
            return await Task.Run<Response?>(() =>
            {
                lock (_lock)
                {
                    if (_client == null || !_client.Connected || _stream == null)
                        return null;

                    try
                    {
                        var req = new Request { Mode = mode, ScreenWidth = screenWidth, ScreenHeight = screenHeight };
                        byte[] payload = StructConverter.StructToBytes(req);
                        int size = payload.Length;
                        byte[] lenPrefix = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(size));
                        _stream.Write(lenPrefix, 0, 4);
                        _stream.Write(payload, 0, size);
                        _stream.Flush();

                        byte[] resLenBuf = new byte[4];
                        if (ReadExact_Locked(resLenBuf, 4) != 4)
                        {
                            Disconnect_Locked();
                            OnConnectionChanged?.Invoke(this, false);
                            return null;
                        }

                        int resSize = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(resLenBuf, 0));
                        int expectedSize = Marshal.SizeOf<Response>();
                        if (resSize <= 0 || resSize > 500000)
                        {
                            Disconnect_Locked();
                            OnConnectionChanged?.Invoke(this, false);
                            return null;
                        }

                        byte[] resPayload = new byte[resSize];
                        if (ReadExact_Locked(resPayload, resSize) != resSize)
                        {
                            Disconnect_Locked();
                            OnConnectionChanged?.Invoke(this, false);
                            return null;
                        }

                        byte[] structBuf = new byte[expectedSize];
                        Buffer.BlockCopy(resPayload, 0, structBuf, 0, Math.Min(resSize, expectedSize));
                        var res = StructConverter.BytesToStruct<Response>(structBuf);

                        var match = new MatchData
                        {
                            MatchState = res.MatchAlive == 1 ? "In Game" : "Lobby",
                            RemainingTime = res.RemainingTimeSeconds.ToString(),
                            PlayerCount = res.PlayerCount
                        };
                        OnMatchDataReceived?.Invoke(this, match);

                        var players = new PlayerData[res.PlayerCount];
                        Array.Copy(res.Players, players, res.PlayerCount);
                        OnPlayersReceived?.Invoke(this, players);

                        return res;
                    }
                    catch
                    {
                        try { Disconnect_Locked(); } catch { }
                        OnConnectionChanged?.Invoke(this, false);
                        return null;
                    }
                }
            });
        }

        public void StartPolling()
        {
            _pollCts?.Cancel();
            _pollCts = new CancellationTokenSource();
            var ct = _pollCts.Token;

            Task.Run(async () =>
            {
                int tickCounter = 0;
                while (!ct.IsCancellationRequested)
                {
                    if (!Connected) break;

                    int pollMode;
                    if (tickCounter % 4 == 0)
                    {
                        pollMode = 1000;
                        tickCounter = 0;
                    }
                    else
                    {
                        pollMode = 1001;
                    }
                    tickCounter++;

                    try
                    {
                        await SendPoll(pollMode);
                    }
                    catch { }

                    try
                    {
                        await Task.Delay(1000, ct);
                    }
                    catch (OperationCanceledException) { break; }
                }
            }, ct);
        }

        public void StopPolling()
        {
            _pollCts?.Cancel();
            _pollCts = null;
        }

        public void Log(string msg)
        {
            OnDebugMessage?.Invoke(this, msg);
        }

        private int ReadExact_Locked(byte[] buffer, int bytesToRead)
        {
            if (_stream == null) return 0;
            int total = 0;
            while (total < bytesToRead)
            {
                int read = _stream.Read(buffer, total, bytesToRead - total);
                if (read <= 0) break;
                total += read;
            }
            return total;
        }

        public void Dispose()
        {
            StopPolling();
            Disconnect();
        }
    }
}
