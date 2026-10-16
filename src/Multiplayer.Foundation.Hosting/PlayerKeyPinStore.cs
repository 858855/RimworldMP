using System.Collections.Concurrent;
using Multiplayer.Foundation.Protocol;

namespace Multiplayer.Foundation.Server;

/// <summary>
/// Which public key belongs to which PlayerKey on this host (ADR 0035), pinned on first contact. Thread-safe. The game
/// stores <see cref="Export"/> with the save or settings and restores it with <see cref="Import"/>.
/// </summary>
public sealed class PlayerKeyPinStore
{
    private readonly ConcurrentDictionary<PlayerKey, string> pins = new();

    public bool IsPinned(PlayerKey key) => pins.ContainsKey(key);

    /// <summary>Pins the fingerprint on first contact; afterwards true only for the same fingerprint.</summary>
    public bool BindOrMatch(PlayerKey key, byte[] fingerprint)
    {
        string text = HelloProof.FingerprintText(fingerprint);
        return pins.GetOrAdd(key, text) == text;
    }

    public IReadOnlyList<(string PlayerKey, string Fingerprint)> Export() =>
        pins.OrderBy(p => p.Key.ToString(), StringComparer.Ordinal).Select(p => (p.Key.ToString(), p.Value)).ToList();

    public void Import(IEnumerable<(string PlayerKey, string Fingerprint)> entries)
    {
        foreach (var (keyText, fingerprint) in entries)
        {
            if (!PlayerKey.TryParse(keyText, out PlayerKey key)) throw new ArgumentException("Invalid player key: " + keyText);
            if (fingerprint.Length != 64 || fingerprint.Any(c => !Uri.IsHexDigit(c))) throw new ArgumentException("Invalid fingerprint.");
            pins[key] = fingerprint.ToLowerInvariant();
        }
    }
}
