using System;
using System.Security.Cryptography;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Network
{
    /// <summary>
    /// A player's RSA key pair (ADR 0035). Created once and stored by the game; proves the PlayerKey on IP connections.
    /// </summary>
    public sealed class PlayerIdentity
    {
        private readonly RSAParameters key;

        private PlayerIdentity(RSAParameters key) { this.key = key; }

        public byte[] Modulus => key.Modulus!;
        public byte[] Exponent => key.Exponent!;

        public static PlayerIdentity Create()
        {
            RSA rsa = RSA.Create();
            try
            {
                // On Mono RSA.Create() gives 1024 bits and setting KeySize afterwards does not change an
                // RSACryptoServiceProvider's key, so ask for 2048 bits explicitly there.
                if (rsa.KeySize < 2048) { rsa.Dispose(); rsa = new RSACryptoServiceProvider(2048); }
                return new PlayerIdentity(rsa.ExportParameters(true));
            }
            finally { rsa.Dispose(); }
        }

        public HelloProof Prove(string playerName, PlayerKey playerKey, long unixSeconds)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportParameters(key);
                byte[] nonce = new byte[HelloProof.NonceBytes];
                using (var random = RandomNumberGenerator.Create()) random.GetBytes(nonce);
                byte[] signature = rsa.SignData(HelloProof.SignedBytes(playerName, playerKey, unixSeconds, nonce), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                return new HelloProof(Modulus, Exponent, unixSeconds, nonce, signature);
            }
        }

        public static long UnixNow => (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        /// <summary>Private key material for the game to store locally. Never send this anywhere.</summary>
        public byte[] ToBytes()
        {
            var writer = new PayloadWriter();
            foreach (byte[]? part in new[] { key.Modulus, key.Exponent, key.D, key.P, key.Q, key.DP, key.DQ, key.InverseQ })
                writer.WriteBytes(part ?? throw new InvalidOperationException("Incomplete private key."), 1024);
            return writer.ToArray();
        }

        public static PlayerIdentity FromBytes(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            var key = new RSAParameters
            {
                Modulus = reader.ReadBytes(1024), Exponent = reader.ReadBytes(1024), D = reader.ReadBytes(1024), P = reader.ReadBytes(1024),
                Q = reader.ReadBytes(1024), DP = reader.ReadBytes(1024), DQ = reader.ReadBytes(1024), InverseQ = reader.ReadBytes(1024)
            };
            reader.EnsureComplete();
            return new PlayerIdentity(key);
        }
    }
}
