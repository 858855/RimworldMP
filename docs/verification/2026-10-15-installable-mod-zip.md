# Installierbares RimWorld-1.6-Mod-ZIP, 15.10.2026

- Branch: `release/mod-install-0.1-dev`; Basis `fix/server-heartbeat` bei `1192eb5`. GitHub-Upload wird nach dem lokalen Commit separat geprüft.
- Ziel: RimWorld 1.6.4871, lokale `Version.txt` rev598; frühere Spiel-Runtime meldete rev600. .NET SDK 10.0.402, Client-Ziel `netstandard2.1`.
- Build-Befehl: `DOTNET_BIN=/home/fabiok/.local/share/dotnet-rimworldmp/dotnet bash scripts/build-mod.sh`. Exit 0; 0 Warnungen, 0 Fehler. Build referenziert lokale RimWorld-Assemblies, verteilt sie aber nicht.
- Paket-Befehl: `python3 scripts/package-mod.py`. Exit 0; `releases/RimWorldMultiplayerFoundation-0.1.0-dev-rw1.6.zip`, 24.182 Bytes, fünf Dateien.
- SHA-256: `1cf1feecff75a055b2a1d963a9ce4acb67d1d02e8b75167034b864bc09a7e86b`.
- Inhalt: `RimWorldMultiplayerFoundation/About/About.xml`, `Assemblies/Multiplayer.Foundation.Protocol.dll`, `Assemblies/Multiplayer.Foundation.Network.dll`, `Assemblies/Multiplayer.Foundation.RimWorld.dll`, `INSTALLATION.md`. Keine proprietären Spiel-DLLs.
- Extraktionstest: Python `zipfile` nach temporärem `RimWorld/Mods/`, XML-`packageId` geprüft, genau drei Assemblies und Anleitung vorhanden. Exit 0.
- Grenze: Kein RimWorld-Spielstart, kein Mod-Ladeeintrag, keine UI- oder Netzwerkaktion aus dem Spiel für dieses ZIP ausgeführt. Keine 1.7-Freigabe und keine Kompatibilitätszusage mit anderen Mods.
