using HarmonyLib;
using UnityEngine;

namespace Stow
{
    /// <summary>
    /// `stow holds` in the console: where the Holds button is in the open container window and
    /// whether it covers anything.
    ///
    /// Written for LHM-58, where the button sat over the top right cell of a wide chest and no
    /// check could tell, because nothing in the log or the build sees a rectangle. It reads
    /// the same Layout the placement uses, so it answers for the geometry that decided where
    /// the button went, and it reads the button's rect after the placement has run, so a
    /// placement that is wrong shows here.
    ///
    /// cellhits is how many inventory cells on screen the button's rectangle overlaps by more
    /// than a pixel. otherhits is the same for Take all, Place stacks and the title's drawn
    /// text. The names are distinct on purpose: Devkit's `printed` step matches a substring,
    /// and "cells=0" would also be found inside "overlapcells=0".
    ///
    /// isCheat false: it reads rectangles and changes nothing.
    /// </summary>
    internal static class StowConsole
    {
        private static bool _registered;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        private static void Register()
        {
            if (_registered) return;
            _registered = true;

            new Terminal.ConsoleCommand("stow",
                "holds: where the Holds button is in the open container window, and how many inventory cells it covers",
                new Terminal.ConsoleEvent(OnCommand), isCheat: false);
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            var term = args.Context;
            if (term == null) return;

            if (args.Length < 2 || args[1].ToLowerInvariant() != "holds")
            {
                term.AddString("stow holds, with a chest open: where the Holds button is and what it covers");
                return;
            }

            var gui = InventoryGui.instance;
            if (gui == null || !InventoryGui.IsVisible())
            {
                term.AddString("stow holds: the inventory window is not open");
                return;
            }

            var layout = RulesButton.Measure(gui);
            if (layout == null)
            {
                term.AddString("stow holds: state=missing   (the button has not been built or the grid is not there)");
                return;
            }

            var shown = RulesButton.IsShown;
            var rect = layout.Button;

            term.AddString("stow holds: state=" + (shown ? "shown" : "hidden")
                           + " cellcount=" + layout.Cells.Count
                           + " cellhits=" + layout.OverlapCells(rect)
                           + " otherhits=" + layout.OverlapOthers(rect)
                           + " button=" + F(rect.x) + "," + F(rect.y) + "," + F(rect.width) + "," + F(rect.height));
        }

        private static string F(float value)
        {
            return Mathf.Round(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
