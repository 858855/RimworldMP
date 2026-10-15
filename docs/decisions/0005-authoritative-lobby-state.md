# ADR 0005: Autoritativer Lobbyzustand

- **ID/Status:** 0005, implementierter Domänenkern; Serverintegration ausstehend.
- **Ziel/Klassen:** `LobbyRegistry` besitzt Mitglieder und Räume; `LobbyPayloads` kodiert Zustandslisten. Keine RimWorld-Defs oder Gameplay-Klassen.
- **Zustand/Persistenz:** Nur flüchtige Server-Sitzungen. Jede Lobby hat GUID, Host, geordnete Mitglieder, Kapazität und steigende Revision. Kein Savegame und keine Wiederaufnahme nach Serverneustart in 0.1.
- **UI/Netzwerk-Sync:** Clients senden Create/Join/Leave/Ready/Close als Anfragen. Nur Servermethoden ändern den Zustand; Snapshots mit Revision gehen an Betroffene. UI zeigt sie an und verändert keinen RimWorld-Weltzustand.
- **Konfliktbereich:** `LobbyRegistry` schützt alle Zustandsänderungen mit einem Lock. Ein Mitglied darf nur einer Lobby angehören. Bei Host-Austritt wird das älteste verbleibende Mitglied Host; leere Räume werden entfernt. Nur Host darf schließen.
- **Performance/Sicherheit:** Maximal 128 Räume, bis zu konfigurierte 64 Spieler pro Raum. Name 1–40 druckbare Zeichen. Die Server-Schicht begrenzt Clients und Nachrichten; ohne Authentifizierung nur LAN-Test.
- **Tests/Rollback:** Regeltest für Kapazität, Rechte, Zustandsrevision, Leave/Disconnect und Payload-Roundtrip. Echte Zwei-Client-TCP-Integration folgt. Änderungen liegen isoliert auf `feat/dedicated-server` und können vor Merge zurückgenommen werden.
