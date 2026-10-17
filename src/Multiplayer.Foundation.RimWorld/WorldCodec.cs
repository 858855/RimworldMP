using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.Profile;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-002: moves the host's generated world (no maps yet) to the guests. The host saves it with
    /// RimWorld's own Scribe, the guest loads it into a fresh setup game in the main menu, as the starting-site
    /// page does after world generation. Main thread only.
    /// </summary>
    internal static class WorldCodec
    {
        private static string Folder => Path.Combine(GenFilePaths.SaveDataFolderPath, "MultiplayerFoundation");

        /// <summary>
        /// Rounds the live tile data to what a save keeps (RimWorld's own StandardizeTileData), so the host's world
        /// equals the one the guests load. Without it e.g. swampiness differs by 0.01 after loading (self-test,
        /// 2026-10-15). Repeating it changes nothing.
        /// </summary>
        internal static void Standardize(World world) => world.grid.StandardizeTileData();

        /// <summary>Host: Scribe-saves the world and returns it gzipped. Call <see cref="Standardize"/> first.</summary>
        internal static byte[] Save(World world)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "world-out.xml");
            World saved = world;
            Scribe.saver.InitSaving(path, "mpWorld");
            try { Scribe_Deep.Look(ref saved, "world"); }
            catch { Scribe.saver.ForceStop(); throw; }
            Scribe.saver.FinalizeSaving();
            return Gzip(File.ReadAllBytes(path));
        }

        /// <summary>Guest: replaces any earlier setup world with the host's and finishes its initialization.</summary>
        internal static World Load(byte[] gzipped, string? scenarioName)
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, "world-in.xml");
            File.WriteAllBytes(path, Gunzip(gzipped));
            if (Current.Game?.World != null) MemoryUtility.ClearAllMapsAndWorld();
            if (Current.Game == null)
            {
                Current.Game = new Game { InitData = new GameInitData() };
                Current.Game.Scenario = FindScenario(scenarioName);
            }
            // The world must be the current world while it loads: loading the grid reads Find.World (planet coverage).
            // Scribe_Deep would create it too late, so create it here and register it for the later passes like Scribe_Deep does.
            var world = new World();
            Current.Game.World = world;
            Scribe.loader.InitLoading(path);
            Loading = true;
            try
            {
                if (!Scribe.EnterNode("world")) throw new InvalidDataException("The transfer contains no world.");
                try { world.ExposeData(); }
                finally { Scribe.ExitNode(); }
                Scribe.loader.crossRefs.RegisterForCrossRefResolve(world);
                Scribe.loader.initer.RegisterForPostLoadInit(world);
                Scribe.loader.FinalizeLoading();
            }
            catch
            {
                Scribe.ForceStop();
                if (Current.Game != null) Current.Game.World = null;
                throw;
            }
            finally { Loading = false; }
            world.FinalizeInit(true);
            Standardize(world);
            return world;
        }

        /// <summary>True while <see cref="Load"/> reads a world (see <see cref="SetupWorldIdeoErrorPatch"/>).</summary>
        internal static bool Loading { get; private set; }

        /// <summary>
        /// Drops a guest's setup world (abort, or before the received game loads), so the main menu is clean again.
        /// ClearAllMapsAndWorld empties the world but leaves it current, so Find.World still passes null checks while its
        /// parts are gone and drawing threw (guest log of the first two-player run, 2026-10-16). The setup game goes with it.
        /// </summary>
        internal static void Discard()
        {
            if (Current.ProgramState != ProgramState.Entry || Current.Game == null) return;
            MemoryUtility.ClearAllMapsAndWorld();
            Current.Game = null;
        }

        private static Scenario FindScenario(string? name)
        {
            Scenario? match = name == null ? null : ScenarioLister.AllScenarios().FirstOrDefault(s => s.name == name);
            return match ?? ScenarioDefOf.Crashlanded.scenario;
        }

        private static byte[] Gzip(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionLevel.Optimal, true)) zip.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        private static byte[] Gunzip(byte[] data)
        {
            using (var input = new MemoryStream(data))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                zip.CopyTo(output);
                return output.ToArray();
            }
        }
    }

    /// <summary>
    /// SETUP-002: SHA-256 over the discrete parts of a world, in a fixed order: seed, surface tiles (biome, hills,
    /// river and road counts), factions and settlements. The transfer's SHA-256 already proves the bytes are equal;
    /// this proves the guest's game turns them into the same world (same biome, faction and world object defs).
    /// Continuous tile values are left out: the save stores them more coarsely than the live world, so swampiness
    /// and elevation drift at rounding edges on load even after StandardizeTileData (self-test, 295 732 tiles, 2026-10-15).
    /// </summary>
    internal static class WorldFingerprint
    {
        internal static byte[] Compute(World world)
        {
            var builder = new FingerprintBuilder();
            foreach (var (name, hash) in Parts(world)) builder.Add(name).Add(System.Convert.ToBase64String(hash));
            return builder.Finish();
        }

        /// <summary>Separate hashes per area, so a mismatch report can say what differs.</summary>
        internal static (string Name, byte[] Hash)[] Parts(World world)
        {
            var info = new FingerprintBuilder().Add(world.info.seedString).Add(world.info.planetCoverage, 3);
            PlanetLayer surface = world.grid.Surface;
            var tiles = new FingerprintBuilder().Add(surface.TilesCount);
            for (int i = 0; i < surface.TilesCount; i++) tiles.Add(TileLine(surface[i]));
            var factions = new FingerprintBuilder();
            foreach (Faction faction in world.factionManager.AllFactionsListForReading.OrderBy(f => f.loadID))
                factions.Add(faction.loadID).Add(faction.def?.defName).Add(faction.Name);
            var settlements = new FingerprintBuilder();
            foreach (Settlement settlement in world.worldObjects.Settlements.OrderBy(s => s.Tile.tileId).ThenBy(s => s.Name))
                settlements.Add(settlement.Tile.tileId).Add(settlement.Faction?.loadID ?? -1).Add(settlement.Name);
            return new[] { ("Seed", info.Finish()), ("Felder", tiles.Finish()), ("Fraktionen", factions.Finish()),
                ("Siedlungen", settlements.Finish()) };
        }

        /// <summary>Canonical text of one surface tile; also used to find the first differing tile.</summary>
        internal static string TileLine(Tile tile)
        {
            string line = (tile.PrimaryBiome?.defName ?? "-") + "|" + (int)tile.hilliness;
            if (tile is SurfaceTile surfaceTile) line += "|" + (surfaceTile.Rivers?.Count ?? 0) + "|" + (surfaceTile.Roads?.Count ?? 0);
            return line;
        }

        internal static string Short(byte[] fingerprint) => string.Concat(fingerprint.Take(3).Select(b => b.ToString("x2")));
    }

    /// <summary>
    /// SETUP-002, guest: the host's world travels before the ideoligion page, so the player faction has none yet. Loading
    /// it, vanilla gives the guest's copy a random one and logs an error (guest log, 2026-10-16). The copy only serves the
    /// site page and is discarded before the game loads, so that one error is expected here and logged as a note.
    /// </summary>
    [HarmonyPatch(typeof(Log), nameof(Log.Error), typeof(string))]
    internal static class SetupWorldIdeoErrorPatch
    {
        private static bool Prefix(string text)
        {
            if (!WorldCodec.Loading || text == null || !text.StartsWith("Faction had no ideoligions after loading")) return true;
            Log.Message("Multiplayer Foundation: setup world loaded before the ideoligion page; vanilla gave this copy's faction a placeholder.");
            return false;
        }
    }
}
