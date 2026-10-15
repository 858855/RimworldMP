using System;
using System.Linq;
using System.Threading.Tasks;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    // MOD-001: reads the running mod list on the main thread, hashes the files on a worker thread.
    internal static class ModManifestBuilder
    {
        internal static Task<ModManifest> StartAsync()
        {
            string game = VersionControl.CurrentVersionStringWithRev;
            var packs = LoadedModManager.RunningModsListForReading.Select(pack => (
                Id: pack.PackageId,
                Name: pack.Name ?? "",
                Version: pack.ModMetaData?.ModVersion ?? "",
                Root: pack.RootDir,
                Folders: pack.foldersToLoadDescendingOrder?.ToArray() ?? Array.Empty<string>())).ToArray();
            return Task.Run(() => new ModManifest(game, packs
                .Select(pack => new ModEntry(pack.Id, pack.Name, pack.Version, ModContentHasher.Hash(pack.Root, pack.Folders)))
                .ToArray()));
        }
    }
}
