using System;
using System.IO;
using System.Threading;

namespace FlvInteractiveRebase.Flv
{
    internal static class StreamCopier
    {
        private const int BUFFER_SIZE = 4 * 1024;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        private static readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        public static int SkipBytes(this Stream stream, int length)
        {
            if (length < 0) { throw new ArgumentOutOfRangeException("length must be non-negative"); }
            if (length == 0) { return 0; }
            if (null == stream) { throw new ArgumentNullException(nameof(stream)); }
            if (!stream.CanRead) { throw new ArgumentException("cannot read stream", nameof(stream)); }

            if (stream.CanSeek)
            {
                if (stream.Length - stream.Position >= length)
                {
                    stream.Position += length;
                    return length;
                }
                return 0;
            }

            try
            {
                int total = 0;
                semaphoreSlim.Wait();

                while (length > BUFFER_SIZE)
                {
                    var read = stream.Read(buffer, 0, BUFFER_SIZE);
                    total += read;
                    if (read != BUFFER_SIZE) { return total; }
                    length -= BUFFER_SIZE;
                }

                total += stream.Read(buffer, 0, length);
                return total;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public static bool CopyBytes(this Stream from, Stream to, int length)
        {
            if (length < 0) { throw new ArgumentOutOfRangeException("length must be non-negative"); }
            if (length == 0) { return true; }
            if (null == from) { throw new ArgumentNullException(nameof(from)); }
            if (null == to) { throw new ArgumentNullException(nameof(to)); }
            if (!from.CanRead) { throw new ArgumentException("cannot read stream", nameof(from)); }
            if (!to.CanWrite) { throw new ArgumentException("cannot write stream", nameof(to)); }

            try
            {
                semaphoreSlim.Wait();

                while (length > BUFFER_SIZE)
                {
                    if (BUFFER_SIZE != from.Read(buffer, 0, BUFFER_SIZE))
                    {
                        return false;
                    }
                    to.Write(buffer, 0, BUFFER_SIZE);
                    length -= BUFFER_SIZE;
                }

                if (length != from.Read(buffer, 0, length))
                {
                    return false;
                }
                to.Write(buffer, 0, length);

                return true;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }
    }
}
