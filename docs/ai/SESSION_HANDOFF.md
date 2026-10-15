# Sitzungsübergabe

- **Bereich:** `NET-002` Verbindung über das Internet.
- **Branch:** `fix/internet-connectivity` von `main` (`5b533cd`); lokal committet, nicht gepusht, kein Merge.
- **Stand:** Direkt-Host und Server binden standardmäßig dual-stack an `::`. Der Client löst Namen auf, nutzt IPv6 und probiert mehrere Adressen. Die Mod öffnet optional per UPnP den Router-Port, zeigt Internet-/LAN-/VPN-/IPv6-Adressen samt Kopierknopf und erkennt CGNAT. Das ZIP unter `releases/` ist neu gebaut. Begründung: ADR 0010.
- **Prüfungen:** Mod-Build gegen Windows-RimWorld 1.6.4871 rev590 ohne Warnungen; alle sieben Testprojekte bestanden, wegen fehlendem SDK unter .NET 8 statt 10; SSDP gegen eine echte FRITZ!Box nur lesend. Details: `docs/verification/2026-10-15-internet-reachability.md`.
- **Offen:** Echte Portfreigabe und Gastverbindung aus fremdem Netz, Spiel-UI/Mono-Laufzeit, Tests unter .NET 10/Linux. Keine Authentifizierung/TLS.
- **Nächster Schritt:** ZIP installieren, Lobby mit UPnP hosten, Freigabe in der FRITZ!Box prüfen (ggf. „Selbstständige Portfreigaben erlauben“), Gast über die angezeigte Internet-Adresse verbinden. Danach Review `docs/reviews/2026-10-15-internet-reachability.md` und Merge-Freigabe.
- **Dateien/Befehle:** `src/Multiplayer.Foundation.Hosting/UpnpPortMapping.cs`, `NetworkAddresses.cs`, `FoundationServer.cs`, `src/Multiplayer.Foundation.Network/NetworkConnection.cs`, `HostEndpoint.cs`, `src/Multiplayer.Foundation.RimWorld/FoundationMod.cs`; `dotnet run --project tests/Hosting.Tests/Hosting.Tests.csproj -c Release --nologo`.
