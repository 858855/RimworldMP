# Codebeispiele — Evidenz und Teststatus

**NICHT PRODUKTIONSFERTIG.** Alle nachfolgenden Quellen sind Beispielvorlagen, nicht in einer RimWorld-Installation kompiliert oder laufzeitgetestet. Die fehlenden Game-DLLs dürfen nicht in dieses Repository kopiert werden. Versionsumfang und Quellen für die Konzepte:

| Verzeichnis | Ziel | Build-/Teststatus | Version |
|---|---|---|---|
| `minimal-mod` | About.xml, XML-Def, Mod-Entrypoint, Settings | XML Syntax automatisiert prüfbar; C# nicht gebaut | Modell `1.6/net472` |
| `xml-patches` | XPath PatchOperationAdd | XML Syntax prüfbar; XPath-Ziel absichtlich Demonstration | 1.6/1.7 Konzept |
| `harmony` | Prefix/Postfix eines beobachteten Game-Hooks | C# nicht gebaut; keine RimWorld-Instanz | 1.6 Beispiel, Harmony2 |
| `components` | Game/Map/ThingComp und Scribe | C# nicht gebaut, API signatures in lokaler DLL prüfen | 1.6-Konzept |
| `multiplayer` | `[SyncMethod]`, API Detection, SyncField/Watch, SyncWorker | C# nicht gebaut; API-Wiki historisch | 1.6 mit `RimWorld.MultiplayerAPI 0.6.0`; keine 1.7-Unterstützung |

Der Code ist didaktisch und enthält einzelne absichtliche Platzhalter (insbesondere nicht vorhandene DefNames/Texturen). Vor realem Einsatz fehlende Properties/Defs, Ref-Packs und UI-Kontexte gegen die eigene Runtime ermitteln. Quellen stehen in `references/verified-apis.md`.
