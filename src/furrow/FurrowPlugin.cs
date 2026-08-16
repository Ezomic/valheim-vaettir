using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;

namespace Furrow
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. Furrow installs and runs on its own; a hard dependency
    // that is absent does not degrade, the plugin simply never loads. Soft still buys
    // the load-order guarantee when Core is present, which is what registering needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // No BepInProcess. Sowing is placement-time and entirely client-side: every extra seed
    // goes through Player.PlacePiece, which is the same call the game makes for a hand-placed
    // one, so the server sees ordinary plants and needs to know nothing about this mod.
    public class FurrowPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.furrow";
        public const string PluginName = "Furrow";
        public const string PluginVersion = "0.1.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            FurrowConfig.Bind(Config);

            TryRegisterWithCore();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(Sowing));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Furrow is worth installing on its own, and a hard dependency that is absent does
        /// not degrade gracefully - the plugin never loads at all. So the reference is
        /// compile-time only and the call is made behind a check.
        ///
        /// What is given up standing alone is the gate, not the mod.
        /// Least is lost here of any mod in the suite: Furrow is HostOnly on the gate already,
        /// because a player without it plants one seed at a time and is not out of step with
        /// anyone.
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
            // HostOnly rather than Everyone: a player without Furrow is not out of step with
            // one who has it, they just plant one seed at a time. Nothing this mod does
            // changes a prefab, an item or a ZDO, so there is no desync to guard against.
            Suite.Register(PluginGuid, PluginName, PluginVersion, Config, Requirement.HostOnly);
        }


        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }

        private void Update()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            // Only while the cultivator is actually out. Reading the keys the rest of the
            // time would mean the numpad silently changed a setting during combat.
            if (!player.InPlaceMode()) return;

            Sowing.HandleKeys(player);
        }
    }
}
