using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Multiplayer.Foundation.Protocol
{
    public static class FrameCodec
    {
        public const byte Version = 4;
        public const int MinimumBodyLength = 6;
        public const int MaximumBodyLength = 65536;

        public static async Task<WireMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            byte[] header = new byte[4];
            if (!await ReadExactlyAsync(stream, header, true, cancellationToken).ConfigureAwait(false))
                return null;
            uint length = ReadUInt32(header, 0);
            if (length < MinimumBodyLength || length > MaximumBodyLength)
                throw new ProtocolException("Invalid frame length.");
            byte[] body = new byte[(int)length];
            await ReadExactlyAsync(stream, body, false, cancellationToken).ConfigureAwait(false);
            if (body[0] != Version)
                throw new ProtocolException("Unsupported protocol version.");
            MessageType type = (MessageType)body[1];
            if (!Enum.IsDefined(typeof(MessageType), type))
                throw new ProtocolException("Unknown message type.");
            uint requestId = ReadUInt32(body, 2);
            byte[] payload = new byte[body.Length - MinimumBodyLength];
            Buffer.BlockCopy(body, MinimumBodyLength, payload, 0, payload.Length);
            return new WireMessage(type, requestId, payload);
        }

        public static async Task WriteAsync(Stream stream, WireMessage message, CancellationToken cancellationToken = default)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (message == null) throw new ArgumentNullException(nameof(message));
            int length = checked(MinimumBodyLength + message.Payload.Length);
            if (length > MaximumBodyLength)
                throw new ProtocolException("Frame exceeds maximum length.");
            byte[] frame = new byte[4 + length];
            WriteUInt32(frame, 0, (uint)length);
            frame[4] = Version;
            frame[5] = (byte)message.Type;
            WriteUInt32(frame, 6, message.RequestId);
            Buffer.BlockCopy(message.Payload, 0, frame, 10, message.Payload.Length);
            await stream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, bool allowCleanEof, CancellationToken cancellationToken)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = await stream.ReadAsync(buffer, offset, buffer.Length - offset, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    if (allowCleanEof && offset == 0) return false;
                    throw new EndOfStreamException("Truncated protocol frame.");
                }
                offset += read;
            }
            return true;
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)(value >> 24);
            data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8);
            data[offset + 3] = (byte)value;
        }
    }
}
