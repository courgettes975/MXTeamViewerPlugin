namespace Loupedeck.MXTeamViewerPlugin
{
    using System;
    using System.Collections.Generic;

    // Parses "Ctrl+Alt+F", "Ctrl+K", "F11", "Ctrl+Plus" into virtual-key codes.
    internal static class KeyChord
    {
        private static readonly Dictionary<string, ushort> Named = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ctrl"] = 0x11, ["control"] = 0x11, ["shift"] = 0x10, ["alt"] = 0x12, ["win"] = 0x5B,
            ["enter"] = 0x0D, ["esc"] = 0x1B, ["escape"] = 0x1B, ["tab"] = 0x09, ["space"] = 0x20,
            ["backspace"] = 0x08, ["delete"] = 0x2E, ["del"] = 0x2E, ["insert"] = 0x2D,
            ["home"] = 0x24, ["end"] = 0x23, ["pageup"] = 0x21, ["pagedown"] = 0x22,
            ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28,
            ["plus"] = 0xBB, ["minus"] = 0xBD, ["numplus"] = 0x6B, ["numminus"] = 0x6D,
            ["slash"] = 0xBF, ["scrolllock"] = 0x91,
        };

        public static bool TryParse(string text, out List<ushort> keys)
        {
            keys = new List<ushort>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }
            foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Named.TryGetValue(raw, out var vk))
                {
                    keys.Add(vk);
                }
                else if (raw.Length == 1 && char.IsLetterOrDigit(raw[0]))
                {
                    keys.Add(char.ToUpperInvariant(raw[0]));
                }
                else if ((raw[0] == 'F' || raw[0] == 'f') && int.TryParse(raw.AsSpan(1), out var f) && f is >= 1 and <= 24)
                {
                    keys.Add((ushort)(0x70 + f - 1));
                }
                else
                {
                    keys.Clear();
                    return false;
                }
            }
            return keys.Count > 0;
        }
    }
}
