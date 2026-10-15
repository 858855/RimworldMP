using System;

namespace Multiplayer.Foundation.Protocol
{
    public sealed class WireMessage
    {
        public WireMessage(MessageType type, uint requestId, byte[] payload)
        {
            if (!Enum.IsDefined(typeof(MessageType), type))
                throw new ArgumentOutOfRangeException(nameof(type));
            Type = type;
            RequestId = requestId;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public MessageType Type { get; }
        public uint RequestId { get; }
        public byte[] Payload { get; }
    }
}
