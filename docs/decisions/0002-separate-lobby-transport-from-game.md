# ADR 0002: Lobby-Transport vom RimWorld-Spiel trennen

- **ID/Status:** 0002, vorgeschlagen für menschliches Review.
- **Problem:** Ein Lobby-Server muss unter Linux ohne Game-Runtime laufen, während der Client an RimWorld 1.6 gebunden ist.
- **Entscheidung:** Protocol, Network und Lobby erhalten keine Verse-/Unity-/Waylith-Referenz. Der RimWorld-Adapter ist ein äußerer Client. Der Server ist autoritativ nur für Lobbyzustand, nicht für Gameplay.
- **Gründe:** Eigenständiger Build und TCP-Tests sind vor dem Game-Adapter möglich; Runtime-Versionen bleiben getrennt.
- **Alternativen:** Netzwerk direkt im Mod (schwer headless testbar); bestehendes `rwmt/Multiplayer` verwenden (anderes Gameplay-Sync-Produktziel und zusätzliche Abhängigkeiten).
- **Folgen/Komponenten:** Separate Schnittstellen und Protokollversion nötig; Authentifizierung/TLS bleiben vor öffentlichem Betrieb zu lösen. Betrifft Protocol, Network, Lobby, Server und RimWorld-Adapter.
