using System;

namespace FlvInteractiveRebase.Flv
{
    internal static class Extensions
    {
        internal static byte[] ToBE(this byte[] b)
        {
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(b);
            }
            return b;
        }
    }
}
