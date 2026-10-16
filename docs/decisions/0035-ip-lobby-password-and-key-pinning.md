# ADR 0035 – IP-Lobbys: Passwort und Schlüsselbindung (SEC-001)

- **Status:** angenommen für `claude/ip-password`, auf `main` @ `8f7c32f`. Designnotiz nach `AGENTS.md` Regel 4. Plan §7.12 und §10 `SEC-001` („Auth for IP lobbies: at least a lobby password; key pinning – done when a spoofed PlayerKey is rejected“). Bahn Claude, C4.
- **Problem:** Über Steam prüft der Host die SteamID gegen Steams eigene Bestätigung (ADR 0016). Über IP/TCP übernimmt er die behauptete Kennung ungeprüft. Jeder sieht die Kennungen aller Mitspieler im Lobbyzustand und könnte sich als einer von ihnen ausgeben, und damit später deren Pawns übernehmen (`OWN-001`, `SYNC-005`).

## Entscheidung

**1. Lobby-Passwort (optional):**
- Der Host setzt es beim Erstellen der Lobby (`CreateLobby`, höchstens 64 Zeichen).
- Beitreten (`JoinLobby`) verlangt dann dasselbe Passwort. Falsch oder fehlend ergibt Code 3, „Wrong lobby password.“
- Die Lobbyliste zeigt, dass ein Passwort nötig ist; das Passwort selbst sieht niemand.
- Der Vergleich läuft in konstanter Zeit. Das Passwort reist im Klartext wie alles auf IP/TCP (keine TLS, siehe unten).

**2. Schlüsselbindung (Key Pinning):**
- **Spieler-Schlüssel:** Jeder Spieler hat ein **RSA-2048-Schlüsselpaar** (`PlayerIdentity`), einmal erzeugt und von der Spielseite gespeichert. Der private Schlüssel verlässt den Rechner nie.
- **Nachweis im Hello:** öffentlicher Schlüssel, Zeitstempel (Unix-Sekunden), 16 Byte Zufallswert und Signatur (RSA-PKCS#1, SHA-256). Signiert wird über `"RimworldMP/hello/v1"`, die kanonischen Bytes des `PlayerKey`, den Spielernamen, den Zeitstempel und den Zufallswert. Ohne privaten Schlüssel ist sie nicht zu fälschen.
- **Warum der Zufallswert:** PKCS#1-v1.5-Signaturen sind deterministisch. Ohne ihn hätte ein Spieler, der sich innerhalb derselben Sekunde neu verbindet, eine bytegleiche Signatur und würde als Wiederholung abgewiesen. Der Ende-zu-Ende-Test hat das gezeigt.
- **Prüfung auf dem Host (nur TCP-Verbindungen):**
  - Der Zeitstempel muss höchstens ±10 Minuten von der Uhr des Hosts abweichen.
  - Die Signatur muss stimmen.
  - Dieselbe Signatur darf innerhalb dieses Fensters nicht zweimal vorkommen (Schutz gegen Wiederholung).
- **Bindung beim ersten Kontakt (TOFU):** Kommt eine Kennung zum ersten Mal mit Nachweis, merkt sich der Host den Fingerabdruck ihres Schlüssels (SHA-256 über Modulus und Exponent). Danach gilt für dieselbe Kennung:
  - **anderer Schlüssel:** abgelehnt (Code 3, „Player key is bound to another identity.“). Damit gilt die Abnahme: Eine gefälschte Kennung wird abgelehnt.
  - **kein Nachweis:** abgelehnt (Code 3, „This player key requires proof.“).
- **Pflicht-Modus:** Mit `HostConfig.RequireKeyProof` lehnt der Host jede TCP-Verbindung ohne Nachweis ab. Standard aus, damit bestehende Spielseiten ohne Schlüssel weiter funktionieren. Die Spielseite soll ihn für IP-Lobbys einschalten, sobald sie Schlüssel erzeugt.
- **Steam-Verbindungen** sind ausgenommen; ihre Identität bestätigt Steam (ADR 0016).
- **Speicherung der Bindungen:** `PlayerKeyPinStore` im Server, mit `Export`/`Import`. So kann die Spielseite die Bindungen dauerhaft im Spielstand oder in den Mod-Einstellungen ablegen; ein fortgesetztes Spiel behält sie.

## Grenzen (bewusst)

- **Keine Verschlüsselung (TLS):** Wer die Verbindung mitlesen kann, sieht das Passwort und könnte eine Sitzung übernehmen. Gegen Mitspieler, die nur die Kennung kennen, schützt die Bindung vollständig.
- **Wiederholung:** Ein mitgelesenes Hello wird binnen 10 Minuten wegen derselben Signatur abgelehnt. Nach einem Neustart des Hosts ist dieser Speicher leer.
- **Erster Kontakt:** Bindet ein Angreifer eine fremde Kennung, bevor ihr echter Besitzer je diesen Host besucht hat, gehört sie ihm auf diesem Host (Eigenschaft von TOFU). Steam-Verbindungen kennen dieses Problem nicht.
- **Mono-Laufzeit:** `RSA.Create`, `SignData`/`VerifyData` mit `HashAlgorithmName`/`RSASignaturePadding` und `RSACryptoServiceProvider.SignHash` (nicht abstrakt) sind in RimWorlds `RimWorldLinux_Data/Managed/mscorlib.dll` vorhanden. Geprüft per Metadaten-Lesen, denn Monos `mscorlib` lässt sich nicht in .NET laden. Die Funktion zur Laufzeit ist **WAHRSCHEINLICH**, aber nicht getestet.

## Protokoll (keine neuen Nachrichtencodes)

| Nachricht | Erweiterung, jeweils am Ende und optional |
|---|---|
| Hello (1) | Nachweis: Modulus (≤ 512 Byte), Exponent (≤ 8 Byte), Zeitstempel `u64`, Zufallswert (16 Byte), Signatur (≤ 512 Byte) |
| CreateLobby (8) | Passwort: `bool` + String (≤ 64 Zeichen) |
| JoinLobby (9) | nach der Lobby-ID optional das Passwort |
| LobbyListResponse (7) | je Lobby `bool` „Passwort nötig“ |

Ohne Erweiterung bleiben die Nachrichten wie bisher gültig.

## Abgrenzung, Tests, Rollback

- **Spielseite (Codex):**
  - `PlayerIdentity` einmal erzeugen und speichern (`ToBytes`/`FromBytes`), bei jedem Verbinden mitgeben.
  - Passwortfeld beim Erstellen und Beitreten; Schloss-Symbol in der Liste.
  - `RequireKeyProof` für IP-Lobbys einschalten.
  - Bindungen mit `PlayerKeyPinStore.Export`/`Import` speichern.
- **Tests:**
  - Payloads und Grenzen.
  - Signatur und Ex-/Import der Identität.
  - Lobby-Passwort im Lobby-Kern.
  - Ende zu Ende über `ListenHost`:
    - erster Kontakt bindet; fremder Schlüssel mit derselben Kennung abgelehnt (Abnahme)
    - ohne Nachweis abgelehnt, Wiederholung abgelehnt, veralteter Zeitstempel abgelehnt
    - Pflicht-Modus; Steam ausgenommen
    - Passwort falsch, fehlend und richtig; Fortbestand der Bindung über Export/Import
- **Rollback:** Branch verwerfen; ohne Nachweis und ohne Passwort verhalten sich alle Nachrichten wie zuvor.
