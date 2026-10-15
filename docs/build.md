# Build-Umgebung

**Ist-Zustand 2026-10-14:** RimWorld 1.6.4871 rev598 ist lokal installiert; `Assembly-CSharp.dll` liegt in `RimWorldLinux_Data/Managed`. Kein .NET-SDK, Mono oder MSBuild im PATH; es wurden noch keine produktiven C#-Builds ausgeführt. Ein .NET-Installationsskript wurde nur nach `/tmp/dotnet-install-rimworldmp.sh` geladen.

Geplant: Spielunabhängige Bibliotheken/Server mit lokalem .NET-SDK bauen; RimWorld-Adapter getrennt gegen rechtmäßig lokal vorhandene 1.6-Referenzen kompilieren. Proprietäre Assemblies bleiben außerhalb von Git. Konkrete Befehle und Ausgaben nach Einführung der Projekte ergänzen.
