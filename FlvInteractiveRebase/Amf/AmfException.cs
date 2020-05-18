using System;
using System.Runtime.Serialization;

namespace FlvInteractiveRebase.Amf
{
    internal class AmfException : Exception
    {
        public AmfException() { }
        public AmfException(string? message) : base(message) { }
        public AmfException(string? message, Exception? innerException) : base(message, innerException) { }
        protected AmfException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
