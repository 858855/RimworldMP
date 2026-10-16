# IP-Lobbys: Passwort und Schlüsselbindung (SEC-001) – Prüfung 2026-10-16

- **Branch/Basis:** `claude/ip-password` von `main` @ `8f7c32f`. Bahn Claude, C4. Design ADR 0035.
- **Umgebung:** Linux, .NET SDK 10.0.402 / Runtime 10.0.13, lokale RimWorld-Assemblies `1.6.4871` (rev598).
- **Mono-Prüfung:** Die Metadaten von `RimWorldLinux_Data/Managed/mscorlib.dll` wurden per `System.Reflection.Metadata` gelesen; laden lässt sich Monos `mscorlib` in .NET nicht. Vorhanden sind:
  - `RSA.Create()`, `RSA.Create(Int32)`, `SignData`/`VerifyData(…, HashAlgorithmName, RSASignaturePadding)`, `ExportParameters`, `ImportParameters`
  - `RSACryptoServiceProvider.SignHash(Byte[], HashAlgorithmName, RSASignaturePadding)`, nicht abstrakt
  - `HashAlgorithmName.SHA256`, `RSASignaturePadding.Pkcs1`, `SHA256.Create`, `RandomNumberGenerator`

  Die Funktion zur Laufzeit unter Mono ist **WAHRSCHEINLICH**, nicht getestet.

## Tatsächlich ausgeführte Prüfungen

| Befehl/Prüfung | Ergebnis |
|---|---|
| `DOTNET_BIN=…/dotnet bash scripts/build-mod.sh` | Exit 0; 0 Warnungen, 0 Fehler |
| `DOTNET_BIN=…/dotnet bash scripts/build-server.sh` | Exit 0 |
| `Protocol.Tests` | Exit 0; 17 Gruppen, neu siehe unten |
| `Network.Tests` | Exit 0; zwölf Gruppen, neu siehe unten |
| `Lobby.Tests` | Exit 0; neun Gruppen, neu: Passwort fehlt, falsch, ähnlich (Präfix) oder richtig; Kennzeichen in der Liste |
| `Hosting.Tests` | Exit 0; 26 Gruppen, neu siehe unten |
| `Integration.Tests`, `Client.Tests`, `ProcessIntegration.Tests`, `Session.Tests` | Exit 0; unverändert |
| `python3 scripts/validate_project_state.py` | Exit 0 |

Neu in `Protocol.Tests`:
- Nachweis gültig. Ungültig, sobald Name, Kennung, Zeitstempel, Zufallswert oder Signatur verändert sind.
- Rundlauf im Hello; 1024-Bit-Schlüssel und falsche Signaturlänge abgelehnt.
- `JoinLobby` alt (nur ID) und neu (mit Passwort); ungültige Passwörter abgelehnt; Passwort in `CreateLobby`; Kennzeichen in der Liste.

Neu in `Network.Tests`:
- 2048-Bit-Schlüsselpaar; Signatur an den Namen gebunden.
- Zwei Nachweise in derselben Sekunde unterscheiden sich.
- Gespeicherte und wiederhergestellte Identität liefert denselben Fingerabdruck.

Neu in `Hosting.Tests`, Ende zu Ende über `ListenHost`:
- Der eigene Schlüssel des Hosts ist gebunden.
- Alices erster Kontakt bindet ihren Schlüssel.
- Passwort: fehlend und falsch abgelehnt, richtig angenommen; das Schloss ist in der Liste sichtbar.
- **Abnahme:** Fremder Schlüssel mit Alices Kennung abgelehnt („bound to another identity“). Alices Kennung ohne Nachweis abgelehnt („requires proof“). Die echte Alice wird wieder angenommen.
- Wiederholung desselben signierten Hellos abgelehnt („already used“); Nachweis von vor einer Stunde abgelehnt.
- Bindungen über `Export`/`Import` in ein neues Spiel übernommen; dort weiterhin geschützt.
- Pflicht-Modus lehnt TCP ohne Nachweis ab; Steam-verifizierte Verbindung ohne Nachweis wird angenommen.

**Gefundener Fehler:** PKCS#1-v1.5-Signaturen sind deterministisch. Ohne Zufallswert hätte ein Spieler, der sich binnen derselben Sekunde neu verbindet, als Wiederholung gegolten. Behoben durch einen signierten Zufallswert je Hello.

## Klassifizierung und Grenzen

`BUILD_ERFOLGREICH`, Protokoll und Server automatisiert getestet. Grenzen laut ADR 0035: kein TLS; TOFU beim ersten Kontakt; Wiederholungsspeicher nur im laufenden Host.

Offen auf der Spielseite (Codex):
- Identität erzeugen und speichern.
- Passwortfeld und Schloss-Symbol.
- `RequireKeyProof` für IP-Lobbys einschalten.
- Bindungen speichern.

Im Spiel `NICHT_GETESTET`.
