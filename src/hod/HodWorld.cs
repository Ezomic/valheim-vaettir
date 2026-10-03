using Ezomic.Shared;
using HarmonyLib;
using Grove;

namespace Hod
{
    /// <summary>
    /// The three things the bench service is built out of, kept up to date: the world's
    /// global keys, the item-to-biome index, and the withdrawal RPC pair.
    ///
    /// All three are per WORLD and not per process, and that distinction is the one that
    /// bites. ObjectDB and ZNetScene are torn down and rebuilt on every world load INCLUDING
    /// logging out to the menu and back in, so anything that answers from a static "already
    /// done" flag is answering about a world that no longer exists. Nothing here caches on a
    /// bool; everything is invalidated when the thing it was derived from is replaced.
    ///
    /// It is a patch class of its own rather than six more methods on the two Vaettir already
    /// has on ObjectDB.Awake, and that is deliberate. GrovePlugin applies each patch class
    /// separately and names it when it fails, so a game update that renames
    /// <c>ZoneSystem.RPC_GlobalKeys</c> or <c>SpawnSystem.Awake</c> costs the bench service
    /// and leaves Skins, the stow catalogue and every prefab declaration alone. Harmony is
    /// perfectly happy with several patches on one method; what it is not happy with is one
    /// PatchAll that throws halfway through.
    /// </summary>
    internal static class HodWorld
    {
        // ------------------------------------------------------------------ global keys

        /// <summary>
        /// Every route a global key can arrive by funnels through this one private method:
        /// RPC_SetGlobalKey on the server, RPC_GlobalKeys on every client, and the world's own
        /// .db on load. Patching the public SetGlobalKey instead would miss the bulk list a
        /// joining client receives - and joining a world where the bosses are already down is
        /// the common case, not the edge.
        ///
        /// It fires for every key in the game, most of which are world modifiers rather than
        /// progress, so the work of deciding whether anything actually moved is deferred to
        /// HodRuntime.Tick rather than done here.
        ///
        /// Note the asymmetry: GlobalKeyAdd does NOT fire on removal. GlobalKeyRemove is a
        /// separate private method, and a key being taken away - which is a console command
        /// and a server option, not something a player does - therefore does not reach this.
        /// The gate only ever opens on its own. That is the right way round for a feature
        /// whose whole promise is that progress is not taken back.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZoneSystem), "GlobalKeyAdd", new[] { typeof(string), typeof(bool) })]
        private static void OnGlobalKeyAdd()
        {
            HodGate.MarkStale();
            HodGate.Dirty = true;
        }

        /// <summary>
        /// Hold the previous answer steady while the world's key list is torn down and rebuilt.
        ///
        /// ZoneSystem.RPC_GlobalKeys calls ClearGlobalKeys and then re-adds every key one at a
        /// time, and it runs on every client every time anybody sets any key, because
        /// SetGlobalKey ends in SendGlobalKeys(Everybody). For the length of that loop the
        /// world genuinely holds fewer keys than it has - at the start of it, none.
        ///
        /// Vanilla never notices, because the refill is synchronous and no frame boundary
        /// falls inside it. The GlobalKeyAdd postfix above does notice, and without this flag
        /// every key in that list would have the gate recomputed against a half-filled
        /// dictionary. A sibling mod shipped only the deferred half of this fix and it was not
        /// enough: on 2026-08-25 it answered "not earned" mid-refill on the live server, wrote
        /// its numbers off the back of that answer, and no further key ever arrived to trigger
        /// a correction, so it stayed wrong for the rest of the session.
        ///
        /// A prefix and a finalizer rather than a wrapper: RPC_GlobalKeys is private and takes
        /// a List&lt;string&gt;, and the flag has to come down even if something inside throws -
        /// which is what a finalizer buys and a postfix does not.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ZoneSystem), "RPC_GlobalKeys")]
        private static void KeysStartArriving()
        {
            HodGate.Settling = true;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(ZoneSystem), "RPC_GlobalKeys")]
        private static void KeysFinishedArriving()
        {
            HodGate.Settling = false;

            // The list just installed is a different world state from whatever the cached
            // answer was built against, whichever keys happened to be in it.
            HodGate.MarkStale();
            HodGate.Dirty = true;
        }

        // ------------------------------------------------------------------ the routed RPCs

        /// <summary>
        /// Where the withdrawal request and its reply are registered, and where the previous
        /// session's registration is forgotten.
        ///
        /// Game.Start is the game's own place for this - its first six lines register
        /// ZRoutedRpc methods - so ZRoutedRpc.instance is certainly alive by the postfix, and
        /// unguarded there is exactly how vanilla treats it.
        ///
        /// A prefix AND a postfix on one method rather than a guess at the session lifecycle.
        /// ZNet.Awake builds a fresh ZRoutedRpc per session and a fresh Game comes with it, so
        /// "the instance we registered into is gone" and "Game.Start is about to run again"
        /// are the same event. Clearing on the way in and registering on the way out needs no
        /// second hook and cannot get out of order.
        ///
        /// ZRoutedRpc.Register uses Dictionary.Add, which THROWS on a duplicate name - and an
        /// exception out of a postfix on Game.Start would break the world load, for a
        /// convenience feature. HodWithdraw.Register catches for that reason; this is the
        /// other half of the same guard.
        ///
        /// Registered even when the feature is switched off or disabled by a conflict.
        /// Registration costs two dictionary entries and buys the ability to ANSWER: a player
        /// with the jib switched off is still somebody else's chest owner, and a peer who
        /// gets no reply waits out a five-second timeout instead of being refused at once.
        /// <b>Answer, not serve.</b> This sentence used to point at HodChests.OwnerMayServe
        /// and say the owner side refused on its own terms, which was simply untrue - nothing
        /// on that path ever asked whether the feature was on, so a machine with HodEnabled
        /// false still emptied its own chests into somebody else's craft. The refusal is in
        /// HodWithdraw.OnRequest now, at the top, and it answers with nothing so the
        /// requester declines in one round trip.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "Start")]
        private static void SessionStarting()
        {
            HodWithdraw.Unregister();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Game), "Start")]
        private static void SessionStarted()
        {
            HodWithdraw.Register();
        }

        // ------------------------------------------------------------------ the biome index

        /// <summary>
        /// Both ObjectDB entry points, because both really happen.
        ///
        /// Awake builds the database a world is played with. CopyOtherDB is NOT the server
        /// handing its item list over on join, which is what this comment used to claim in
        /// Hirsla and is worth correcting rather than deleting - somebody reading it would
        /// conclude that a mid-session ObjectDB replacement is a real event and reason about
        /// the index's lifetime from there. In 0.221.12 CopyOtherDB has exactly one caller in
        /// the whole assembly, FejdStartup.SetupObjectDB, which builds the main menu's
        /// database from a prefab. ObjectDB content is local; it is not networked at all. It
        /// is still patched, because that call is how the mod sees a world being LEFT for the
        /// menu, and the index left behind describes a world that no longer exists.
        ///
        /// The first ObjectDB.Awake of a session fires against a STUB - two status effects, no
        /// items - and ZoneSystem, SpawnSystem and ZNetScene do not exist yet either, so this
        /// pass reliably produces an incomplete index. That is expected rather than a failure:
        /// BiomeIndex says so itself through Complete, the gate answers openly while it is
        /// incomplete, and SpawnSystem.Awake below finishes the job.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void OnObjectDbAwake()
        {
            Rebuild();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void OnObjectDbCopy()
        {
            Rebuild();
        }

        /// <summary>
        /// The last of the three tables the index needs.
        ///
        /// ZoneSystem is up early enough that the world's keys arrive through it, and ZNetScene
        /// comes with the world - but SpawnSystem appears later, and a pass that ran before it
        /// has every creature drop missing from every biome. There is no ordering to rely on,
        /// so the index is simply rebuilt when the straggler turns up.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnSystem), "Awake")]
        private static void OnSpawnSystemAwake()
        {
            // Once. A loaded world holds more than one SpawnSystem, and without this every
            // one of them walks the vegetation table, the spawn lists, the whole prefab list
            // and every recipe in the game again.
            if (BiomeIndex.Complete) return;

            BiomeIndex.Prepare();
            HodGate.ReportIfChanged();
        }

        /// <summary>
        /// Rebuilds the index and drops everything derived from the world that has gone.
        ///
        /// <b>Unconditional, and not gated on HodEnabled.</b> That was weighed and rejected
        /// rather than overlooked. BiomeIndex.Build only rebuilds while the index is
        /// incomplete, so a lazy "build it when the first craft asks" would be a build per
        /// item asked until it succeeded - the shared file's own docstring says the first
        /// version of it did exactly that and walked the vegetation table a thousand times
        /// over. Skipping the build while the setting is off would therefore leave
        /// BiomeIndex.Complete false, and an incomplete index makes HodGate answer OPENLY for
        /// every item in the game - so a player who turned the bench service on mid-session
        /// would get an ungated one. A few tens of milliseconds once per world load is the
        /// price of that not being possible, and it is paid on the load screen.
        /// </summary>
        private static void Rebuild()
        {
            try
            {
                // Invalidate and not just Prepare. Prepare returns immediately when the index
                // is already complete, so without this a second world would be gated on the
                // first world's item database - and the two disagree the moment a mod is
                // added or removed between them.
                BiomeIndex.Invalidate();
                HodTraders.Invalidate();
                BiomeIndex.Prepare();
            }
            catch (System.Exception e)
            {
                // This runs inside a postfix on ObjectDB.Awake, which every other part of
                // Vaettir also hooks. An exception escaping here would land in the game's own
                // database setup - so the bench service loses its gate and says so, rather
                // than taking a world load down with it. An index that failed to build is
                // incomplete, and an incomplete index answers openly; that is a leak rather
                // than a lock-out, which is the direction this whole feature fails in.
                GrovePlugin.LogErrorOnce(
                    "The hod jib's biome index could not be built (" + e.Message + "), so the "
                    + "chests around a post are ungated until the next world load. Nothing "
                    + "else in Vaettir is affected.");
            }

            HodRuntime.Forget();
        }
    }
}
