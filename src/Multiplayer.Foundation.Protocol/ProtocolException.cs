using System.IO;

namespace Multiplayer.Foundation.Protocol
{
    public sealed class ProtocolException : IOException
    {
        public ProtocolException(string message) : base(message) { }
    }
}
