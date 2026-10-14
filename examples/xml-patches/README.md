# XML Patch, technisch begrenztes Lehrbeispiel

`ResearchPatch.xml`: Standard-`PatchOperationAdd` mit XPath-Prädikat. **Nicht in eine produktive Modliste übernehmen**: Der Ziel-DefName `Codex_ResearchSample` existiert nur im Beispiel-Def und Verwendungs- sowie Ladefolge müssen bestätigt werden. Als erste Modding-Quelle: https://rimworldwiki.com/wiki/Modding_Tutorials/PatchOperations.

Test: Syntax `python3 scripts/validate_structure.py`, danach im Zielspiel Debug-Log auf fehlgeschlagenes XPath/invalides Tag prüfen. Multiplayer: identische Defs/Modlisten, Patch-Ausführung vor Session sicherstellen, keine zur Laufzeit clientlokale Def-Mutation.
