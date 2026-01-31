using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;

namespace MandalaLogics.Encoding
{
    public static class SocketExtensions
    {
        /// <summary>
        /// Attempts to receive exactly <paramref name="count"/> bytes into <paramref name="buffer"/>.
        /// Returns the number of bytes received (may be less if the peer closed).
        /// Throws TimeoutException if the overall timeout expires before enough bytes arrive.
        /// </summary>
        public static int ReceiveExactly(this Socket socket, byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            if (socket == null) throw new ArgumentNullException(nameof(socket));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();
            if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));

            var sw = Stopwatch.StartNew();
            int total = 0;

            while (total < count)
            {
                var remaining = timeout - sw.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException("Timed out while receiving from socket.");

                int microseconds = remaining == Timeout.InfiniteTimeSpan
                    ? -1
                    : (int)Math.Min(int.MaxValue, remaining.TotalMilliseconds * 1000.0);

                if (!socket.Poll(microseconds, SelectMode.SelectRead))
                    throw new TimeoutException("Timed out while receiving from socket.");

                int received = socket.Receive(buffer, offset + total, count - total, SocketFlags.None);

                if (received == 0)
                    break;

                total += received;
            }

            return total;
        }

        public static byte ReciveByte(this Socket socket)
        {
            var b = new byte[1];

            socket.ReceiveExactly(b, 0, b.Length, TimeSpan.MaxValue);

            return b[0];
        }

        public static int ReciveInt32(this Socket socket)
        {
            var b = new byte[sizeof(int)];

            socket.ReceiveExactly(b, 0, b.Length, TimeSpan.MaxValue);

            var ret = new int[1];

            Buffer.BlockCopy(b, 0, ret, 0, b.Length);

            return ret[0];
        }
    }
}

