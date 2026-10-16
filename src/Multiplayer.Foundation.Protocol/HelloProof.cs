using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Multiplayer.Foundation.Protocol
{
    /// <summary>
    /// Proof that the player owns the private key for their PlayerKey on this connection (ADR 0035): public key, timestamp,
    /// random nonce and an RSA-PKCS#1/SHA-256 signature over context, PlayerKey, name, timestamp and nonce. The private
    /// key never travels. The nonce makes every Hello unique: PKCS#1 v1.5 signatures are deterministic, so without it a
    /// player reconnecting within the same second would look like a replay.
    /// </summary>
    public sealed class HelloProof
    {
        public const int MinimumModulusBytes = 256, MaximumModulusBytes = 512, MaximumExponentBytes = 8, NonceBytes = 16;
        private static readonly byte[] Context = Encoding.ASCII.GetBytes("RimworldMP/hello/v1");

        public HelloProof(byte[] modulus, byte[] exponent, long unixSeconds, byte[] nonce, byte[] signature)
        {
            Nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
            if (nonce.Length != NonceBytes) throw new ProtocolException("Nonce must be 16 bytes.");
            Modulus = modulus ?? throw new ArgumentNullException(nameof(modulus));
            Exponent = exponent ?? throw new ArgumentNullException(nameof(exponent));
            Signature = signature ?? throw new ArgumentNullException(nameof(signature));
            UnixSeconds = unixSeconds;
            if (modulus.Length < MinimumModulusBytes || modulus.Length > MaximumModulusBytes) throw new ProtocolException("RSA key must be 2048-4096 bits.");
            if (exponent.Length == 0 || exponent.Length > MaximumExponentBytes) throw new ProtocolException("Invalid RSA exponent.");
            if (signature.Length != modulus.Length) throw new ProtocolException("Signature length must match the key.");
        }

        public byte[] Modulus { get; }
        public byte[] Exponent { get; }
        public long UnixSeconds { get; }
        public byte[] Nonce { get; }
        public byte[] Signature { get; }

        /// <summary>What the player signs. Binding name and timestamp keeps a proof from being reused under another name.</summary>
        public static byte[] SignedBytes(string playerName, PlayerKey key, long unixSeconds, byte[] nonce)
        {
            var writer = new PayloadWriter();
            writer.WriteFixed(Context, Context.Length);
            key.Write(writer);
            writer.WriteString(playerName, 160);
            writer.WriteUInt64((ulong)unixSeconds);
            writer.WriteFixed(nonce, NonceBytes);
            return writer.ToArray();
        }

        /// <summary>SHA-256 over the public key; this is what the host pins for a PlayerKey.</summary>
        public byte[] Fingerprint()
        {
            var writer = new PayloadWriter();
            writer.WriteBytes(Modulus, MaximumModulusBytes); writer.WriteBytes(Exponent, MaximumExponentBytes);
            using (SHA256 sha = SHA256.Create()) return sha.ComputeHash(writer.ToArray());
        }

        public bool Verify(string playerName, PlayerKey key)
        {
            try
            {
                using (RSA rsa = RSA.Create())
                {
                    rsa.ImportParameters(new RSAParameters { Modulus = Modulus, Exponent = Exponent });
                    return rsa.VerifyData(SignedBytes(playerName, key, UnixSeconds, Nonce), Signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                }
            }
            catch (CryptographicException) { return false; }
        }

        public void Write(PayloadWriter writer)
        {
            writer.WriteBytes(Modulus, MaximumModulusBytes); writer.WriteBytes(Exponent, MaximumExponentBytes);
            writer.WriteUInt64((ulong)UnixSeconds); writer.WriteFixed(Nonce, NonceBytes); writer.WriteBytes(Signature, MaximumModulusBytes);
        }

        public static HelloProof Read(PayloadReader reader)
        {
            byte[] modulus = reader.ReadBytes(MaximumModulusBytes), exponent = reader.ReadBytes(MaximumExponentBytes);
            ulong seconds = reader.ReadUInt64();
            if (seconds > long.MaxValue) throw new ProtocolException("Invalid timestamp.");
            byte[] nonce = reader.ReadFixed(NonceBytes);
            return new HelloProof(modulus, exponent, (long)seconds, nonce, reader.ReadBytes(MaximumModulusBytes));
        }

        /// <summary>Stable text form of a fingerprint for storage.</summary>
        public static string FingerprintText(byte[] fingerprint) => string.Concat(fingerprint.Select(b => b.ToString("x2")));
    }
}
