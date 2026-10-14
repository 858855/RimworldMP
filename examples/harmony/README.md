# Harmony Prefix/Postfix — Analysebeispiel

Das Target `TickManager.TickManagerUpdate` erscheint nachweislich in `rwmt/Multiplayer/Source/Client/Patches/TickPatch.cs`. Die hier gezeigten leeren Prefix-/Postfix-Methoden demonstrieren **nur Patch-Syntax**; für eine Funktion ist Code erforderlich, der **keine** Weltmutation pro Frame betreibt. Echte Prioritäts-/BeforeAfter-Regeln müssen mit einer festen Harmony-/RimWorld-/MP-Version gegengeprüft werden.

C# **nicht gebaut oder gestartet**; auf 1.7 Harmony 3 und Target Reflection prüfen. Bei Multiplayer darf ein Harmony-Patch diese Methode nicht ohne genaue Prüfung des MP-Tick-Managers überspringen. Quelle: https://harmony.pardeike.net/articles/patching.html .
