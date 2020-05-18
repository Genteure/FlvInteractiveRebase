using System;
using System.Runtime.Serialization;

namespace FlvInteractiveRebase.Fib
{
    internal class FibException : Exception
    {
        public FibException()
        {
        }

        public FibException(string? message) : base(message)
        {
        }

        public FibException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected FibException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
