# Verifikation – Multiplayer-Spielstand aus der Lobby fortsetzen (SAVE-002 Coop, ADR 0027)

- **Datum:** 2026-10-17
- **Branch:** `feat/continue-save` (Worktree `E:\RimworldMP-continue`), von `main` @ `060d2be`; Code-Commit `733830c`, Doku-Commit `ab4c7a1`, Selbsttest und Halte-Regel im Folgecommit (siehe `git log`).
- **Umgebung:** Windows 11 Pro; RimWorld `1.6.4871` (`Version.txt` rev590) unter `E:/SteamLibrary/steamapps/common/RimWorld`; .NET SDK 8.0.403 (Tests im lokalen .NET-8-Harness wie in `2026-10-17-integration-sync.md`); Lib.Harmony 2.4.3; Python 3.11 mit PyYAML.
- **Designnotiz:** ADR 0027.

## Ausgeführte Prüfungen (nach dem Code-Commit `733830c`)

| Befehl (aus `E:\RimworldMP-continue`) | Ergebnis |
|---|---|
| `RIMWORLD_MANAGED=/e/SteamLibrary/steamapps/common/RimWorld/RimWorldWin64_Data/Managed DOTNET_BIN=dotnet bash scripts/build-mod.sh` | Exit 0; `0 Warnung(en)`, `0 Fehler`; `Installable mod: /e/RimworldMP-continue/dist/RimWorldMultiplayerFoundation` |
| `dotnet run --project <harness>/Protocol/Protocol.csproj -c Release --nologo` | Exit 0; 20 PASS, `PASS: 19 protocol test groups`; neu: `SessionRestore` hin und zurück, Grenzen (0, 65, doppelter Schlüssel, abgeschnitten), Coop-Zustand mit `Restored`, Byte nach dem Modus, Code 105 gültig, 106 reserviert |
| `dotnet run --project <harness>/Network/Network.csproj -c Release --nologo` | Exit 0; 12 PASS |
| `dotnet run --project <harness>/Lobby/Lobby.csproj -c Release --nologo` | Exit 0; 13 PASS, neu: `PASS: continue a saved Coop game: host restores the player list, only its players join, start goes to the game transfer, Factions refuse the list, abort forgets it (ADR 0027)` |
| `dotnet run --project <harness>/Session/Session.csproj -c Release --nologo` | Exit 0; 16 PASS |
| `dotnet run --project <harness>/Hosting/Hosting.csproj -c Release --nologo` (zweimal) | Exit 0 / Exit 0; 31 PASS, neu: `PASS: continue a saved Coop game end to end: host-only restore over 105, only the save's players join, start to the game transfer, late join, Factions refuse the list, allowed via ListenHost (ADR 0027)` |
| `dotnet run --project <harness>/Client/Client.csproj -c Release --nologo` | Exit 0; 3 PASS |
| `dotnet run --project <harness>/Integration/Integration.csproj -c Release --nologo` | Exit 0; 18 PASS |
| `dotnet run --project <harness>/ProcessIntegration/ProcessIntegration.csproj -c Release --nologo` | Exit 0; 7 PASS |
| `dotnet build <harness>/Server/Server.csproj -c Release --nologo` | Exit 0; 0 Fehler |
| `python -I scripts/validate_project_state.py`; `git diff --check main...HEAD` | PASS (22 Pflichtdateien, 69 Aufgaben); leer |

Während der Entwicklung behoben: Protocol-Test „nothing after the mode“ an das neue `Restored`-Byte angepasst; der Prefix-Vergleich Coop/Fraktionen um dieses Byte verschoben; die Reservierungsprüfung für Code 105 auf 106 verlegt; `SetupAbort` in einer fortgesetzten Lobby erlaubt (vorher „No setup is running“); `LobbyMode?`-Zugriff in `RebindLobby`.

## Was die Tests belegen

- Nur der Host, nur Coop, nur Phase `Lobby`, Host in der Liste; ein Fraktions-Lobby lehnt 105 ab (6).
- Nach `RestoreSession`: `Restored` im Zustand, Bereit gelöscht, keine Fraktionsdaten; Beitritt nur für Schlüssel aus dem Spielstand (6 sonst), Verlassen vor dem Start behält den Platz; `SetupAdvance(Scenario)` abgelehnt, `Generating` erst, wenn jedes Mitglied zum Spielstand gehört und alle bereit sind; `Restored` ist ab `Generating` nicht mehr gesetzt; nach `Playing` tritt ein abwesender Spieler des Spielstands bei; `SetupAbort` vergisst den Spielstand, danach darf jeder beitreten.
- Ende zu Ende über `FoundationServer` und über `ListenHost` (der Hosting-Pfad der Mod): Wiederherstellen, Beitritt, Bereit, Start nach `Generating`, Spielstandübertragung mit `TransferGameAsync`, später Beitritt.
- Der Lobbyzustand kodiert `Restored` für Coop und Fraktionen; bestehende Zustände sind bis zum Modus byte-gleich, danach folgt ein Byte.

## Selbsttest im Spiel (`-mpContinueSelfTest`, Benutzerwunsch 2026-10-17)

Installation wie bei `2026-10-17-pawn-control.md` (Sicherung `ModBackups/RimWorldMultiplayerFoundation-2026-10-17-before-continue`, Ordner komplett ersetzt). Befehl: `RimWorldWin64.exe -mpWorldSelfTest -mpContinueSelfTest -mpWorldSelfTestCoverage=0.3 -mpQuitAfterSelfTest -screen-fullscreen 0 -screen-width 1280 -screen-height 720` (rev591, Core + 5 DLCs + Foundation). Ablauf nach dem Spielteil (`ContinueSelfTest.cs`): Hauptmenü → lokaler Host über `ListenHost` (127.0.0.1, Allein-Start, eigener `PlayerKey`, Identität, Pins, Schlüsselbeweis) → Lobby da → `LoadSavedMultiplayerGame("Multiplayer - Selbsttest Host")` → Spiel geladen, Lobby besteht, `Restored` → Bereit → `SetupAdvance(Generating)` → Host-Pfad → `Playing` → Prüfungen, danach Trennen und Aufräumen der dabei geschriebenen Spielstände.

| Lauf | Stand | Ergebnis |
|---|---|---|
| 1 (04:48) | ohne Halte-Regel | `game self-test: FAIL`: `continueLobby=ok continueLobbyKept=ok continueRestored=ok continuePlaying=ok continueSession=ok continueOwners=ok continueDriver=ok`, aber `continueTick=FAIL continueHeld=FAIL`: das geladene Spiel lief drei Ticks, bevor der Host „fortsetzen“ drückte (`Spielstand gesendet … tick 3`). 0 Exceptions. |
| 2 (04:51) | mit `ContinuingSave` (geladenes Spiel bleibt pausiert, bis `Generating`) | **`game self-test: PASS`** mit allen Welt-, Figuren- und Spielprüfungen und `continueLobby=ok continueLobbyKept=ok continueRestored=ok continuePlaying=ok continueSession=ok continueOwners=ok continueTick=ok continueDriver=ok continueHeld=ok`; `Spielstand gesendet … tick 0; … players 2; owners 2`. Prozess nach 53 s beendet, 0 Exceptions, kein Steam-Fehler. |

Belegt damit im Spiel: Spielstand laden bei bestehender Lobby-Verbindung (Szenenwechsel), Restore über 105 im geladenen Spiel, Start direkt nach `Generating`, Host-Pfad `RebindLobby`/`PruneOwners`/Speichern/Neuladen/Fingerabdruck, Landung am gespeicherten Tick, Tick-Treiber. Nicht belegt: die Spielstand-Auswahl als Klick im Fenster (der Test ruft dieselbe Methode), der Gast-Empfang (nur Hosting-Tests), Fortsetzen mit zwei echten Clients.

## Nicht geprüft (Laufzeit)

- Akzeptanz SAVE-002 mit zwei Clients: speichern, beenden, fortsetzen, gleicher Fingerabdruck, Besitz intakt; Gäste laden den Spielstand aus dem Hauptmenü.
- Bedienung der Spielstand-Auswahl im Fenster; Fenster im geladenen Spiel.

## Einordnung nach AGENTS.md Regel 25

`BUILD_ERFOLGREICH` (0 Warnungen); .NET-Tests im .NET-8-Harness bestanden (Koordinator-Seite); `IN_GAME_GETESTET` (Selbsttest, ein Prozess, lokaler Host über Loopback, Host-Seite); `MP_ZWEI_CLIENTS_GETESTET`: nein.
