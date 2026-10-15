using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Multiplayer.Foundation.Protocol
{
    /// <summary>Lobby check status of one member compared with the host (MOD-001).</summary>
    public enum ModStatus : byte
    {
        /// <summary>Neither the member nor the host sent a manifest (tools, tests). Not checked.</summary>
        Unchecked = 0,
        /// <summary>The host sent a manifest, this member not yet.</summary>
        Checking = 1,
        Match = 2,
        Mismatch = 3
    }

    public sealed class ModEntry
    {
        public ModEntry(string packageId, string name, string version, byte[] contentHash)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ProtocolException("Package ID is required.");
            if (contentHash == null || contentHash.Length != 32) throw new ProtocolException("Content hash must have 32 bytes.");
            PackageId = packageId.ToLowerInvariant(); Name = name ?? ""; Version = version ?? ""; ContentHash = contentHash;
        }
        public string PackageId { get; }
        public string Name { get; }
        public string Version { get; }
        public byte[] ContentHash { get; }
        public string Label => Name.Length > 0 ? Name + " (" + PackageId + ")" : PackageId;
    }

    /// <summary>Game build plus active mods in load order with version and content hash.</summary>
    public sealed class ModManifest
    {
        public const int MaximumMods = 4000;

        public ModManifest(string gameVersion, IReadOnlyList<ModEntry> mods)
        {
            if (mods == null || mods.Count > MaximumMods) throw new ProtocolException("Too many mods.");
            GameVersion = gameVersion ?? ""; Mods = mods;
        }
        public string GameVersion { get; }
        public IReadOnlyList<ModEntry> Mods { get; }

        public byte[] Serialize()
        {
            var writer = new PayloadWriter();
            writer.WriteString(GameVersion, 64); writer.WriteUInt16((ushort)Mods.Count);
            foreach (ModEntry mod in Mods)
            {
                writer.WriteString(mod.PackageId, 160); writer.WriteString(Truncate(mod.Name, 160), 160);
                writer.WriteString(Truncate(mod.Version, 64), 64); TransferPayloads.WriteHash(writer, mod.ContentHash);
            }
            return writer.ToArray();
        }

        public static ModManifest Deserialize(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            string game = reader.ReadString(64); ushort count = reader.ReadUInt16();
            if (count > MaximumMods) throw new ProtocolException("Too many mods.");
            var mods = new List<ModEntry>(count);
            for (int i = 0; i < count; i++)
                mods.Add(new ModEntry(reader.ReadString(160), reader.ReadString(160), reader.ReadString(64), TransferPayloads.ReadHash(reader)));
            reader.EnsureComplete();
            return new ModManifest(game, mods);
        }

        /// <summary>SHA-256 over the canonical serialization; equal hashes mean equal manifests.</summary>
        public byte[] Hash() { byte[] data = Serialize(); return TransferPayloads.Hash(data, 0, data.Length); }

        public ModManifestSummary Summary() => new ModManifestSummary(Hash(), GameVersion, (ushort)Mods.Count);

        // Cut on a character boundary so the UTF-8 form stays within the field limit.
        private static string Truncate(string value, int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;
            int length = value.Length;
            while (length > 0 && Encoding.UTF8.GetByteCount(value.Substring(0, length)) > maxBytes) length--;
            if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            return value.Substring(0, length);
        }
    }

    /// <summary>What each member tells the server: the server compares hashes and never parses mod lists.</summary>
    public sealed class ModManifestSummary
    {
        public ModManifestSummary(byte[] hash, string gameVersion, ushort modCount)
        {
            if (hash == null || hash.Length != 32) throw new ProtocolException("Manifest hash must have 32 bytes.");
            Hash = hash; GameVersion = gameVersion ?? ""; ModCount = modCount;
        }
        public byte[] Hash { get; }
        public string GameVersion { get; }
        public ushort ModCount { get; }
        public string ShortHash => string.Concat(Hash.Take(2).Select(b => b.ToString("x2")));
        public bool SameAs(ModManifestSummary? other) => other != null && Hash.SequenceEqual(other.Hash);

        public byte[] ToPayload()
        {
            var writer = new PayloadWriter(); TransferPayloads.WriteHash(writer, Hash);
            writer.WriteString(GameVersion, 64); writer.WriteUInt16(ModCount);
            return writer.ToArray();
        }
        public static ModManifestSummary Read(byte[] bytes)
        {
            var reader = new PayloadReader(bytes);
            byte[] hash = TransferPayloads.ReadHash(reader); string game = reader.ReadString(64); ushort count = reader.ReadUInt16();
            reader.EnsureComplete(); return new ModManifestSummary(hash, game, count);
        }
        internal void Write(PayloadWriter writer) { TransferPayloads.WriteHash(writer, Hash); writer.WriteString(GameVersion, 64); writer.WriteUInt16(ModCount); }
        internal static ModManifestSummary Read(PayloadReader reader)
            => new ModManifestSummary(TransferPayloads.ReadHash(reader), reader.ReadString(64), reader.ReadUInt16());
    }

    /// <summary>Differences between the local manifest and the host's, grouped for the report window.</summary>
    public sealed class ModManifestDiff
    {
        private ModManifestDiff() { }
        public string LocalGameVersion { get; private set; } = "";
        public string HostGameVersion { get; private set; } = "";
        public bool GameVersionDiffers => LocalGameVersion != HostGameVersion;
        /// <summary>Mods the host has and this player does not.</summary>
        public IReadOnlyList<ModEntry> MissingLocally { get; private set; } = Array.Empty<ModEntry>();
        /// <summary>Mods this player has and the host does not.</summary>
        public IReadOnlyList<ModEntry> ExtraLocally { get; private set; } = Array.Empty<ModEntry>();
        /// <summary>Same package ID, other version or content.</summary>
        public IReadOnlyList<(ModEntry Local, ModEntry Host)> Changed { get; private set; } = Array.Empty<(ModEntry, ModEntry)>();
        /// <summary>Position (in the list of shared mods) where the load order first differs; -1 if the order matches.</summary>
        public int FirstOrderDifference { get; private set; } = -1;
        public IReadOnlyList<string> LocalSharedOrder { get; private set; } = Array.Empty<string>();
        public IReadOnlyList<string> HostSharedOrder { get; private set; } = Array.Empty<string>();
        public bool IsMatch => !GameVersionDiffers && MissingLocally.Count == 0 && ExtraLocally.Count == 0 &&
            Changed.Count == 0 && FirstOrderDifference < 0;

        public static ModManifestDiff Compare(ModManifest local, ModManifest host)
        {
            var localIds = new HashSet<string>(local.Mods.Select(m => m.PackageId));
            var hostIds = new HashSet<string>(host.Mods.Select(m => m.PackageId));
            var hostById = host.Mods.GroupBy(m => m.PackageId).ToDictionary(g => g.Key, g => g.First());
            string[] localShared = local.Mods.Select(m => m.PackageId).Where(hostIds.Contains).ToArray();
            string[] hostShared = host.Mods.Select(m => m.PackageId).Where(localIds.Contains).ToArray();
            int first = -1;
            for (int i = 0; i < Math.Min(localShared.Length, hostShared.Length); i++)
                if (localShared[i] != hostShared[i]) { first = i; break; }
            return new ModManifestDiff
            {
                LocalGameVersion = local.GameVersion,
                HostGameVersion = host.GameVersion,
                MissingLocally = host.Mods.Where(m => !localIds.Contains(m.PackageId)).ToArray(),
                ExtraLocally = local.Mods.Where(m => !hostIds.Contains(m.PackageId)).ToArray(),
                Changed = local.Mods.Where(m => hostById.ContainsKey(m.PackageId))
                    .Select(m => (Local: m, Host: hostById[m.PackageId]))
                    .Where(p => p.Local.Version != p.Host.Version || !p.Local.ContentHash.SequenceEqual(p.Host.ContentHash))
                    .ToArray(),
                FirstOrderDifference = first,
                LocalSharedOrder = localShared,
                HostSharedOrder = hostShared
            };
        }
    }

    /// <summary>
    /// Content hash of one mod: About.xml plus Assemblies (*.dll), Defs and Patches (*.xml) in its load folders.
    /// Textures and sounds are skipped; they do not affect the simulation.
    /// </summary>
    public static class ModContentHasher
    {
        private static readonly (string Folder, string Pattern)[] Parts =
            { ("Assemblies", "*.dll"), ("Defs", "*.xml"), ("Patches", "*.xml") };

        public static byte[] Hash(string rootDir, IEnumerable<string> loadFolders)
        {
            string root = Path.GetFullPath(rootDir).TrimEnd('/', '\\');
            var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Add(string full)
            {
                string relative = full.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/').ToLowerInvariant();
                if (!files.ContainsKey(relative)) files.Add(relative, full);
            }
            string about = Path.Combine(root, "About", "About.xml");
            if (File.Exists(about)) Add(Path.GetFullPath(about));
            foreach (string folder in loadFolders.Distinct())
            {
                string baseDir = Path.GetFullPath(Path.IsPathRooted(folder) ? folder : Path.Combine(root, folder)).TrimEnd('/', '\\');
                bool inside = baseDir.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                    baseDir.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    baseDir.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                if (!inside) continue;
                foreach (var (sub, pattern) in Parts)
                {
                    string dir = Path.Combine(baseDir, sub);
                    if (!Directory.Exists(dir)) continue;
                    foreach (string file in Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)) Add(Path.GetFullPath(file));
                }
            }
            using (SHA256 outer = SHA256.Create())
            using (SHA256 inner = SHA256.Create())
            {
                foreach (var pair in files)
                {
                    byte[] name = Encoding.UTF8.GetBytes(pair.Key + "\n");
                    byte[] content;
                    using (FileStream stream = File.OpenRead(pair.Value)) content = inner.ComputeHash(stream);
                    outer.TransformBlock(name, 0, name.Length, null, 0);
                    outer.TransformBlock(content, 0, content.Length, null, 0);
                }
                outer.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return outer.Hash;
            }
        }
    }
}
