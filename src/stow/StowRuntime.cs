using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Grove;

namespace Stow
{
    /// <summary>
    /// The stowing post's half of Vaettir, driven by GrovePlugin rather than by BepInEx.
    ///
    /// This was a plugin of its own until the merge, and leaving it one was the mistake:
    /// two [BepInPlugin] classes in a single DLL means two entries in the BepInEx log, two
    /// .cfg files, two registrations with Core's version gate and two version numbers for
    /// one file. All of that reads as two mods that happen to ship together, which is
    /// exactly what this is not.
    ///
    /// The GUID went with it. Config now lives in ezomic.valheim.vaettir.cfg alongside
    /// the sapling and the spirit, since BepInEx names a config file after the plugin GUID
    /// and there is only one of those now.
    ///
    /// Nothing about the world changes. The post keeps its prefab name, so every post
    /// already standing resolves exactly as before - which is the part that could not be
    /// got wrong, because ZNetScene discards a ZDO whose prefab name no longer resolves.
    /// </summary>
    internal static class StowRuntime
    {
        internal static ManualLogSource Log;

        private static bool _stowHeld;
        private static bool _configureHeld;
        private static bool _propsReported;

        /// <summary>Called once from GrovePlugin.Update, at the end of its own work.</summary>
        internal static void Tick()
        {
            // The post is no longer registered from here. GrovePlugin declares it to
            // Ezomic.Shared.Prefabs in Awake and that re-registers it into every world -
            // asking the live scene each time, never a flag of ours, which is the lesson
            // this file's own post was destroyed to teach on 2026-08-16.
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

        private static void HandleStow(Player player)
        {
            var down = StowConfig.KeyStow.Value.IsDown();
            if (!down) { _stowHeld = false; return; }
            if (_stowHeld) return;
            _stowHeld = true;

            if (Busy()) return;

            Depositor.Run(player);
        }

        private static void HandleConfigure(Player player)
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
