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
        /// </summary>
        internal const int Current = 2;

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

            Revision.Value = Current;
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
