using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

namespace CSharp_ImGui_Client
{
    public static class SocketMemory
    {
        private static SocketClientESP _readClient;
        private static SocketClientESP _writeClient;
        private static int _pid;

        private const int BLOCK_SIZE = 4096;
        private const int MAX_BATCH = 64;
        private const int REQ_SIZE = 16;

        private class BlockContainer
        {
            public readonly byte[][] Buffers = { new byte[BLOCK_SIZE], new byte[BLOCK_SIZE] };
            public int FrontIndex = 0;
            public long LastAccess;
            public byte[] GetFront() => Buffers[FrontIndex];
            public byte[] GetBack() => Buffers[1 - FrontIndex];
            public void Swap() { FrontIndex = 1 - FrontIndex; }
        }

        private static readonly ConcurrentDictionary<ulong, BlockContainer> _cache = new();
        private static readonly ConcurrentDictionary<ulong, byte> _activeBlocks = new();

        private static readonly byte[] _batchSendBuf = new byte[MAX_BATCH * REQ_SIZE];
        private static readonly byte[] _batchRecvBuf = new byte[MAX_BATCH * BLOCK_SIZE];

        [ThreadStatic] private static byte[] _writeBuf;
        private static bool _running;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct MemoryRequest
        {
            public int pid;
            public uint addr;
            public int size;
            public int write;
        }

        public static void Initialize(SocketClientESP readClient, SocketClientESP writeClient, int pid)
        {
            _readClient = readClient;
            _writeClient = writeClient;
            _pid = pid;
            if (_running) return;
            _running = true;
            new Thread(UpdateLoop) { IsBackground = true, Priority = ThreadPriority.Highest }.Start();
            new Thread(EvictionLoop) { IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
        }

        public static void Stop()
        {
            _running = false;
        }

        private static unsafe void UpdateLoop()
        {
            var blockList = new List<ulong>(128);
            while (_running)
            {
                blockList.Clear();
                foreach (var k in _activeBlocks.Keys) blockList.Add(k);
                int total = blockList.Count;
                if (total == 0) { Thread.Sleep(1); continue; }
                int sent = 0;
                while (sent < total)
                {
                    int batchCount = Math.Min(MAX_BATCH, total - sent);
                    int batchReqBytes = batchCount * REQ_SIZE;
                    int batchDataBytes = batchCount * BLOCK_SIZE;
                    fixed (byte* b = _batchSendBuf)
                    {
                        MemoryRequest* reqs = (MemoryRequest*)b;
                        for (int i = 0; i < batchCount; i++)
                        {
                            reqs[i].pid = _pid;
                            reqs[i].addr = (uint)blockList[sent + i];
                            reqs[i].size = BLOCK_SIZE;
                            reqs[i].write = 0;
                        }
                    }
                    _readClient.Send(_batchSendBuf, batchReqBytes);
                    if (_readClient.ReceiveExact(_batchRecvBuf, batchDataBytes))
                    {
                        for (int i = 0; i < batchCount; i++)
                        {
                            ulong addr = blockList[sent + i];
                            var container = _cache.GetOrAdd(addr, _ => new BlockContainer());
                            Buffer.BlockCopy(_batchRecvBuf, i * BLOCK_SIZE, container.GetBack(), 0, BLOCK_SIZE);
                            container.Swap();
                        }
                    }
                    sent += batchCount;
                }
                Thread.Yield();
            }
        }

        private static void EvictionLoop()
        {
            while (_running)
            {
                Thread.Sleep(1000);
                long now = Environment.TickCount64;
                const int TTL = 170;
                foreach (var kv in _cache)
                {
                    if (now - kv.Value.LastAccess > TTL)
                    {
                        _activeBlocks.TryRemove(kv.Key, out _);
                        _cache.TryRemove(kv.Key, out _);
                    }
                }
            }
        }

        public static unsafe bool Read<T>(ulong address, out T value) where T : unmanaged
        {
            value = default;
            ulong blockBase = address & ~((ulong)BLOCK_SIZE - 1);
            int offset = (int)(address - blockBase);
            _activeBlocks[blockBase] = 1;
            if (_cache.TryGetValue(blockBase, out var container))
            {
                container.LastAccess = Environment.TickCount64;
                if (offset + sizeof(T) > BLOCK_SIZE) return false;
                fixed (byte* ptr = &container.GetFront()[offset])
                    value = *(T*)ptr;
                return true;
            }
            return false;
        }

        public static unsafe void Write<T>(ulong address, T value) where T : unmanaged
        {
            int size = sizeof(T);
            int totalSize = REQ_SIZE + size;
            if (_writeBuf == null || _writeBuf.Length < totalSize)
                _writeBuf = new byte[totalSize];
            ulong blockBase = address & ~((ulong)BLOCK_SIZE - 1);
            int offset = (int)(address - blockBase);
            if (_cache.TryGetValue(blockBase, out var container) && offset + size <= BLOCK_SIZE)
            {
                fixed (byte* f = &container.Buffers[0][offset], b = &container.Buffers[1][offset])
                {
                    *(T*)f = value;
                    *(T*)b = value;
                }
            }
            fixed (byte* b = _writeBuf)
            {
                var req = (MemoryRequest*)b;
                req->pid = _pid;
                req->addr = (uint)address;
                req->size = size;
                req->write = 1;
                *(T*)(b + REQ_SIZE) = value;
            }
            _writeClient.Send(_writeBuf, totalSize);
        }

        public static string ReadString(ulong address, int size = 32)
        {
            ulong blockBase = address & ~((ulong)BLOCK_SIZE - 1);
            int offset = (int)(address - blockBase);
            _activeBlocks[blockBase] = 1;
            if (!_cache.TryGetValue(blockBase, out var container)) return "";
            container.LastAccess = Environment.TickCount64;
            byte[] data = container.GetFront();
            int available = Math.Min(size, BLOCK_SIZE - offset);
            int len = 0;
            for (int i = 0; i < available - 1; i += 2)
            {
                if (data[offset + i] == 0 && data[offset + i + 1] == 0) break;
                len += 2;
            }
            return len > 0 ? Encoding.Unicode.GetString(data, offset, len) : "";
        }
    }
}
