using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>
    /// SETUP-001: reads the host's open vanilla setup page without changing it, for the guests' setup view.
    /// Private page fields are read by name; their names and types are checked in references/verified-apis.md.
    /// </summary>
    internal static class SetupMirror
    {
        private static readonly AccessTools.FieldRef<Page_SelectScenario, Scenario> CurScen =
            AccessTools.FieldRefAccess<Page_SelectScenario, Scenario>("curScen");
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, StorytellerDef> Storyteller =
            AccessTools.FieldRefAccess<Page_SelectStoryteller, StorytellerDef>("storyteller");
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, DifficultyDef> DifficultyChoice =
            AccessTools.FieldRefAccess<Page_SelectStoryteller, DifficultyDef>("difficulty");
        private static readonly AccessTools.FieldRef<Page_SelectStoryteller, Difficulty> DifficultyValues =
            AccessTools.FieldRefAccess<Page_SelectStoryteller, Difficulty>("difficultyValues");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, string> Seed =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, string>("seedString");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, float> Coverage =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, float>("planetCoverage");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, OverallRainfall> Rainfall =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, OverallRainfall>("rainfall");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, OverallTemperature> Temperature =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, OverallTemperature>("temperature");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, OverallPopulation> Population =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, OverallPopulation>("population");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, LandmarkDensity> Landmarks =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, LandmarkDensity>("landmarkDensity");
        private static readonly AccessTools.FieldRef<Page_CreateWorldParams, List<FactionDef>> Factions =
            AccessTools.FieldRefAccess<Page_CreateWorldParams, List<FactionDef>>("factions");
        private static readonly AccessTools.FieldRef<Page_ChooseIdeoPreset, IdeoPresetDef> SelectedIdeo =
            AccessTools.FieldRefAccess<Page_ChooseIdeoPreset, IdeoPresetDef>("selectedIdeo");
        private static readonly AccessTools.FieldRef<Page_ChooseIdeoPreset, MemeDef> SelectedStructure =
            AccessTools.FieldRefAccess<Page_ChooseIdeoPreset, MemeDef>("selectedStructure");

        /// <summary>Top-most open setup page, or null when the host has left the setup pages.</summary>
        internal static Page? CurrentPage() => Find.WindowStack.Windows.OfType<Page>().LastOrDefault();

        /// <summary>Only the host's vanilla storyteller page determines the shared difficulty.</summary>
        internal static DifficultyDef? SelectedDifficulty(Page_SelectStoryteller page) => DifficultyChoice(page);

        /// <summary>Setup phase the page belongs to (plan §6.1).</summary>
        internal static SetupPhase? PhaseOf(Page page)
        {
            switch (page)
            {
                case Page_SelectScenario _: case Page_ScenarioEditor _: return SetupPhase.Scenario;
                case Page_SelectStoryteller _: return SetupPhase.Storyteller;
                case Page_CreateWorldParams _: return SetupPhase.WorldParams;
                case Page_SelectStartingSite _: return SetupPhase.SiteSelection;
                case Page_ChooseIdeoPreset _: case Page_ConfigureFluidIdeo _: case Page_ConfigureIdeo _: return SetupPhase.Ideology;
                case Page_ConfigureStartingPawns _: return SetupPhase.Characters;
                default: return null;
            }
        }

        internal static SetupSettings Read(Page page)
        {
            var entries = new List<(string, string)>();
            void Add(string label, string? value) => entries.Add((label, Clip(value ?? "–")));
            try
            {
                switch (page)
                {
                    case Page_SelectScenario scenarioPage:
                        Scenario? scenario = CurScen(scenarioPage);
                        Add("Szenario", scenario?.name);
                        Add("Kurzbeschreibung", scenario?.summary);
                        return new SetupSettings("Szenario", entries);
                    case Page_ScenarioEditor _:
                        Add("Szenario", "wird im Szenario-Editor bearbeitet");
                        return new SetupSettings("Szenario", entries);
                    case Page_SelectStoryteller storytellerPage:
                        Add("Erzähler", Storyteller(storytellerPage)?.LabelCap.ToString());
                        Add("Schwierigkeit", DifficultyChoice(storytellerPage)?.LabelCap.ToString());
                        Difficulty? values = DifficultyValues(storytellerPage);
                        if (values != null) Add("Bedrohungen", Percent(values.threatScale));
                        if (Find.GameInitData != null)
                            Add("Speichermodus", Find.GameInitData.permadeathChosen
                                ? (Find.GameInitData.permadeath ? "Commitment-Modus" : "Jederzeit laden") : "noch nicht gewählt");
                        return new SetupSettings("Erzähler und Schwierigkeit", entries);
                    case Page_CreateWorldParams worldPage:
                        Add("Seed", Seed(worldPage));
                        Add("Planetenabdeckung", Percent(Coverage(worldPage)));
                        Add("Niederschlag", Rainfall(worldPage).ToString());
                        Add("Temperatur", Temperature(worldPage).ToString());
                        Add("Bevölkerung", Population(worldPage).ToString());
                        Add("Wahrzeichen", Landmarks(worldPage).ToString());
                        Add("Verschmutzung", Percent(worldPage.pollution));
                        List<FactionDef>? factions = Factions(worldPage);
                        if (factions != null)
                            Add("Fraktionen (" + factions.Count + ")", string.Join(", ", factions.Select(f => f.LabelCap.ToString())));
                        return new SetupSettings("Welt erstellen", entries);
                    case Page_SelectStartingSite _:
                        PlanetTile tile = Find.WorldSelector.SelectedTile;
                        Add("Ausgewähltes Feld", tile.Valid
                            ? Find.WorldGrid[tile].PrimaryBiome?.LabelCap.ToString() + " (Feld " + tile.tileId + ")" : "keins");
                        Add("Hinweis", "Startort-Fähnchen der Mitspieler folgen mit SETUP-003.");
                        return new SetupSettings("Startort", entries);
                    case Page_ChooseIdeoPreset ideoPage:
                        Add("Ideoligion", SelectedIdeo(ideoPage)?.LabelCap.ToString() ?? "keine Vorlage gewählt");
                        Add("Struktur", SelectedStructure(ideoPage)?.LabelCap.ToString());
                        return new SetupSettings("Ideoligion", entries);
                    case Page_ConfigureFluidIdeo _: // subclass of Page_ConfigureIdeo, so it comes first
                        Add("Ideoligion", "eigene, fließende Ideoligion");
                        return new SetupSettings("Ideoligion", entries);
                    case Page_ConfigureIdeo configure:
                        Add("Ideoligion", configure.ideo?.name ?? "wird angepasst");
                        return new SetupSettings("Ideoligion", entries);
                    case Page_ConfigureStartingPawns _:
                        GameInitData? init = Find.GameInitData;
                        if (init != null)
                            Add("Figuren (" + init.startingPawnCount + ")", string.Join(", ", init.startingAndOptionalPawns
                                .Take(init.startingPawnCount).Select(p => p.Name?.ToStringShort ?? "?")));
                        Add("Hinweis", "Eigene Figurenwahl je Spieler folgt mit SETUP-004.");
                        return new SetupSettings("Figuren", entries);
                }
            }
            catch (Exception error)
            {
                // Never break the host's page for the mirror; report what could not be read.
                Log.Warning("Multiplayer setup mirror: " + error.Message);
                entries.Add(("Hinweis", "Diese Seite konnte nicht gelesen werden."));
            }
            return new SetupSettings(page.GetType().Name, entries);
        }

        private static string Percent(float value) => Math.Round(value * 100f).ToString(CultureInfo.InvariantCulture) + " %";

        private static string Clip(string value)
        {
            const int limit = 120;
            value = value.Replace('\n', ' ').Replace('\r', ' ');
            return value.Length <= limit ? value : value.Substring(0, limit - 1) + "…";
        }
    }
}
