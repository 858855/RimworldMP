// Beispiel für die Foundation-API v1 (ADR 0029, docs/extension-api.md). NICHT GEBAUT: Vorlage für einen Fremd-Mod.
// About.xml: modDependencies + loadAfter rimworldmp.foundation; Verweis auf Assemblies/Multiplayer.Foundation.Api.dll.
using Multiplayer.Foundation.Api;
using RimWorld;
using UnityEngine;
using Verse;

namespace FoundationApiExample
{
    [StaticConstructorOnStartup]
    public static class Registrierung
    {
        static Registrierung()
        {
            // Worker zuerst, dann Hash-Beiträge, dann Sync-Methoden dieses Assemblys.
            if (MP.IsAvailable) MP.RegisterAll();
        }
    }

    /// <summary>Eine Komponente mit synchronisiertem Zustand; der Schalter im Gizmo ruft die Sync-Methode.</summary>
    public sealed class CompMaschine : ThingComp
    {
        public bool aktiv;
        public int stufe;

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref aktiv, "aktiv");
            Scribe_Values.Look(ref stufe, "stufe");
        }

        public override System.Collections.Generic.IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Action
            {
                defaultLabel = aktiv ? "Maschine aus" : "Maschine an",
                // Der Klick ändert nichts selbst: er ruft die Sync-Methode, die auf jedem Client am selben Tick läuft.
                action = () => MaschinenBefehle.SetzeAktiv(this, !aktiv),
            };
        }

        // Synchronisierter Zustand, der in keinem Thing-Feld sichtbar ist, gehört in die 60-Tick-Hash-Prüfung.
        [SyncHash]
        public static ulong Hash()
        {
            ulong hash = 0;
            foreach (Map map in Find.Maps)
                foreach (Thing thing in map.listerThings.AllThings)
                    if (thing is ThingWithComps withComps && withComps.GetComp<CompMaschine>() is CompMaschine comp)
                        hash = hash * 1099511628211UL ^ (ulong)(uint)thing.thingIDNumber ^ (comp.aktiv ? 1UL : 0UL) ^ (ulong)(uint)comp.stufe << 8;
            return hash;
        }
    }

    public static class MaschinenBefehle
    {
        // Instanz (ThingComp) und Argument sind eingebaute Typen; kein Worker nötig.
        [SyncMethod]
        public static void SetzeAktiv(CompMaschine comp, bool aktiv)
        {
            comp.aktiv = aktiv;
        }

        // Eigener Typ als Argument: braucht den Worker unten.
        [SyncMethod]
        public static void Stufe(Auswahl auswahl)
        {
            if (auswahl.Comp == null) return;
            // Nur der Besitzer der Figur darf (ADR 0026): die Foundation liefert den Aussteller, die Mod entscheidet.
            if (MP.ExecutingIssuer != null && auswahl.Verantwortlich != MP.ExecutingIssuer) return;
            auswahl.Comp.stufe = Mathf.Clamp(auswahl.Stufe, 0, 3);
        }
    }

    public sealed class Auswahl
    {
        public CompMaschine? Comp;
        public int Stufe;
        public string? Verantwortlich;

        [SyncWorker]
        public static void Arbeite(SyncWorker sync, ref Auswahl wert)
        {
            if (sync.IsWriting)
            {
                sync.Write(wert.Comp);
                sync.Write(wert.Stufe);
                sync.Write(wert.Verantwortlich);
            }
            else wert = new Auswahl { Comp = sync.Read<CompMaschine?>(), Stufe = sync.Read<int>(), Verantwortlich = sync.Read<string?>() };
        }
    }
}
