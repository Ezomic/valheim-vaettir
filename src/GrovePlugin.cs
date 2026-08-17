using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Grove
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. Vaettir installs and runs on its own; a hard dependency
    // that is absent does not degrade, the plugin simply never loads. Soft still buys
    // the load-order guarantee when Core is present, which is what registering needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // The spirit and its pieces are registered prefabs, and ZNetScene discards any ZDO whose
    // prefab name does not resolve - so a server without it destroys them all, silently.
    //
    // Stow used to be declared here as a soft dependency, for load order: if it was present
    // it loaded first, so its post existed to be repriced. It is not a separate mod any
    // more - StowPlugin ships in this assembly - and BepInEx still orders the two plugin
    // classes, so the declaration bought nothing and read as though an absent third-party
    // mod were still involved.
    //
    // StowCoupling is deliberately left as it was, finding the post by prefab name rather
    // than by reference. It costs nothing now that the piece is guaranteed present, and the
    // lookup is what keeps CoupleToStow meaningful: someone who wants the sorting post at
    // its wood-and-nails price still turns the heartwood off and gets it.
    // The GUID by reference rather than as a literal, which is one thing the merge buys:
    // it is the same assembly now, so a rename over there is a compile error here instead
    // of a silent no-op.
    [BepInDependency(Stow.StowPlugin.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class GrovePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.vaettir";
        public const string PluginName = "Vaettir";
        public const string PluginVersion = "0.6.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Said = new HashSet<string>();

        private Harmony _harmony;
        private bool _diagnosticsDone;

        /// <summary>
        /// One thing that has to be retried until it takes, and whether it has given up.
        /// </summary>
        private sealed class Step
        {
            public string Name;
            public System.Func<bool> Run;
            public bool Abandoned;
        }

        private Step[] _steps;

        /// <summary>
        /// Runs one registration step, and stops running it for good if it ever throws.
        ///
        /// Retrying every frame is right for a step that is merely not ready yet - that is
        /// the whole design below. It is badly wrong for one that is broken, because the
        /// throw comes back every frame with it. That is not hypothetical: a null deref in
        /// SaplingPrefab.Strip, since fixed, wrote 46,457 identical stack traces in a
        /// single session and a 50MB log, and the frame cost of that was a far worse
        /// symptom than the missing sapling it was reporting.
        ///
        /// So a step that throws is abandoned rather than retried. The mod comes up short
        /// one prefab and says so once, which is a thing you can read and act on.
        /// </summary>
        /// <summary>
        /// StowCoupling.Apply returns nothing, and a Step wants a bool. It is never "done"
        /// in the sense the others are anyway - the post can appear at any point - so it
        /// reports false and simply keeps being called.
        /// </summary>
        private static bool StowApply()
        {
            StowCoupling.Apply();
            return false;
        }

        /// <summary>
        /// Registers everything right now, rather than on the next frame.
        ///
        /// This exists because a heartwood was deleted out of a saved inventory, and
        /// the game did it quite correctly. Inventory.AddItem, which is what a load
        /// runs for every stack, does:
        ///
        ///     GameObject itemPrefab = ObjectDB.instance.GetItemPrefab(name);
        ///     if (itemPrefab == null) { ZLog.Log("Failed to find item prefab " + name);
        ///                               return false; }
        ///
        /// An item it cannot resolve is skipped, not errored - and the next save writes
        /// the inventory back without it. There is no recovering from that.
        ///
        /// Registering from Update was therefore a race the whole time: our first frame
        /// against the player's inventory load. It won almost every time, which is the
        /// worst way for a race to behave, because it looks like it cannot happen right
        /// up until somebody loses something.
        ///
        /// So registration is driven from ObjectDB.Awake and ZNetScene.Awake as well.
        /// Both singletons come up during scene load and well before any player spawns,
        /// and each Register is idempotent and refuses until both exist - so whichever
        /// of the two lands second is the one that does the work. Update stays as the
        /// backstop for anything that arrives later still.
        /// </summary>
        internal static void RegisterNow()
        {
            if (Log == null) return;

            try
            {
                HeartwoodPrefab.Register();
                SpiritPrefab.Register();
                SaplingPrefab.Register();
            }
            catch (System.Exception e)
            {
                Log.LogError("Early registration failed; Update will retry. " + e);
            }
        }

        private void Run(Step step)
        {
            if (step.Abandoned) return;

            try
            {
                step.Run();
            }
            catch (System.Exception e)
            {
                step.Abandoned = true;
                Log.LogError(step.Name + " could not be registered and will not be retried "
                             + "again this session. " + e);
            }
        }

        /// <summary>
        /// Warns once rather than every frame.
        ///
        /// Registration is retried until it takes, so anything that complains inside it
        /// complains sixty times a second - which buries the log it was written to help
        /// with.
        /// </summary>
        internal static void LogOnce(string message)
        {
            if (Log == null || !Said.Add(message)) return;
            Log.LogWarning(message);
        }

        private void Awake()
        {
            Log = Logger;
            GroveConfig.Bind(Config);
            TryRegisterWithCore();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(BloodFeed));
            _harmony.PatchAll(typeof(GrovePatches));

            // Built once rather than per frame, so Update allocates nothing to iterate.
            _steps = new[]
            {
                new Step { Name = "Heartwood", Run = HeartwoodPrefab.Register },
                new Step { Name = "Forest spirit", Run = SpiritPrefab.Register },
                new Step { Name = "Ancient sapling", Run = SaplingPrefab.Register },
                new Step { Name = "Stow coupling", Run = StowApply },
            };

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");

            if (GroveConfig.TestMode.Value)
                Log.LogWarning("TEST MODE: a sapling needs three greydwarfs, not sixty. "
                               + "Turn TestMode off in the config before playing for real.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Vaettir is worth installing on its own, and a hard dependency that is absent does
        /// not degrade gracefully - the plugin never loads at all. So the reference is
        /// compile-time only and the call is made behind a check.
        ///
        /// What is given up standing alone is the gate, not the mod.
        /// This registers prefabs into ZNetScene, and a client that cannot resolve one discards
        /// the ZDO rather than erroring - destroying what is already standing. Without Core
        /// nothing refuses that client.
        /// </summary>
        private void TryRegisterWithCore()
        {
            if (!Chainloader.PluginInfos.ContainsKey(CoreGuid))
            {
                Log.LogInfo("Core not installed - running standalone, without the version gate.");
                return;
            }

            RegisterWithCore();
        }

        /// <summary>
        /// Kept separate and never inlined on purpose. The JIT resolves the assemblies a method
        /// needs when it first compiles that method, so a Suite call sitting directly in Awake
        /// would drag Ezomic.Core in before the check above could prevent it - and the
        /// missing-assembly exception would land during plugin load, which is the failure this
        /// whole arrangement exists to avoid. Isolating it means the type is only ever resolved
        /// on a machine that has Core.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void RegisterWithCore()
        {
            // Everyone, not HostOnly. Both ends have to agree about this mod, and the
            // disagreement is silent when they do not: a client that cannot resolve a prefab
            // hash discards the ZDO rather than erroring - destroying what is already standing
            // in the world - and item data that differs desyncs inventories.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config);
        }


        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        /// <summary>
        /// Registration is retried every frame until it takes.
        ///
        /// ZNetScene does not exist at load and the prefab cannot be built without it,
        /// so there is no single moment to hook - Register() is idempotent and returns
        /// immediately once done, which is cheaper than finding the right event and far
        /// harder to get wrong.
        /// </summary>
        private void Update()
        {
            // Ordered, and the order matters: Heartwood first, because the sapling's cost
            // and the spirit's gift both name it, and an item that is not in ObjectDB yet
            // is an item that silently is not there. Stow last, because it builds its post
            // on its own schedule - the piece simply appears in ZNetScene at some point
            // and this notices.
            foreach (var step in _steps) Run(step);

            if (_diagnosticsDone || ZNetScene.instance == null) return;
            _diagnosticsDone = true;

            if (GroveConfig.LookForPrefabs.Value.Length > 0)
                PropIndex.Search(GroveConfig.LookForPrefabs.Value);

            if (GroveConfig.DumpMaterials.Value)
                SpiritPrefab.DumpMaterials();
        }
    }

    internal static class GrovePatches
    {
        /// <summary>
        /// A new world may have different mods loaded, so borrowed materials are not
        /// kept across one. Both entry points, because a local world comes through
        /// Awake and a server hands its item list over through CopyOtherDB.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void ForgetSkins()
        {
            Skins.Invalidate();

            // The post is rebuilt with a fresh recipe on a new world, so the coupling
            // has to be reapplied - otherwise loading a second world after the first
            // leaves the post back at its unmodified cost.
            StowCoupling.Invalidate();

            // And the item goes back in immediately, not next frame. The first Awake of
            // a session fires against a stub ObjectDB with no items in it, where this
            // does nothing and correctly says so by refusing; the real one is the call
            // that matters, and it lands before any inventory is read.
            GrovePlugin.RegisterNow();
        }

        /// <summary>
        /// The other half of the same guarantee.
        ///
        /// ObjectDB.Awake and ZNetScene.Awake both run during scene load and neither is
        /// ordered against the other, while registration needs both singletons. So both
        /// are hooked and whichever runs second does the work.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static void SceneReady()
        {
            GrovePlugin.RegisterNow();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void ForgetSkinsFromServer()
        {
            Skins.Invalidate();
            StowCoupling.Invalidate();
            GrovePlugin.RegisterNow();
        }
    }
}
