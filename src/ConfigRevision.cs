using BepInEx.Configuration;

namespace Grove
{
    /// <summary>
    /// Moves a setting whose default changed on a machine that has already run this mod.
    ///
    /// BepInEx writes every entry to disk on first run and the saved value beats a new
    /// default in code, which is the right rule and makes a changed default reach nobody:
    /// a server that has been running Vaettir keeps the old number for good, and its host
    /// never learns there was a new one. Shipping a changelog line about it reaches the
    /// people who read changelogs.
    ///
    /// So one entry records which of these moves a file has had, and each move is applied
    /// once. A value somebody changed themselves is left exactly as it is - the test is
    /// that it still reads as the *previous* default, character for character - because
    /// overwriting a deliberate choice to hand out a new one is worse than the problem
    /// this exists to solve. Anyone who did deliberately set the old default gets moved
    /// once and can set it back; the log says so.
    ///
    /// On a server none of this decides anything by itself for a synced setting: Core hands
    /// the host's value to every client, so it is the host's file that has to move, which is
    /// exactly the file this runs against. A setting that is local to each player is never
    /// handed over, so there it is each player's own file, and that runs through here too.
    /// </summary>
    internal static class ConfigRevision
    {
        /// <summary>
        /// The number of moves this version knows about. Raise it by one and add the move
        /// below; never renumber, or a file that has had move 2 would be given it again.
        ///
        /// 1 - Vaettir 1.6.1: the stowing post's cost went from 20 fine wood and 20 iron
        ///     nails to 40 fine wood and 20 bronze nails, so the post lands with the Black
        ///     Forest behind you rather than the swamp.
        /// 2 - LHM-28: the crafting panel's chest total went from "{need} (+{chest})" to
        ///     "{have}/{need} +{chest}", both with the chest part in blue. Robbin picked it
        ///     from three mockups after the old line was reported cut off at its slot's
        ///     edge. Unlike move 1 this entry is local to each player (Suite.Local in
        ///     GrovePlugin), so it is every player's own file that moves, not only the
        ///     host's, and each one moves the first time that player loads this version.
        /// 3 - LHM-36: BossBiomes gained a Deep North row, read off the Frozen King prefab,
        ///     after core ce9aaac filed the Elaking and Jotun drops and the Vanguard family
        ///     in the Deep North. Without the move the row reaches no machine that has run
        ///     Vaettir before, and on those every one of those items comes out of a chest
        ///     with no boss killed. BossBiomes is synced, so it is the host's file that
        ///     decides on a server, as with move 1.
        /// 4 - LHM-63: BiomeOverrides lost CuredSquirrelHamstring:mistlands. That pin beat
        ///     every other source, and the Bog Witch sells the hamstring, so a swamp item
        ///     stayed in the chest until the Queen fell. BiomeOverrides is synced, so it is
        ///     the host's file that decides on a server, as with move 1.
        /// 5 - Thicket's rungs from Thistle up are ten Farming levels apart (35, 45, 55, 65,
        ///     75): cloudberry moved 60 to 55, to make room for the smoke puff at 65 and the
        ///     lingonberry at 75. Blue mushrooms are not in the game and keep their row. Each
        ///     row is synced, so it is the host's file that decides on a server.
        /// </summary>
        internal const int Current = 5;

        internal static ConfigEntry<int> Revision;

        internal static void Run(ConfigFile config)
        {
            Revision = config.Bind("Internal", "ConfigRevision", 0,
                "Which of this mod's config moves your file has had. Vaettir writes it; "
                + "there is nothing to set here. Lowering it makes the moves run again, "
                + "which is only ever useful for testing them.");

            // A file written by this version or a later one is already where it should be.
            // Ahead of Current is a downgrade, and the older code has no business undoing
            // a move it has never heard of.
            if (Revision.Value >= Current) return;

            if (Revision.Value < 1)
            {
                Moved(Stow.StowConfig.PostCost, "FineWood:20,IronNails:20");
            }

            if (Revision.Value < 2)
            {
                // The old default exactly as Hirsla shipped it and Vaettir kept it. Anyone
                // who had written their own layout keeps it, which is the rule; anyone who
                // wants the old line back can paste this into the file.
                Moved(Hod.HodConfig.ChestTotalFormat, "{need} <color=#88CCFF>(+{chest})</color>");
            }

            if (Revision.Value < 3)
            {
                // The default as it stood from Hirsla's fold until the Deep North row, which
                // is also exactly what both local profiles held when the row was added.
                Moved(Hod.HodConfig.BossBiomes,
                    "defeated_eikthyr:meadows, defeated_gdking:blackforest, "
                    + "defeated_bonemass:swamp, defeated_bonemass:ocean, "
                    + "defeated_dragon:mountain, defeated_goblinking:plains, "
                    + "defeated_queen:mistlands, defeated_fader:ashlands");
            }

            if (Revision.Value < 4)
            {
                // The default as it stood in 1.6.3 is today's with the pin put back in front of
                // TurretBoltBone. A list anyone edited reads differently and is left alone,
                // including one that keeps the pin on purpose.
                var overrides = Hod.HodConfig.BiomeOverrides;
                if (overrides != null && overrides.DefaultValue is string)
                    Moved(overrides, ((string)overrides.DefaultValue).Replace("TurretBoltBone:mistlands",
                        "CuredSquirrelHamstring:mistlands, TurretBoltBone:mistlands"));
            }

            if (Revision.Value < 5)
            {
                MovedRow("Cloudberry", "60 | thicket_uprooted_cloudberry:1 | Plains | 240-420");
            }

            Revision.Value = Current;
        }

        private static void MovedRow(string id, string was)
        {
            Moved(Thicket.ThicketConfig.RowEntry(id), was);
        }

        /// <summary>
        /// Puts an entry on its new default, but only if it still holds the old one.
        ///
        /// The new value is read off the entry rather than passed in, so the default lives
        /// in exactly one place - the Bind call that documents it - and this cannot drift
        /// away from it.
        /// </summary>
        private static void Moved(ConfigEntry<string> entry, string was)
        {
            if (entry == null) return;

            var now = entry.DefaultValue as string;
            if (now == null || now == was) return;

            var held = entry.Value == null ? "" : entry.Value.Trim();
            if (held != was) return;

            // Setting it is what writes the file: BepInEx saves on every set.
            entry.Value = now;

            GrovePlugin.Log.LogInfo(entry.Definition.Section + "/" + entry.Definition.Key
                + " still held the default from before this version (" + was + ") and "
                + "nothing had changed it, so it now reads " + now + ". Set it back in the "
                + "config file if you want the old one; this runs once.");
        }
    }
}
