using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Ezomic.Shared;
using Grove;

namespace Hod
{
    /// <summary>
    /// The bench service's half of Vaettir, driven by GrovePlugin rather than by BepInEx.
    ///
    /// This is Hirsla folded in, and the shape it takes is the one StowRuntime already
    /// settled: a static class with a Bind and a Tick, called from the one
    /// <c>[BepInPlugin]</c> the assembly has. <b>Two BepInPlugin classes in one DLL is two
    /// mods</b> - two entries in the BepInEx log, two .cfg files, two registrations with
    /// Core's version gate and two version numbers for one file - and the whole point of the
    /// fold is that the hod jib is a piece of Vaettir rather than a second mod bundled with
    /// it. So HirslaPlugin does not come across at all; everything it did lands here:
    ///
    ///   Config.Bind            a [Hod] section in ezomic.valheim.vaettir.cfg - see HodConfig
    ///   BiomeIndex's seams     wired below, because the shared index is source rather than a
    ///                          library and asks for a logger, a boss table and an overrides
    ///                          string instead of holding references to them
    ///   Core registration      gone. GrovePlugin already registers the whole mod at
    ///                          Requirement.Everyone, because Vaettir declares prefabs and a
    ///                          client that cannot resolve one discards the ZDO. There is no
    ///                          weaker setting available here, so Hirsla's RequireOnClients
    ///                          would have been a switch that could not do anything.
    ///   the conflict check     kept, and widened - see <see cref="Conflicts"/>
    ///   the Update drain       <see cref="Tick"/>
    ///
    /// Harmony is GrovePlugin's too. The three patch classes in this folder are applied by
    /// name through its Apply helper, so a game update that renames one of the methods they
    /// hook costs the bench service and leaves the rest of the mod - and every declared
    /// prefab - alone.
    /// </summary>
    internal static class HodRuntime
    {
        /// <summary>
        /// The mod this feature was lifted out of. It is still its own repository, still in
        /// both build lists and still deployed, so the two really can be in one process until
        /// the fold is merged.
        /// </summary>
        private const string HirslaGuid = "ezomic.valheim.hirsla";

        /// <summary>The mod Hirsla itself replaced. Same failure, one generation earlier.</summary>
        private const string TetherGuid = "ezomic.valheim.tether";

        private static bool _conflicted;

        private static bool _worldHooksLost;

        /// <summary>
        /// Whether another mod in this process counts the same chests, in which case the
        /// bench service does nothing at all. See <see cref="Conflicts"/>.
        /// </summary>
        public static bool Conflicted
        {
            get { return _conflicted; }
        }

        /// <summary>
        /// Whether the bench service is switched off for a reason that is not a setting.
        ///
        /// Read by <see cref="HodScope"/> on its frame-cached path rather than only at load,
        /// so there is exactly one branch between these flags and every "where" question the
        /// feature asks. That is cheaper than trusting that no other entry point exists, and
        /// the failures it prevents are ones nobody can see from inside the game - a chest
        /// counted twice, or a gate that is not there at all.
        ///
        /// Two reasons, and they shut the same door for opposite-looking causes: another mod
        /// already doing this, or this build's own world hooks failing to apply.
        /// </summary>
        public static bool Shut
        {
            get { return _conflicted || _worldHooksLost; }
        }

        /// <summary>
        /// Called by GrovePlugin when the HodWorld patch group would not apply.
        ///
        /// That group is what invalidates and rebuilds the biome index on every world load,
        /// and <b>an index that was never built is incomplete, which HodGate deliberately
        /// answers OPENLY for.</b> That fail-open is right where it belongs - at startup,
        /// before a player exists, for the few frames between a stub ObjectDB and a loaded
        /// world - and catastrophic as a permanent state: the crafting patches would still be
        /// applied, the chests would still be counted, and every material in the game would
        /// come out of them with no boss killed. A player would see a bench that works
        /// suspiciously well and nothing else.
        ///
        /// So the honest response to losing the gate is to lose the feature. It is the same
        /// shape as the conflict refusal and for the same reason: there is no half of this
        /// that is safe, and the alternative is a promise quietly not kept.
        ///
        /// It also takes the withdrawal RPCs and the global-key settling flag with it, both of
        /// which live in that group - so leaving the service running would additionally mean
        /// a gate that never noticed a boss dying and a chest wall nobody could ask.
        /// </summary>
        /// <summary>
        /// Chest mods that also feed the hammer, matched on the plugin's GUID or name with
        /// spaces removed. A substring match on purpose: GUIDs differ by author and version and
        /// the failure being avoided is quiet (a piece paid for twice).
        /// </summary>
        private static readonly string[] BuildFromChestsMods =
        {
            "azucraftyboxes", "craftfromcontainers", "craftfromchests", "storagecore",
        };

        private static string _buildYield;
        private static bool _buildYieldRead;

        /// <summary>
        /// Whether building from chests is held off because another mod already does it.
        /// Read lazily, on the first build question: at Bind time the plugins loaded after this
        /// one are not in the chainloader yet.
        /// </summary>
        public static bool BuildYields
        {
            get
            {
                if (_buildYieldRead) return _buildYield != null;
                _buildYieldRead = true;

                foreach (var info in Chainloader.PluginInfos.Values)
                {
                    var id = (info.Metadata.GUID + info.Metadata.Name).Replace(" ", "").ToLowerInvariant();
                    foreach (var token in BuildFromChestsMods)
                    {
                        if (!id.Contains(token)) continue;

                        _buildYield = info.Metadata.Name;
                        GrovePlugin.Log.LogWarning(
                            _buildYield + " is installed and also builds from chests, so Vaettir's "
                            + "hammer service has switched itself off for this session rather than "
                            + "pay a piece twice. Crafting at a bench is unaffected by this line.");
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>The mod that makes the hammer step aside, or null. For the `hod build` readout.</summary>
        public static string YieldsTo
        {
            get { return BuildYields ? _buildYield : null; }
        }

        public static void LoseWorldHooks()
        {
            _worldHooksLost = true;

            GrovePlugin.LogErrorOnce(
                "Vaettir could not apply the hod jib's world hooks, which are what build the "
                + "biome gate and keep it in step with the world's boss keys. Without them "
                + "the bench service would serve every material in the game with nothing "
                + "killed, so it has switched itself off for this session. Everything else in "
                + "Vaettir is unaffected and nothing built is at risk.");
        }

        /// <summary>
        /// Binds the config, wires the shared biome index, and decides whether to run at all.
        ///
        /// Called from GrovePlugin.Awake AFTER the prefab declarations and after
        /// GroveConfig.Bind - the first because nothing that can throw belongs in front of a
        /// declaration whose absence deletes what is standing in a world, and the second
        /// because HodConfig's Verbose entry is a pointer at GroveConfig's rather than a
        /// second switch of its own.
        /// </summary>
        public static void Bind(ConfigFile config)
        {
            HodConfig.Bind(config);

            WireBiomeIndex();

            _conflicted = Conflicts();
        }

        /// <summary>
        /// Hands the shared biome index the three things it cannot know on its own.
        ///
        /// BiomeIndex is shared source rather than a library - one copy in core\shared,
        /// linked into this project and into Yoke and Hirsla - so the mod's logger, the mod's
        /// boss table and the mod's config are static seams it asks for rather than
        /// references it holds. Every mod that links it therefore gives a player the same
        /// answer about the same item out of the same tables, which is the entire reason it
        /// lives where it lives.
        ///
        /// After HodConfig.Bind, because the overrides seam closes over a ConfigEntry that
        /// Bind creates. The delegate is not called until the first index build, a world load
        /// later, so the ordering is belt and braces - but a null field captured here would
        /// be a null field captured here, and the cost of getting it right is one line's
        /// position.
        ///
        /// The delegates read the config every time rather than copying a value in, because
        /// these are lines a player edits while the game is running and the index is rebuilt
        /// on every world load. A value snapshotted in Awake would answer for startup forever.
        /// </summary>
        private static void WireBiomeIndex()
        {
            BiomeIndex.Log = GrovePlugin.Log;
            BiomeIndex.BiomeForKey = HodGate.BiomeFor;
            BiomeIndex.Overrides = () => HodConfig.BiomeOverrides.Value;

            // Named explicitly even though it matches the shared default. The one warning
            // that sends a player off to pin a guessed item has to name a setting that really
            // is in Vaettir's own .cfg, whatever the shared default is changed to for
            // somebody else's sake.
            BiomeIndex.OverrideSetting = "BiomeOverrides";
        }

        /// <summary>
        /// Refuses to run the bench service beside a mod that reaches into the same chests.
        ///
        /// Not a matter of taste, and not the same as the two mods being incompatible. Both
        /// of these open a scope bracket around the same Inventory methods and both add chest
        /// stock inside it, so <b>a chest both can see is counted TWICE</b>: HaveRequirements
        /// says yes on twice the material that exists, and the two consume patches then race
        /// to take a shortfall each has already had taken by the other. Which of them wins
        /// depends on Harmony's patch order, which is not something a player can see or
        /// influence, and vanilla's <c>RemoveItem(string, ...)</c> returns void and stops
        /// quietly when it runs out - so the visible outcome is a sword built for half its
        /// cost with nothing in any log.
        ///
        /// <b>Only this feature is disabled, never the mod.</b> That is the one real
        /// difference from Hirsla's version of this check, and it is forced: Hirsla could
        /// refuse to apply any patch at all, because the mod WAS the feature. Vaettir
        /// declares prefabs, and a Vaettir that declined to run would discard every sapling,
        /// spirit, heartwood and stowing post standing in the world. So the flag is read on
        /// the scope's own path instead and everything else carries on untouched - the jib
        /// still stands, still draws its motes and still says what it is for, and does
        /// nothing.
        ///
        /// Hirsla and Tether are named rather than detected, because there is nothing to
        /// detect: a mod that patches Inventory.CountItems is not necessarily counting chests,
        /// and one that is may not patch that method at all. A third-party mod of this kind
        /// therefore goes unnoticed here, which is written down rather than papered over -
        /// the symptom is a recipe that looks twice as affordable as it is, and the fix is to
        /// turn one of the two off.
        /// </summary>
        private static bool Conflicts()
        {
            var name = Installed();
            if (name == null) return false;

            GrovePlugin.Log.LogWarning(
                name + " is installed, and Vaettir's bench service does the same job: both "
                + "add what is in nearby chests to the crafting panel's counts, inside their "
                + "own scope brackets, so a chest both can see is counted twice and a craft "
                + "can be paid for with material that is not there. The hod jib has switched "
                + "itself off for this session; everything else in Vaettir is unaffected and "
                + "nothing built is at risk. Remove " + name + " to use the jib.");

            return true;
        }

        private static string Installed()
        {
            if (Chainloader.PluginInfos.ContainsKey(HirslaGuid)) return "Hirsla";
            if (Chainloader.PluginInfos.ContainsKey(TetherGuid)) return "Tether";

            return null;
        }

        /// <summary>
        /// Once a frame, from GrovePlugin.Update.
        ///
        /// Deliberately not a coroutine or an Invoke: this has to run whether or not a player
        /// exists - a withdrawal can be outstanding while the world is being left - and it
        /// has to be the cheapest possible thing on the frames where nothing happened, which
        /// is nearly all of them. All three calls return on their first line when there is
        /// nothing to do.
        ///
        /// The withdrawal timer goes first, and unconditionally. A request nobody ever
        /// answers has to be let go of, or the in-flight guard keeps that chest shut for that
        /// material for the rest of the session - which from inside the game reads exactly
        /// like the biome gate having closed.
        ///
        /// The key drain is last and is deferred rather than done in the patch that notices.
        /// ZoneSystem.RPC_GlobalKeys clears the world's key list and re-adds it one key at a
        /// time, so a postfix that recomputed on the spot would ask "which biomes are open"
        /// once per key against a dictionary that was still filling up. Update is the first
        /// moment the whole list is certainly in, and it collapses a connect's worth of keys
        /// into one recompute instead of one per key.
        /// </summary>
        public static void Tick()
        {
            HodWithdraw.Tick();
            HodShow.Tick();
            HodRing.Tick();

            if (!HodGate.Dirty) return;
            HodGate.Dirty = false;

            // The flag is still drained when the feature is switched off, so that turning it
            // on later does not find a stale "dirty" from a boss killed an hour ago. What is
            // skipped is only the log line, which would otherwise announce what a disabled
            // jib serves.
            if (Shut || !HodConfig.Enabled.Value) return;

            HodGate.ReportIfChanged();
        }

        /// <summary>
        /// Everything derived from a world, dropped because that world has been replaced.
        ///
        /// ObjectDB and ZNetScene are torn down and rebuilt on every world load INCLUDING
        /// logging out to the menu and back in, so a cache that answers from a static flag is
        /// answering about a world that no longer exists. Each of these is a different kind
        /// of stale and none of them is safe to keep:
        ///
        ///   the chests      Containers and a physics layer mask from a scene that is gone
        ///   the withdrawals ZDOIDs naming containers in a world nobody is standing in, and a
        ///                   reply arriving against one has nowhere to put its items
        ///   the gate        an open-biome set computed from the previous world's keys
        ///   the scope       a StowPost reference into a destroyed scene
        ///   the flights     a spirit is a plain local GameObject with no ZNetView, so a world
        ///                   load does not clear one; it would hang in the air over ground
        ///                   that no longer exists
        ///   the labels      the crafting panel's requirement labels this feature resized to
        ///                   fit a chest total, keyed on objects the old scene owns
        /// </summary>
        public static void Forget()
        {
            HodChests.Forget();
            HodWithdraw.Forget();
            HodScope.Forget();
            HodNetwork.Forget();
            HodRing.Forget();
            HodShow.Clear();
            HodRequirement.Forget();

            HodGate.Reset();
            HodGate.Dirty = true;
        }
    }
}
