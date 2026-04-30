// MIT License - Copyright (c) 2021 Alberto Rodriguez Orozco & LiveCharts Contributors

namespace LiveChartsCore.Console;

/// <summary>
/// Selects how the off-screen sub-pixel grid is encoded into terminal cells.
/// </summary>
public enum ConsoleRenderMode
{
    /// <summary>
    /// One terminal cell = 1 column × 2 rows of sub-pixels, encoded with the upper/lower
    /// half-block characters (▀ ▄ █). Each cell can hold two distinct colors (top + bottom),
    /// so overlapping series stay visibly separated.
    /// </summary>
    HalfBlock = 0,

    /// <summary>
    /// One terminal cell = 2 columns × 4 rows of sub-pixels, encoded as Braille codepoints
    /// (U+2800–U+28FF). Quadruples the effective resolution at the cost of a single color
    /// per cell (last-write-wins when multiple series cross the same cell).
    /// </summary>
    Braille = 1,
}
