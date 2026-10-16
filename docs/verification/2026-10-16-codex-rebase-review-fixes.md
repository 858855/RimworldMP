# Codex-Spielseite nach C1–C3-Merge – Prüfung 2026-10-16

- **Branch/Basis:** `codex/resync-game`, `git fetch origin` und `git rebase origin/main` auf `8f7c32f`, ohne Konflikte. Prüfumgebung: Linux, lokale RimWorld 1.6.4871 rev598, .NET SDK 10.0.402.
- **Quellcodeänderung:** Erstes Hashfenster nach Join/Resync immer auslassen (`(Boundary / 60 + 1) * 60`). Save-Befehle einzeln an ihrer Sequenzposition abarbeiten; verbleibende Grenzbefehle bis zum Ende des Saves zurückhalten. Spielbefehle und `VoteResult` bleiben ohne Ausführer gesperrt.
- **API-Nachweis:** Reflection und IL gegen lokale `Assembly-CSharp.dll`: `Verse.SummaryHealthHandler` hat `cachedSummaryHealthPercent` und `dirty`; Getter liefert den Cache. Deshalb aus dem Hash entfernt. `Faction.GoodwillWith` wird für dieselbe Fraktion nicht mehr aufgerufen.

| Ausgeführt | Ergebnis |
|---|---|
| `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh` | Erster Lauf: Exit 1, fehlendes `System.Linq` nach Bearbeitung. Nach Korrektur: Exit 0, 0 Warnungen, 0 Fehler. |
| `dotnet run --project tests/Session.Tests/Session.Tests.csproj -c Release --nologo` | Exit 0; Lockstep-, Save/Join-, Resync- und Vote-Simulationen bestanden. |
| `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo` | Exit 0; Netzwerk- und Host-Integrationstests bestanden. |
| `dotnet run --project tests/Client.Tests/Client.Tests.csproj -c Release --nologo` | Exit 0; drei Client-Gruppen bestanden. |
| `python3 scripts/validate_project_state.py` | Exit 0; 22 Pflichtdateien, 61 Aufgaben und Referenzen gültig. |

**Grenze der Prüfung:** Kein laufender RimWorld-Zwei-Client-Test und kein ausgeführter Spielbefehl. Die Save-Reihenfolge ist im Adapter implementiert und gebaut, aber erst mit `SYNC-002` und einem Spieltest funktional nachzuweisen. `VoteResult` wird bis zur Spielseiten-UI bewusst angehalten.
