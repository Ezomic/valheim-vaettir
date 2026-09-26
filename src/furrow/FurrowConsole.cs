using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using UnityEngine;

namespace Furrow
{
    /// <summary>
    /// `furrow`, the console command: plant on the grid, plant off it, forget it, and ask
    /// how many grids a field is on.
    ///
    /// It exists so the grid can be tested without a hand on the mouse, and it was written
    /// for LHM-29, where the grid put two patches of one field on two grids and walked an oak
    /// row off the grid of its first tree. Nothing in Devkit's vocabulary could show that. Its
    /// `place` step calls Player.PlacePiece at a spot it chose itself, which is exactly the
    /// placement the grid never sees, and no step aims the cultivator and clicks.
    ///
    /// So `furrow plant` goes through GridPlacement.Resolve - the same call the ghost makes
    /// every frame, the same vote, the same held state - and plants where it answers. What it
    /// skips is the layer in front of that: vanilla's placement ray, the ghost itself, and
    /// the gates (GridEnabled, GridLevel, the Shift key). Those are unchanged by the fix and
    /// are said out loud in the output rather than assumed.
    ///
    /// `furrow check` is deliberately NOT built on the resolver. It reads positions off the
    /// plants standing in the world and asks, pair by pair, whether they share one lattice.
    /// A test that asked the grid whether it agreed with itself would pass while the grid was
    /// wrong.
    ///
    /// Offsets are metres along the grid's own rows and columns from where you stand - east
    /// and north at the default angle - rather than along your facing the way Devkit's
    /// `place` measures them. A bed laid along the player's facing is only a grid when the
    /// player happens to face along a world axis, and a scenario cannot choose that.
    ///
    /// Registered with isCheat, so typed at the console it needs devcommands, which a
    /// dedicated server never grants a client. Devkit's `mod` step calls the handler
    /// directly, so scenarios run it either way.
    /// </summary>
    [HarmonyPatch]
    internal static class FurrowConsole
    {
        /// <summary>
        /// Process-wide: Terminal's command table is a private static nothing ever clears, so
        /// a second registration would be a duplicate that outlives the world.
        /// </summary>
        private static bool _registered;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Terminal), "InitTerminal")]
        private static void Register()
        {
            if (_registered) return;
            _registered = true;

            new Terminal.ConsoleCommand("furrow",
                "furrow plant|free <plant> <x> <z> | furrow forget | furrow check <plant[,plant]> [radius]"
                + " - the planting grid, for tests",
                new Terminal.ConsoleEvent(OnCommand), isCheat: true);
        }

        private static void OnCommand(Terminal.ConsoleEventArgs args)
        {
            var term = args.Context;
            if (term == null) return;

            var what = args.Length > 1 ? args[1].ToLowerInvariant() : "";

            switch (what)
            {
                case "plant": Put(term, args, true); return;
                case "free": Put(term, args, false); return;
                case "forget": Forget(term); return;
                case "check": Check(term, args); return;
            }

            term.AddString("furrow plant <plant> <x> <z>  - plant on the grid, aimed x m along the rows and z m along the columns from you");
            term.AddString("furrow free <plant> <x> <z>   - plant exactly there, off any grid, the way Shift does");
            term.AddString("furrow forget                 - drop the pin and the grid in use, as a fresh game would");
            term.AddString("furrow check <plant[,plant]> [radius] - how many grids the standing plants are on");
            term.AddString("<plant> is a prefab name: sapling_carrot, sapling_turnip, Oak_Sapling...");
        }

        private static void Say(Terminal term, string line)
        {
            term.AddString(line);
            Grove.GrovePlugin.Log.LogInfo(line);
        }

        // ------------------------------------------------------------------ plant

        private static void Put(Terminal term, Terminal.ConsoleEventArgs args, bool onGrid)
        {
            var verb = onGrid ? "furrow plant" : "furrow free";

            var player = Player.m_localPlayer;
            if (player == null) { Say(term, verb + ": no player."); return; }
            if (ZNetScene.instance == null) { Say(term, verb + ": no world."); return; }

            float x, z;
            if (args.Length < 5 || !Number(args[3], out x) || !Number(args[4], out z))
            {
                Say(term, verb + " <plant> <x> <z> - x and z are metres along the grid from you.");
                return;
            }

            var prefab = ZNetScene.instance.GetPrefab(args[2]);
            if (prefab == null) { Say(term, verb + ": no prefab called " + args[2] + "."); return; }

            Piece piece;
            Plant plant;
            if (!prefab.TryGetComponent(out piece) || !prefab.TryGetComponent(out plant))
            {
                Say(term, verb + ": " + args[2] + " is not a plant the cultivator places.");
                return;
            }

            var angle = FurrowConfig.GridAngle.Value;
            var aim = player.transform.position
                      + Quaternion.Euler(0f, angle, 0f) * new Vector3(x, 0f, z);

            float ground;
            if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(aim, out ground))
                aim.y = ground;

            var step = Lattice.StepFor(plant);
            Lattice.Describe(prefab.name, plant, step);

            var at = aim;
            var how = "off any grid";

            if (onGrid)
            {
                Vector3 anchor;
                at = GridPlacement.Resolve(aim, step, out anchor);
                how = GridPlacement.Describe(GridPlacement.LastSource, GridPlacement.LastSupport);
            }

            // doAttack false, cheated false - the same two Devkit's own `place` passes. No
            // swing, and a test fixture does not mark the character as a cheater on a server
            // that reads cheat records at the door.
            player.PlacePiece(piece, at, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f),
                              doAttack: false, cheated: false);

            Say(term, verb + ": " + prefab.name + " at " + Flat(at) + ", " + how
                      + ", rows " + step.ToString("0.##", CultureInfo.InvariantCulture) + "m apart"
                      + Gates(player));
        }

        /// <summary>
        /// What the real ghost would do differently, if anything. The command plants on the
        /// grid regardless, because the thing under test is which grid - but a result read
        /// back later should not look like the grid a player at Farming 5 would get.
        /// </summary>
        private static string Gates(Player player)
        {
            if (!FurrowConfig.GridEnabled.Value) return " (note: GridEnabled is off, so a player gets no grid)";

            var level = player.GetSkillFactor(Skills.SkillType.Farming) * 100f;
            if (level < FurrowConfig.GridLevel.Value)
                return " (note: Farming " + Mathf.FloorToInt(level) + " is below GridLevel "
                       + FurrowConfig.GridLevel.Value + ", so this character gets no grid by hand)";

            return "";
        }

        // ------------------------------------------------------------------ forget

        private static void Forget(Terminal term)
        {
            GridPlacement.Forget();
            Say(term, "furrow forget: no pin and no grid in use - the next plant reads the ground.");
        }

        // ------------------------------------------------------------------ check

        /// <summary>
        /// How many lattices the standing plants of these kinds are spread across, near you.
        ///
        /// Greedy, pair by pair: each plant joins the first group whose first member it is in
        /// line with, at this spacing and the current angle, and otherwise starts a group. One
        /// group is one grid. The group sizes are printed largest first, so "on 2 grids (5 + 1)"
        /// and "on 2 grids (4 + 2)" can be told apart - a stray left out of a bed, against a bed
        /// that followed the stray.
        ///
        /// Saplings only, read off SlowUpdate's registry like the vote does. A test plants and
        /// checks in the same minute, so nothing it planted has had time to grow.
        /// </summary>
        private static void Check(Terminal term, Terminal.ConsoleEventArgs args)
        {
            var player = Player.m_localPlayer;
            if (player == null) { Say(term, "furrow check: no player."); return; }

            if (args.Length < 3) { Say(term, "furrow check <plant[,plant]> [radius]"); return; }

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in args[2].Split(','))
                if (name.Trim().Length > 0) names.Add(name.Trim());

            float radius;
            if (args.Length < 4 || !Number(args[3], out radius)) radius = 20f;

            var here = player.transform.position;
            var found = new List<Vector3>();
            var steps = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

            foreach (var slow in SlowUpdate.GetAllInstaces())
            {
                var plant = slow as Plant;
                if (plant == null) continue;

                ZNetView view;
                if (!plant.TryGetComponent(out view) || !view.IsValid()) continue;

                var name = Utils.GetPrefabName(plant.gameObject.name);
                if (!names.Contains(name)) continue;
                if (Lattice.FlatSqr(plant.transform.position, here) > radius * radius) continue;

                found.Add(plant.transform.position);
                steps[name] = Lattice.StepFor(plant);
            }

            var label = string.Join(",", new List<string>(names).ToArray());

            if (found.Count == 0)
            {
                Say(term, "furrow check: no " + label + " within " + radius + "m.");
                return;
            }

            // One spacing or the question has no answer: plants whose rows are a different
            // distance apart can never share a lattice, and saying "2 grids" for that would
            // read as the grid having failed.
            var step = -1f;
            foreach (var pair in steps)
            {
                if (step < 0f) { step = pair.Value; continue; }
                if (Lattice.SameStep(step, pair.Value)) continue;

                Say(term, "furrow check: " + label + " are not all spaced alike ("
                          + Spacings(steps) + "), so they cannot share one grid.");
                return;
            }

            var angle = FurrowConfig.GridAngle.Value;
            var into = Quaternion.Euler(0f, -angle, 0f);

            var firsts = new List<Vector3>();
            var sizes = new List<int>();

            foreach (var point in found)
            {
                var joined = false;
                for (var i = 0; i < firsts.Count && !joined; i++)
                {
                    if (!Lattice.On(point, firsts[i], step, into)) continue;
                    sizes[i]++;
                    joined = true;
                }

                if (joined) continue;
                firsts.Add(point);
                sizes.Add(1);
            }

            sizes.Sort((a, b) => b.CompareTo(a));

            var parts = new List<string>();
            foreach (var size in sizes) parts.Add(size.ToString(CultureInfo.InvariantCulture));

            Say(term, "furrow check: " + found.Count + " " + label + " within " + radius + "m, "
                      + (sizes.Count == 1
                          ? "on one grid"
                          : "on " + sizes.Count + " grids (" + string.Join(" + ", parts.ToArray()) + ")")
                      + ", rows " + step.ToString("0.##", CultureInfo.InvariantCulture) + "m apart at "
                      + angle.ToString("0.#", CultureInfo.InvariantCulture) + " degrees.");
        }

        private static string Spacings(Dictionary<string, float> steps)
        {
            var parts = new List<string>();
            foreach (var pair in steps)
                parts.Add(pair.Key + " " + pair.Value.ToString("0.##", CultureInfo.InvariantCulture) + "m");
            return string.Join(", ", parts.ToArray());
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>
        /// Invariant culture, because this machine writes 0,5 for a half and a scenario file
        /// written with 0.5 must mean the same thing on every machine it runs on.
        /// </summary>
        private static bool Number(string text, out float value)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Flat(Vector3 at)
        {
            return "(" + at.x.ToString("0.00", CultureInfo.InvariantCulture) + ", "
                   + at.z.ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }
    }
}
