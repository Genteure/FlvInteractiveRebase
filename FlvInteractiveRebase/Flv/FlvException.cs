using System;
using System.Runtime.Serialization;

namespace FlvInteractiveRebase.Flv
{
    internal class FlvException : Exception
    {
        public FlvException()
        {
        }

        public FlvException(string? message) : base(message)
        {
        }

        public FlvException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected FlvException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
