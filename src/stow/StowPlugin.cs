using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using HarmonyLib;
using UnityEngine;

namespace Stow
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    // Soft, not hard. Stow installs and runs on its own; a hard dependency
    // that is absent does not degrade, the plugin simply never loads. Soft still buys
    // the load-order guarantee when Core is present, which is what registering needs.
    [BepInDependency(CoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    // No BepInProcess. It is a whitelist, and a dedicated server runs valheim_server.exe.
    // The stowing post is a registered prefab, and ZNetScene discards any ZDO whose prefab
    // name does not resolve - so a server without it destroys every post already built.
    public class StowPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.stow";
        public const string PluginName = "Stow";
        public const string PluginVersion = "0.5.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        private Harmony _harmony;
        private bool _stowHeld;
        private bool _configureHeld;
        private bool _propsReported;

        private void Awake()
        {
            Log = Logger;
            StowConfig.Bind(Config);
            TryRegisterWithCore();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(StowPatches));

            Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
        }

        /// <summary>
        /// Joins Core's version gate when Core is installed, and does nothing when it is not.
        ///
        /// Stow is worth installing on its own, and a hard dependency that is absent does
        /// not degrade gracefully - the plugin never loads at all. So the reference is
        /// compile-time only and the call is made behind a check.
        ///
        /// What is given up standing alone is the gate, not the mod.
        /// This registers a buildable piece, and a client that cannot resolve its prefab hash
        /// discards the ZDO rather than erroring - destroying posts already placed. Without Core
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

        private void OnGUI()
        {
            FilterPanel.Draw();
        }

        private void Update()
        {
            // Retried every frame, and deliberately not short-circuited by a flag of our
            // own: the piece needs ZNetScene and ObjectDB, neither exists at load, and both
            // are rebuilt for every world. Register asks the live scene whether it already
            // knows the prefab, so a call against a world that has it costs two lookups -
            // and a world that does not gets told, which is what stops its posts being
            // discarded.
            //
            // Guarding this with a static "already done" bool destroyed a built post.
            StowPost.Register();

            var player = Player.m_localPlayer;
            if (player == null) return;

            if (!_propsReported && StowConfig.LookForProps.Value.Length > 0)
            {
                _propsReported = true;
                PropIndex.Search(StowConfig.LookForProps.Value);
            }

            // Both unbound by default; the post replaced them. Kept because a key is the
            // right answer for someone who does not want to build anything.
            HandleStow(player);
            HandleConfigure(player);
        }

        private void HandleStow(Player player)
        {
            var down = StowConfig.KeyStow.Value.IsDown();
            if (!down) { _stowHeld = false; return; }
            if (_stowHeld) return;
            _stowHeld = true;

            if (Busy()) return;

            Depositor.Run(player);
        }

        private void HandleConfigure(Player player)
        {
            var down = StowConfig.KeyConfigure.Value.IsDown();
            if (!down) { _configureHeld = false; return; }
            if (_configureHeld) return;
            _configureHeld = true;

            if (FilterPanel.IsOpen) { FilterPanel.Close(); return; }
            if (Busy()) return;

            var hovering = StowPatches.Hovering(player);
            if (hovering == null) return;

            var container = hovering.GetComponentInParent<Container>();
            if (container == null || StowPost.Is(container)) return;

            FilterPanel.Open(container);
        }

        /// <summary>
        /// Player.TakeInput is protected, so the two windows that actually matter are
        /// checked directly: typing in a chest or the menu should not stow anything.
        /// </summary>
        private static bool Busy()
        {
            return InventoryGui.IsVisible() || Menu.IsVisible();
        }
    }

    internal static class StowPatches
    {
        private static readonly System.Reflection.FieldInfo PlayerHovering =
            AccessTools.Field(typeof(Player), "m_hovering");

        private static readonly System.Reflection.FieldInfo GuiContainer =
            AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        public static GameObject Hovering(Player player)
        {
            return PlayerHovering == null ? null : PlayerHovering.GetValue(player) as GameObject;
        }

        public static Container CurrentContainer(InventoryGui gui)
        {
            return GuiContainer == null || gui == null
                ? null
                : GuiContainer.GetValue(gui) as Container;
        }

        // ------------------------------------------------------------------ the post

        /// <summary>
        /// Closing the window is the moment that means "I am done putting things in", so
        /// it is the moment the post empties.
        ///
        /// Both close paths are patched because the game uses both: CloseContainer when
        /// you walk out of range or the chest closes itself, Hide when you press escape or
        /// tab. Each nulls m_currentContainer on its way out, so the post has to be read
        /// in a prefix, before it is gone.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "CloseContainer")]
        private static void EmptyOnClose(InventoryGui __instance)
        {
            EmptyPost(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
        private static void EmptyOnHide(InventoryGui __instance)
        {
            EmptyPost(__instance);
        }

        private static void EmptyPost(InventoryGui gui)
        {
            var container = CurrentContainer(gui);
            if (container == null) return;

            RulesButton.Hide();
            if (FilterPanel.IsOpen) FilterPanel.Close();

            var post = container.GetComponent<StowPost>();
            if (post != null) post.Empty();
        }

        /// <summary>Keeps the rules button in step with whichever chest is open.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
        private static void ShowRulesButton(InventoryGui __instance, Container container)
        {
            RulesButton.Sync(__instance, container);
        }

        // ------------------------------------------------------------------ hover

        /// <summary>
        /// A chest says what it holds without being opened.
        ///
        /// The rule is invisible otherwise - it lives on a ZDO - and a storage wall of
        /// twenty identical chests is exactly the case where you cannot remember which is
        /// which. This is the cheapest possible answer: it is already the text you are
        /// looking at when you are standing in front of the chest wondering.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        private static void ShowFilter(Container __instance, ref string __result)
        {
            if (string.IsNullOrEmpty(__result)) return;
            if (StowPost.Is(__instance)) return;

            var summary = ChestFilter.Summary(__instance);
            if (summary == null) return;

            __result += "\n<color=#D9A441>" + summary + "</color>";
        }

        // ------------------------------------------------------------------ panel

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static void BlockInput(ref bool __result)
        {
            if (FilterPanel.IsOpen) __result = false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static void BlockController(ref bool __result)
        {
            if (FilterPanel.IsOpen) __result = false;
        }

        /// <summary>Stops the camera swinging while the panel is up.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerController), "InInventoryEtc")]
        private static void HoldLookStill(ref bool __result)
        {
            if (FilterPanel.IsOpen) __result = true;
        }

        /// <summary>
        /// Frees the mouse pointer. GameCamera.UpdateMouseCapture re-locks and hides the
        /// cursor every frame unless one of ten named vanilla interfaces is visible, and a
        /// modded window is in none of them - without this the panel is one you can look
        /// at and not click.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
        private static void FreeCursor()
        {
            if (!FilterPanel.IsOpen) return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Escape closes the panel rather than opening the game's own menu.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Menu), nameof(Menu.Show))]
        private static bool EscapeClosesPanel()
        {
            if (!FilterPanel.IsOpen) return true;

            FilterPanel.Close();
            return false;
        }

        // ------------------------------------------------------------------ catalogue

        /// <summary>
        /// A new world may have different mods, so the catalogue is not kept.
        ///
        /// Both entry points, because they are reached in different situations: Awake for
        /// a local world, CopyOtherDB when a server hands its own item list over. Catching
        /// only the first would leave a client on a modded server matching against the
        /// wrong catalogue.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        private static void RebuildCatalogue()
        {
            Forget();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        private static void RebuildCatalogueFromServer()
        {
            Forget();
        }

        /// <summary>
        /// Everything cached off the last world.
        ///
        /// The item catalogue was the only one of these until the carrier existed, and
        /// the other two were a latent bug rather than a new requirement: borrowed
        /// materials and stripped item models are both lifted off prefabs belonging to
        /// whichever world was loaded at the time, and a static cache outlives the world
        /// it was filled from. The post is built once and keeps what it was built with;
        /// the carrier is built afresh on every run, well after startup, which is what
        /// made it start to matter.
        /// </summary>
        private static void Forget()
        {
            ItemGroups.Invalidate();

            // Order matters by one step: PostModel forgets the borrowed materials and
            // their atlas rectangles, and CarrierModel then drops the meshes those
            // rectangles were baked into. Dropping the meshes first would have them
            // reloaded and re-mapped against the rectangles that are about to be thrown
            // away.
            PostModel.Invalidate();
            CarrierModel.Invalidate();
            Flare.Invalidate();
            CarriedItem.Invalidate();
        }
    }
}
