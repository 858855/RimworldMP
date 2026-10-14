# Components/Scribe: Grundkonzept

Zeigt die Organisationsform `GameComponent`, `MapComponent`, `ThingComp`, `CompProperties`, `Scribe_Values.Look` und `CompTick` als 1.6-Vorlage. **Nicht kompiliert.** `GameComponent`-Basiskonstruktor und `ExposeData`-Overrides **in installierter RimWorld DLL** prüfen. Die alten öffentlichen RimWorld-Wiki-Seiten sind nicht verbindlich für 1.7. Keine eventlosen automatischen Mutationen im Beispiel.

MP: Diese Komponenten können guten Zustand kapseln, sind aber **nicht automatisch MP-kompatibel**. Jede mutierende UI-Aktion muss durch `MP.RegisterAll`/SyncMethod oder passende API abgesichert werden; feste Tick-Reihenfolge und vollständiger Scribe müssen dokumentiert und getestet sein.
