using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using Multiplayer.Foundation.Protocol;
using RimWorld;
using Verse;

namespace Multiplayer.Foundation.RimWorld
{
    /// <summary>Version 1 of the opaque ADR 0038 ideology bytes. RimWorld Scribe calls must run on the game thread.</summary>
    internal static class FactionIdeologyCodec
    {
        private const byte Version = 1;
        private const byte Fixed = 0, Preset = 1, Edited = 2;
        private const int MaximumXmlBytes = 2_000_000;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static (string Label, byte[] Data) FixedChoice() => ("Ohne Ideology", new byte[] { Version, Fixed });

        internal static (string Label, byte[] Data) PresetChoice(IdeoPresetDef preset)
        {
            if (!DefNames.IsValid(preset.defName)) throw new InvalidDataException("Ungültige Ideologie-Vorlage.");
            return (preset.LabelCap.ToString(), Pack(Preset, StrictUtf8.GetBytes(preset.defName)));
        }

        internal static (string Label, byte[] Data) EditedChoice(Ideo ideology)
        {
            if (ideology == null || !FactionPayloads.IsValidText(ideology.name, FactionPayloads.MaximumIdeologyLabelBytes))
                throw new InvalidDataException("Die bearbeitete Ideologie hat keine gültige Bezeichnung.");
            string folder = Path.Combine(GenFilePaths.SaveDataFolderPath, "MultiplayerFoundation");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "faction-ideo-out.xml");
            Ideo saved = ideology;
            Scribe.saver.InitSaving(path, "mpIdeology");
            try { Scribe_Deep.Look(ref saved, "ideo"); }
            catch { Scribe.saver.ForceStop(); throw; }
            Scribe.saver.FinalizeSaving();
            byte[] xml = File.ReadAllBytes(path);
            if (xml.Length > MaximumXmlBytes) throw new InvalidDataException("Die Ideologie ist zu groß.");
            using (var output = new MemoryStream())
            {
                using (var zip = new GZipStream(output, CompressionLevel.Optimal, true)) zip.Write(xml, 0, xml.Length);
                return (ideology.name, Pack(Edited, output.ToArray()));
            }
        }

        private static byte[] Pack(byte kind, byte[] body)
        {
            byte[] data = new byte[body.Length + 2]; data[0] = Version; data[1] = kind;
            Buffer.BlockCopy(body, 0, data, 2, body.Length);
            if (data.Length > FactionPayloads.MaximumIdeologyBytes)
                throw new InvalidDataException("Ideologie überschreitet 49 152 Byte.");
            return data;
        }

        /// <summary>Checks the envelope, installed defs and bounded XML structure; creating game Ideos is a later step.</summary>
        internal static string? InvalidReason(string label, byte[]? data)
        {
            if (!FactionPayloads.IsValidText(label, FactionPayloads.MaximumIdeologyLabelBytes))
                return "Ideologie-Bezeichnung fehlt oder ist zu lang.";
            if (data == null || data.Length < 2 || data.Length > FactionPayloads.MaximumIdeologyBytes || data[0] != Version)
                return "Unbekanntes oder zu großes Ideologieformat.";
            if (data[1] == Fixed)
                return data.Length == 2 && !ModsConfig.IdeologyActive && label == "Ohne Ideology"
                    ? null : "Feste Wahl ist nur ohne Ideology gültig.";
            if (!ModsConfig.IdeologyActive) return "Ideology ist für diese Wahl nicht aktiv.";
            if (data[1] == Preset)
            {
                try
                {
                    string name = StrictUtf8.GetString(data, 2, data.Length - 2);
                    return DefNames.IsValid(name) && DefDatabase<IdeoPresetDef>.AllDefsListForReading.Any(def => def.defName == name)
                        ? null : "Ideologie-Vorlage ist nicht installiert.";
                }
                catch (DecoderFallbackException) { return "Ungültige Ideologie-Vorlage."; }
            }
            if (data[1] != Edited) return "Unbekannte Ideologie-Art.";
            try
            {
                using (var input = new MemoryStream(data, 2, data.Length - 2))
                using (var zip = new GZipStream(input, CompressionMode.Decompress))
                using (var xml = new MemoryStream())
                {
                    byte[] buffer = new byte[8192]; int count;
                    while ((count = zip.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (xml.Length + count > MaximumXmlBytes) return "Bearbeitete Ideologie ist entpackt zu groß.";
                        xml.Write(buffer, 0, count);
                    }
                    xml.Position = 0;
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                        MaxCharactersInDocument = MaximumXmlBytes };
                    using (XmlReader reader = XmlReader.Create(xml, settings))
                    {
                        reader.MoveToContent();
                        if (reader.Name != "mpIdeology") return "Bearbeitete Ideologie hat keinen Scribe-Stamm.";
                        bool foundIdeo = false;
                        while (reader.Read()) if (reader.NodeType == XmlNodeType.Element && reader.Name == "ideo") foundIdeo = true;
                        if (!foundIdeo) return "Bearbeitete Ideologie enthält keine Ideo-Daten.";
                    }
                }
                return null;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is XmlException)
            { return "Bearbeitete Ideologie ist beschädigt."; }
        }
    }
}
