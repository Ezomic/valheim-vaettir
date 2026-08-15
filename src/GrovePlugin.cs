using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace Grove
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
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
        public const string PluginVersion = "0.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Said = new HashSet<string>();

        private Harmony _harmony;
        private bool _diagnosticsDone;

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

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(BloodFeed));
            _harmony.PatchAll(typeof(GrovePatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
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
            // Heartwood first: the sapling's cost and the spirit's gift both name it,
            // and an item that is not in ObjectDB yet is an item that silently is not
            // there.
            HeartwoodPrefab.Register();
            SpiritPrefab.Register();
            SaplingPrefab.Register();

            // Last, and retried like the rest: Stow builds its post on its own schedule,
            // so there is no moment to hook - the piece simply appears in ZNetScene at
            // some point and this notices.
            StowCoupling.Apply();

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
