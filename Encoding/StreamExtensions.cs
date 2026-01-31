using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;

namespace MandalaLogics.Encoding
{
    public static class StreamExtensions
    {
        public static int ReadExactly(this Stream stream, byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException();

            var stopwatch = Stopwatch.StartNew();
            int totalRead = 0;

            while (totalRead < count)
            {
                if (stopwatch.Elapsed > timeout)
                    throw new TimeoutException("Timed out while reading from stream.");

                int read = stream.Read(
                    buffer,
                    offset + totalRead,
                    count - totalRead);

                if (read == 0)
                    break; // EOF

                totalRead += read;
            }

            return totalRead;
        }

        /// <summary>
        /// Reads an int from a binary stream in little endian.
        /// </summary>
        /// <exception cref="EndOfStreamException"></exception>
        public static int ReadInt32(this Stream stream)
        {
            var bytes = new byte[sizeof(int)];

            if (stream.ReadExactly(bytes, 0, bytes.Length, TimeSpan.MaxValue) != sizeof(int))
            {
                throw new EndOfStreamException();
            }

            return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        }
    }
}

