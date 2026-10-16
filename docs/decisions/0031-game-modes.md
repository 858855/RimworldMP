# ADR 0031 – Spielmodi: Coop (Host-Entscheid oder Demokratie) und Fraktionen

- **Status:** angenommen durch den Benutzer am 2026-10-16. Erweitert `docs/plans/rimworldmp-full-plan.md`: Plan §2 hatte mehrere Spielerfraktionen aus 1.0 ausgeschlossen. Grundlage ADR 0013 (Steam-Listen-Host), ADR 0015 (Fähnchen, alle fertig), ADR 0030 (Lockstep).
- **Entscheidung:** Der Host wählt den Modus **einmalig beim Erstellen der Lobby**. Er gilt für die ganze Partie und ist **später nicht änderbar**. Beim Laden eines Spielstands kommt der Modus aus dem Spielstand.

| Modus | Inhalt | Wer entscheidet |
|---|---|---|
| **Coop – Host-Entscheid** | eine gemeinsame Kolonie (Plan §2–§7) | Einrichtung und Ereignisse im Spiel: der Host |
| **Coop – Demokratie** | eine gemeinsame Kolonie | Einrichtung: der Host. **Ereignisse im Spiel** (z. B. Quest annehmen, Flüchtlinge aufnehmen): Abstimmung aller, **der Host entscheidet bei Gleichstand** |
| **Fraktionen** | jeder Spieler seine eigene Fraktion und Kolonie | jeder für seine Fraktion |

- **Fraktionsmodus im Einzelnen** (Benutzervorgabe):
  - So viele Fraktionen wie Spieler in der Lobby.
  - Jeder wählt seine **eigene Ideologie** und seinen **eigenen Startort**; gestartet wird, wenn alle fertig sind. Szenario, Erzähler und Welt wählt weiterhin der Host [A].
  - Jeder Spieler hat seine **eigene Kolonie auf einer eigenen Karte** der gemeinsamen Welt.
  - **Beziehung der Fraktionen** wählt der Host beim Erstellen: verbündet, neutral oder verfeindet. Ob sie sich im Spiel ändern darf, ist offen.
  - **Spieler-Fenster:** einklappbar, listet alle Spieler und Kolonien. Ein Klick wechselt die **Ansicht** zur Kolonie eines anderen, etwa um bei einem Raid zuzusehen, und zurück zur eigenen. Der Ansichtswechsel ist rein lokal und kein Befehl. Plan §7.4 gilt besonders: Spiellogik darf nie aus der aktuell angezeigten Karte lesen.
  - **Weltweite Ereignisse** (Beispiel des Benutzers: das Gravship aus Odyssey) gehen **an alle Spieler**, nicht nur an eine Fraktion. Offen ist, ob jede Fraktion eine eigene Kopie erhält oder ob es ein gemeinsames Ereignis ist.
- **Folgen für die Technik:**
  - **Gemeinsam für alle Modi:** Lobby, Steam-Einladung, Mod-Prüfung, Chunk-Kanal, Weltübertragung, Spielstart und der Lockstep-Koordinator (ADR 0030). Auch im Fraktionsmodus simuliert jeder Rechner die ganze Welt mit allen Kolonien, und die Zeit läuft für alle gleich. Pausiert einer, steht auch die andere Kolonie. Ob im Fraktionsmodus jeder pausieren darf, ist offen.
  - **Fraktionsmodus ist technisch die schwerere Variante.** RimWorld kennt im Kern eine Spielerfraktion (`Faction.OfPlayer`); daran hängen Kolonistenleiste, Zonen, Bauaufträge, Forschung, Arbeitsplan und Koloniewert für die Raid-Stärke. Jeder dieser Bereiche muss je Fraktion getrennt werden. Rechenlast: Jeder Rechner simuliert alle Kolonien.
  - **Demokratie** braucht für jede Abstimmung im Spiel eine gemeinsame Pause (Plan §7.6), eine Zählung und den Host als Gleichstand-Entscheider. Die Abstimmung ist ein Lockstep-Befehl, das Ergebnis wirkt bei allen am selben Tick.
  - ADR 0015 (Fähnchen sind Vorschläge, nur der Host wählt den Startort) gilt im Coop-Modus. Die Einrichtung bleibt dort laut Benutzer beim Host, auch in der Demokratie-Variante.
- **Reihenfolge** (Benutzer folgte der Empfehlung):
  1. Modus jetzt in Protokoll, Server, Lobbyzustand und Spielstand vorsehen, auch wenn zunächst nur Coop spielbar ist. Der Server lehnt den Start im Fraktionsmodus ab, solange er nicht gebaut ist.
  2. Coop spielbar machen (Plan §7).
  3. Fraktionsmodus.
- **Aufteilung** (ADR 0030): Protokoll, Server-Regeln und Lobbyzustand macht die Koordinator-Seite. Die Auswahl im Lobby-Fenster, die Anzeige in der Lobbyliste und das Speichern des Modus im Spielstand (`MultiplayerSession`) macht die Spielseite.
- **Offene Fragen an den Benutzer:**
  - Gravship und andere Weltereignisse im Fraktionsmodus: Kopie je Fraktion oder ein gemeinsames Ereignis?
  - Pause im Fraktionsmodus: darf jeder pausieren?
  - Fraktionsbeziehung im Spiel änderbar?
  - Demokratie: Welche Ereignisse genau, und gibt es eine Abstimmungsfrist?
- **Rollback:** ADR zurückziehen; ohne Moduswahl gilt Coop mit Host-Entscheid.
