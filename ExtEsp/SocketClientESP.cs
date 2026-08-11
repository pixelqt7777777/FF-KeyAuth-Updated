using System.Net.Sockets;

namespace CSharp_ImGui_Client.ExtEsp
{
    public class SocketClientESP
    {
        private Socket _socket;

        public bool Connect(string ip, int port)
        {
            try
            {
                _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _socket.NoDelay = true;
                _socket.SendBufferSize = 65536;
                _socket.ReceiveBufferSize = 65536;
                _socket.Connect(ip, port);
                return true;
            }
            catch { return false; }
        }

        public bool ReceiveExact(byte[] buffer, int size)
        {
            int received = 0;
            try
            {
                while (received < size)
                {
                    int r = _socket.Receive(buffer, received, size - received, SocketFlags.None);
                    if (r <= 0) return false;
                    received += r;
                }
                return true;
            }
            catch { return false; }
        }

        public void Send(byte[] buffer, int size)
        {
            int sent = 0;
            while (sent < size)
            {
                sent += _socket.Send(buffer, sent, size - sent, SocketFlags.None);
            }
        }

        public int Receive(byte[] buffer, int offset, int size)
        {
            int received = 0;
            while (received < size)
            {
                int r = _socket.Receive(buffer, offset + received, size - received, SocketFlags.None);
                if (r <= 0) return 0;
                received += r;
            }
            return received;
        }

        public void Close()
        {
            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
            }
            catch { }
        }
    }
}
