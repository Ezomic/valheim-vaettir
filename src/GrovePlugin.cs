using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Grove
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("ezomic.valheim.core", BepInDependency.DependencyFlags.HardDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // The spirit and its pieces are registered prefabs, and ZNetScene discards any ZDO whose
    // prefab name does not resolve - so a server without it destroys them all, silently.
    //
    // Soft, and only about load order: if Stow is present it loads first, so its post
    // exists to be repriced. Nothing here references Stow's assembly - the piece is
    // found by prefab name - so this mod loads and runs perfectly well without it.
    [BepInDependency("ezomic.valheim.stow", BepInDependency.DependencyFlags.SoftDependency)]
    public class GrovePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.vaettir";
        public const string PluginName = "Vaettir";
        public const string PluginVersion = "1.0.0";
        public const string PluginAuthor = "Robbin Thijssen";

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
            // Everyone, not HostOnly. Both ends have to agree about this mod, and the
            // disagreement is silent when they do not: a client that cannot resolve a prefab
            // hash discards the ZDO rather than erroring - destroying what is already standing
            // in the world - and item data that differs desyncs inventories.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config);

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
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void ForgetSkinsFromServer()
        {
            Skins.Invalidate();
            StowCoupling.Invalidate();
        }
    }
}
