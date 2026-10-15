using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-005: the starting game travels as a normal vanilla save (GameDataSaveLoader), gzipped. Host and guests
    /// each keep it in their save folder, so the session can be resumed from there later (plan §7.8).
    /// </summary>
    internal static class GameCodec
    {
        // Larger than any start save; the transfer itself is capped at 256 MiB compressed.
        private const long MaximumSaveBytes = 512L * 1024 * 1024;

        /// <summary>One file per start (with date and time), so an earlier session's save is never overwritten.</summary>
        internal static string FileNameFor(string lobbyName)
        {
            string safe = new string(lobbyName.Select(c => Path.GetInvalidFileNameChars().Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim();
            return "Multiplayer - " + (safe.Length == 0 ? "Lobby" : safe) + " - " +
                DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", System.Globalization.CultureInfo.InvariantCulture);
        }

        internal static string PathFor(string fileName) => GenFilePaths.FilePathForSavedGame(fileName);

        /// <summary>The save once vanilla has written it (newer than <paramref name="sinceUtc"/>), otherwise null.</summary>
        internal static byte[]? ReadFinishedSave(string fileName, DateTime sinceUtc)
        {
            string path = PathFor(fileName);
            try
            {
                if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < sinceUtc.AddSeconds(-2)) return null;
                return File.ReadAllBytes(path);
            }
            catch (IOException) { return null; } // still being written
        }

        internal static byte[] Gzip(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionLevel.Optimal, true)) zip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        internal static byte[] Gunzip(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                int read;
                while ((read = zip.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    if (output.Length > MaximumSaveBytes) throw new InvalidDataException("Save is too large.");
                }
                return output.ToArray();
            }
        }
    }

    /// <summary>
    /// SETUP-005: SHA-256 over the save file and what it turns into after loading: game tick, maps (ID, tile, size),
    /// landed colonists (thing ID, full name) and the session's players and pawn owners. Host: its live game right
    /// after saving. Guest: the game it loaded from the received file. Only discrete values (compare ADR 0020).
    /// </summary>
    internal static class GameFingerprint
    {
        internal static (byte[] Fingerprint, string[] Lines) Compute(byte[] saveFile, MultiplayerSession session)
        {
            var lines = new List<string>();
            using (SHA256 sha = SHA256.Create())
                lines.Add("save " + string.Concat(sha.ComputeHash(saveFile).Select(b => b.ToString("x2"))));
            lines.Add("tick " + Find.TickManager.TicksGame);
            foreach (Map map in Find.Maps.OrderBy(m => m.uniqueID))
                lines.Add("map " + map.uniqueID + " " + map.Tile.tileId + "/" + map.Tile.Layer.LayerID + " " + map.Size.x + "x" + map.Size.z);
            foreach (Pawn pawn in Colonists().OrderBy(p => p.thingIDNumber))
                lines.Add("pawn " + pawn.thingIDNumber + " " + (pawn.Name?.ToStringFull ?? ""));
            lines.Add("session " + session.SessionId);
            foreach (var (key, name) in session.Players.OrderBy(p => p.Key, StringComparer.Ordinal)) lines.Add("player " + key + " " + name);
            foreach (var (pawnId, owner) in session.Owners.OrderBy(o => o.PawnId)) lines.Add("owner " + pawnId + " " + owner);
            using (SHA256 sha = SHA256.Create())
                return (sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))), lines.ToArray());
        }

        internal static List<Pawn> Colonists() => Find.Maps.SelectMany(m => m.mapPawns.FreeColonists).ToList();

        /// <summary>The fingerprint lines for Player.log, without player keys (Steam IDs).</summary>
        internal static string LogSummary(string[] lines) =>
            string.Join("; ", lines.Where(l => !l.StartsWith("player ") && !l.StartsWith("owner "))
                .Select(l => l.StartsWith("save ") ? l.Substring(0, Math.Min(l.Length, 17)) : l)) +
            "; players " + lines.Count(l => l.StartsWith("player ")) + "; owners " + lines.Count(l => l.StartsWith("owner "));

        internal static string Short(byte[] fingerprint) => WorldFingerprint.Short(fingerprint);
    }
}
