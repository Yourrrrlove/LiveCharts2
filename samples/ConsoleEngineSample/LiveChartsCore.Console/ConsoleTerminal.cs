// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

using System.Globalization;
using System.Text.RegularExpressions;
using LiveChartsCore.Drawing;

namespace LiveChartsCore.Console;

/// <summary>
/// Helpers that talk to the host terminal via interactive control sequences. These all assume
/// stdout/stdin are real TTYs; pipe redirection makes them silently fail.
/// </summary>
public static class ConsoleTerminal
{
    /// <summary>
    /// Queries the terminal for its current background color via the OSC 11 sequence
    /// (<c>\x1b]11;?\x07</c>). Returns null if the terminal doesn't respond within
    /// <paramref name="timeoutMs"/>, doesn't support OSC 11 at all, or stdin is redirected.
    /// Modern terminals (Windows Terminal, iTerm2, WezTerm, ghostty, recent VSCode, xterm)
    /// implement this; older or stripped-down terminals don't.
    /// </summary>
    public static LvcColor? TryDetectBackground(int timeoutMs = 200)
    {
        if (System.Console.IsInputRedirected || System.Console.IsOutputRedirected)
            return null;

        try
        {
            // Send the query. BEL terminator (\x07) is more widely understood than ST.
            System.Console.Out.Write("\x1b]11;?\x07");
            System.Console.Out.Flush();

            var collected = new System.Text.StringBuilder();
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                if (System.Console.KeyAvailable)
                {
                    var key = System.Console.ReadKey(intercept: true);
                    var ch = key.KeyChar;
                    _ = collected.Append(ch);
                    if (ch == '\x07') break;
                    var s = collected.ToString();
                    if (s.EndsWith("\x1b\\")) break;
                }
                else
                {
                    Thread.Sleep(5);
                }
            }

            // Response shape: ESC ] 11 ; rgb : RRRR / GGGG / BBBB ( BEL | ESC \ ).
            // Components are 1–4 hex digits each; iTerm2 sends 4, others vary.
            var match = Regex.Match(
                collected.ToString(),
                @"rgb:([0-9a-fA-F]+)/([0-9a-fA-F]+)/([0-9a-fA-F]+)");
            if (!match.Success) return null;

            return new LvcColor(
                ScaleHexComponent(match.Groups[1].Value),
                ScaleHexComponent(match.Groups[2].Value),
                ScaleHexComponent(match.Groups[3].Value));
        }
        catch
        {
            // Any I/O hiccup (no console attached, raw mode failure on weird hosts) → bail.
            return null;
        }
    }

    private static byte ScaleHexComponent(string hex)
    {
        if (string.IsNullOrEmpty(hex)) return 0;
        var v = int.Parse(hex, NumberStyles.HexNumber);
        var max = (1 << (hex.Length * 4)) - 1;
        return max == 0 ? (byte)0 : (byte)(v * 255 / max);
    }
}
