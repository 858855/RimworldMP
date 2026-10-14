# 13 — Native Linux-Entwicklung und CI für RimWorld 1.6/1.7

## Grundlage

RimWorld hat eine native Linux-Distribution über Steam. Ein typischer Steam-Librarypfad lautet `~/.steam/steam/steamapps/common/RimWorld`, kann durch alternative Steam-Bibliotheken, Flatpak/Steam-Container oder Symlinks abweichen. **Pfad nicht hartkodieren.** Case-Sensitivity, fehlende Mono-Reference-Assemblies, Vulkan-Treiber und OS-spezifisches Dateiverhalten sind wichtigste Stolperfallen.

## Empfohlene Werkzeuge

`git`, `dotnet` SDK (für 1.6 kompatibler MSBuild/.NET-Framework-Targetingpack unter Linux, für 1.7 .NET 10 SDK), Rider/VS Code, ILSpyCmd bzw. ILSpy, `xmllint` oder Python `xml.etree`, `diff`, `sha256sum`, Debug-/Log-Tools. Die proprietären `RimWorld`-Assemblies legal lokal aus installierter Spielversion referenzieren.

```bash
# Read-only Systeminventur
uname -a
dotnet --info
find "$HOME" -path '*/steamapps/common/RimWorld' -type d -print 2>/dev/null
# Lokale DLL-Referenzen suchen, ohne sie zu verteilen:
find "$HOME/.steam/steam/steamapps/common/RimWorld" \
  -name 'Assembly-CSharp.dll' -o -name 'Waylith.dll' 2>/dev/null
```

Klammerung bei `find` korrekt herstellen, wenn gezielte Path-Matches erforderlich sind; siehe `codex/mod-development-workflow.md`.

## 1.6 Build

`net472` benötigt bei modernem Linux-SDK in vielen Setups explizite **.NET Framework Reference Assemblies** oder bekannte Referenzpakete (z. B. `Krafs.Rimworld.Ref`, Ziel-Build kontrollieren). Die tatsächliche Versionskombination muss zum installierten Spiel passen. Projekte in `examples/minimal-mod` sind **kein vollständig kompilierbarer Build ohne Dritt-/Spiel-DLLs**. Im Repo nur Quellen/Projektdateien; echte DLLs liegen außerhalb.

## 1.7 Build/Headless

Ludeon verlangt `net10.0` gegen installierte 1.7-Assemblies `Assembly-CSharp.dll`, `Waylith.dll`, `UnityStubs.dll` (ggf. `RimWorld.dll`). `-Headless` läuft ohne Grafikdevice und kann Load-/Asset-Tests ermöglichen; `-NoWindow` initialisiert trotzdem Rendering. Die genaue Datei der installierten Executable, Arbeitsdirectory und Linux-Aufrufsyntax **lokal prüfen**, statt einen erfundenen Binärnamen vorzugeben.

```bash
# ILLUSTRATIVER Aufruf, nur nach Lokalisierung der 1.7-Binary:
# "$RIMWORLD_17_BINARY" -Headless -mod=com.example.sample -Verbose
# Achtung: Spiel muss installiert sein; Prozessende/Log auf Erfolg prüfen.
```

## Was ohne GUI möglich ist

| Testart | Nur Shell ohne gekaufte Spielinstallation? | Mit 1.7 + installiertem Game headless? |
|---|---|---|
| XML-Wellformedness/Struktur | ja | ja |
| Git-Diff, Lizenz, Pfade, Case | ja | ja |
| C#-Build | nur mit allen legal beschafften Referenzen | ja, bei passender SDK/Assemblies |
| Def-Ladeprozess | nein | voraussichtlich ja, tatsächlichen Start verifizieren |
| Rendering/Shader/GUI-Interaktionen | nein | nicht vollständig; `-NoWindow` oder GUI nötig |
| Save-/Load-Funktion mit echtem Tick | nein | abhängig von Testharness/CLI; nicht garantiert |
| Multi-Client-Interaktion | nein | zwei laufende MP-Clients und MP-Support erforderlich |

## Cross-OS-Kriterien

Linux-Dateipfade sind case-sensitive; Windows oft nicht. In `Textures`, `Sounds`, `Languages` und Assets Case gegen tatsächliche Dateien testen. `DateTime.Now`, Culture-spezifisches Parsing/Sorting, Float/JIT-Unterschiede nicht Teil deterministischer Weltentscheidungen machen. Lokalen Pfad nicht in Spielzustand oder SyncWorker serialisieren.

## Datenschutz und rechtliche Grenzen

Game-DLLs ausschließlich als lokale Buildreferenzen nutzen; keine Weiterverbreitung, keine komprimierten Originalassets. Netzwerk-Multiplayer-Logs vor öffentlichem Bugreport auf Nutzernamen/IPs/Paths prüfen.

Quelle: https://docs.google.com/document/d/e/2PACX-1vSykw5_DYaaw42nEznI9zmBZVT_Vgg2bxYq_v2XWvAsHC3QSKFuDK2OyiQ5WC9jvVRUcrjpFt7xLLF5/pub
