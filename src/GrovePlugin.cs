using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Ezomic.Core;
using Ezomic.Shared;
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
    // Stow was a plugin of its own until the merge, declared here as a soft dependency so
    // that it loaded first and its post existed to be repriced. It is not a plugin at all
    // now - StowRuntime is driven from this class - so there is nothing to order against.
    //
    // StowCoupling is deliberately left as it was, finding the post by prefab name rather
    // than by reference. It costs nothing now that the piece is guaranteed present, and the
    // lookup is what keeps CoupleToStow meaningful: someone who wants the sorting post at
    // its wood-and-nails price still turns the heartwood off and gets it.
    public class GrovePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ezomic.valheim.vaettir";
        public const string PluginName = "Vaettir";
        public const string PluginVersion = "1.5.0";
        public const string PluginAuthor = "Robbin Thijssen";

        /// <summary>Core's plugin GUID. Optional - see TryRegisterWithCore.</summary>
        private const string CoreGuid = "ezomic.valheim.core";

        internal static ManualLogSource Log;

        private static readonly HashSet<string> Said = new HashSet<string>();

        private Harmony _harmony;
        private bool _diagnosticsDone;

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
                Prefabs.Tick();

                // Alongside the keeper, and for the same reason: a prefab that is not in
                // ZNetScene by the time a zone loads has its ZDOs discarded rather than
                // errored. A planted seedling is exactly as easy to lose that way as a
                // heartwood in an inventory. Thicket keeps its own registry because it
                // registers items AND pieces per plant, which Prefabs.Keep does not model.
                Thicket.WildPlants.Register();
                BonemealPrefab.Register();
            }
            catch (System.Exception e)
            {
                Log.LogError("Early registration failed; Update will retry. " + e);
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

            // One config file, one Harmony instance, one registration with Core. The post
            // was a second plugin until the merge and is now a part of this one; binding
            // its settings against this Config is what puts them in this mod's .cfg.
            GroveConfig.Bind(Config);
            Stow.StowConfig.Bind(Config);

            // The wild plants bind their own rows, one per plant, so the defaults live
            // beside the plant they describe rather than in a second list here that can
            // drift out of step with it.
            Thicket.ThicketConfig.Bind(Config);
            Furrow.FurrowConfig.Bind(Config);
            Thicket.WildPlants.Bind(Config);
            Stow.StowRuntime.Log = Logger;

            TryRegisterWithCore();

            // The prefabs are declared BEFORE anything is patched, and that order is the whole
            // point rather than tidiness.
            //
            // These four are the only things here that can cost somebody their world. ZNetScene
            // discards any ZDO whose prefab name will not resolve, silently and permanently, so
            // a Heartwood, a sapling, a spirit or a stow post standing in a world is deleted the
            // moment this mod loads without having declared it. Everything below is a feature:
            // if a patch does not apply, something does not work and you can read about it.
            //
            // They used to sit after thirteen PatchAll calls. Any one of those throwing - one
            // renamed method after a game update, which is ordinary - took the declarations with
            // it, so the first failure of a feature was also the permanent loss of everything
            // built. Declare first, then patch.
            //
            // Keep is only a declaration; Prefabs.Tick() from Update does the live registration
            // and re-does it for every world that loads, asking the scene each time rather than
            // trusting a flag of ours. That is the whole reason that file exists - a post
            // standing in a world was destroyed for want of exactly this.
            Prefabs.Log = Logger;

            // Heartwood first: it is the only item of the four, the sapling's cost names it
            // and the spirit hands one over, and an item ObjectDB cannot find by name is an
            // item that quietly does not exist.
            Prefabs.Keep(HeartwoodPrefab.Name, HeartwoodPrefab.Build, item: true);

            // The cultivator, not the hammer. It is a seed, so it belongs under the tool
            // already in your hand when you think "I want to plant this".
            Prefabs.Keep(SaplingPrefab.Name, SaplingPrefab.Build, buildTool: "Cultivator");

            Prefabs.Keep(SpiritPrefab.Name, SpiritPrefab.Build);

            // Declared only when the post is wanted. The old Register read this setting on
            // every call and returned early; a builder answering "disabled" with a null
            // would instead be retried five times and then reported as broken.
            if (Stow.StowConfig.PostEnabled.Value)
                Prefabs.Keep(Stow.StowPost.Name, Stow.StowPost.Build, buildTool: "Hammer");

            _harmony = new Harmony(PluginGuid);

            // Each group applied on its own, so one failure costs one feature rather than every
            // feature after it in the list.
            int failed = 0;
            failed += Apply("blood feeding", typeof(BloodFeed));
            failed += Apply("grove", typeof(GrovePatches));
            failed += Apply("stow", typeof(Stow.StowPatches));
            failed += Apply("thicket skill gate", typeof(Thicket.SkillGate));
            failed += Apply("thicket transplanting", typeof(Thicket.Transplant));
            failed += Apply("thicket carrying", typeof(Thicket.Carry));
            failed += Apply("fertilising", typeof(Fertilise));
            failed += Apply("furrow sowing", typeof(Furrow.Sowing));
            failed += Apply("furrow grid placement", typeof(Furrow.GridPlacement));
            failed += Apply("furrow area picking", typeof(Furrow.AreaPick));

            // Without this the sapling still calls, and every one of them appears on top of
            // it: the band is enforced by a prefix on SpawnArea.FindSpawnPoint, and an
            // unapplied patch is a silent fallback to vanilla's uniform disc.
            failed += Apply("beckon spawn point", typeof(BeckonSpawnPoint));
            failed += Apply("beckon waves", typeof(BeckonWave));

            // Keeps the sapling out of people's homes. Without it applying, the ghost stays
            // green over a longhouse and the seed goes in.
            failed += Apply("wilderness check", typeof(Wilderness));

            if (failed == 0)
            {
                Log.LogInfo(PluginName + " " + PluginVersion + " by " + PluginAuthor + " - ready.");
            }
            else
            {
                // Not the "ready." line, which is what an absence check greps for. The prefabs
                // are safe either way - that is what the reordering above buys - so this says
                // which half is affected rather than reading as total failure.
                Log.LogError(PluginName + " " + PluginVersion + " came up with " + failed
                    + " of its patch groups unapplied - see the errors above. The four prefabs "
                    + "ARE declared, so nothing standing in a world is at risk; the features "
                    + "behind those patches are off.");
            }

            if (GroveConfig.TestMode.Value)
                Log.LogWarning("TEST MODE: a sapling needs three greydwarfs, not sixty. "
                               + "Turn TestMode off in the config before playing for real.");
        }

        /// <summary>
        /// One patch group, applied so its failure cannot take the rest of the mod with it.
        /// Returns 1 when it failed, so the caller can just add them up.
        ///
        /// Harmony throws out of PatchAll when a target cannot be resolved - a rename, a changed
        /// signature, an ambiguous overload - which is the ordinary state of affairs on the first
        /// launch after a game update. Catching per group turns "Vaettir did nothing" into
        /// "Vaettir lost transplanting", and names which.
        /// </summary>
        private int Apply(string what, Type patches)
        {
            try
            {
                _harmony.PatchAll(patches);
                return 0;
            }
            catch (Exception e)
            {
                Log.LogError("Vaettir could not apply its " + what + " patches, so that feature "
                    + "is off for this session. The rest of the mod is unaffected. " + e.Message);
                return 1;
            }
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

            // What a host may decide and what is the player's own was settled entry
            // by entry on 2026-08-29, after the grid-angle bug: GridAngle is a float
            // the mod writes on every scroll notch, Core's own exemption covers only
            // KeyCode and KeyboardShortcut, and on a server Core's SettingChanged
            // watch snapped every write straight back to the host's value. The grid
            // simply would not turn online while working perfectly in singleplayer,
            // which is about the most misleading shape a bug can have.
            //
            // Suite.Local is exactly the declaration for this: Core's own comment on
            // it names a UI scale, a colour and a hover-text toggle as the same kind
            // of thing. It removes the key from the synced set, so the host's value
            // is never applied rather than merely being overridden afterwards.
            //
            // Everything below is per-player by nature and none of it is a rule about
            // how the world plays. Every gameplay number - costs, ranges, health,
            // rosters, caps, TestMode - stays synced on purpose, and so does every
            // shared prefab fact (donors, names, stack sizes), which both ends must
            // agree on. Three that look personal stay synced deliberately:
            // BeckonMessage and the grove Messages toggle are read on the machine
            // that owns the sapling and decide what OTHER players are shown, and
            // SpiritScale is baked into a persistent networked object's transform.

            // The grid and sowing gestures - the angle's whole family.
            Suite.Local(Furrow.FurrowConfig.GridAngle,
                        Furrow.FurrowConfig.GridTurnScroll,
                        Furrow.FurrowConfig.GridTurnStep,
                        Furrow.FurrowConfig.GridPreview,
                        Furrow.FurrowConfig.GridPreviewRings,
                        Furrow.FurrowConfig.RoomPreview,
                        Furrow.FurrowConfig.Shape,
                        Furrow.FurrowConfig.RowAcrossFacing,
                        Furrow.FurrowConfig.Verbose);

            // Your own map, messages on your own screen, the spirit's local render,
            // and the diagnostics.
            Suite.Local(GroveConfig.PinSaplings, GroveConfig.PinIcon,
                        GroveConfig.MoteCount, GroveConfig.RingCount,
                        GroveConfig.ShowHoop, GroveConfig.PartingEffect,
                        GroveConfig.BaseRefusal, GroveConfig.BiomeRefusal,
                        GroveConfig.GlowDonors, GroveConfig.Verbose,
                        GroveConfig.DumpMaterials, GroveConfig.LookForPrefabs);

            // Which of your own items the stow gesture may take, and the post's
            // lamp - built on every client from its own config, lighting nothing
            // anyone else sees.
            Suite.Local(Stow.StowConfig.KeepHotbar, Stow.StowConfig.NeverStow,
                        Stow.StowConfig.CarrierScale,
                        Stow.StowConfig.PostGlowDonors, Stow.StowConfig.FlareDonors,
                        Stow.StowConfig.PostLightRange, Stow.StowConfig.PostLightIntensity,
                        Stow.StowConfig.PostFlareScale,
                        Stow.StowConfig.Messages, Stow.StowConfig.Verbose,
                        Stow.StowConfig.LookForProps);

            // The carried plant's look in your arms, and Thicket's own chatter.
            Suite.Local(Thicket.ThicketConfig.Scale,
                        Thicket.ThicketConfig.SayTheLevel,
                        Thicket.ThicketConfig.Verbose);
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
            // One call, and cheap once satisfied: every check inside it is a live lookup
            // that returns immediately when the world already has the prefab.
            Prefabs.Tick();
            Thicket.WildPlants.Register();
            BonemealPrefab.Register();
            Furrow.Sowing.HandleKeys(Player.m_localPlayer);
            Thicket.Carry.Tick();

            // Not a registration, and so not part of the above. The post can appear at any
            // moment - it is a piece somebody builds - and this reprices its recipe when it
            // does, so it is never "done" and simply keeps being called.
            StowCoupling.Apply();

            // Takes map pins off saplings that are no longer there. Throttled to one sweep
            // a second inside, and it runs from here rather than from the sapling because
            // by the time a sapling could tell you it has gone, it is gone.
            SaplingPin.Reconcile(GroveConfig.SaplingName.Value);

            // The post registers itself on the same retry-until-it-takes footing as the
            // pieces above, and reads the two stow keys. Last, because it is the half of
            // the mod that depends on the heartwood existing.
            Stow.StowRuntime.Tick();

            // Says which Farming level a locked plant wants. Driven from here rather than
            // from inside the gate itself, which the Hud asks about every piece in the open
            // build menu on every frame.
            Thicket.SkillGate.Tick(Player.m_localPlayer);

            if (_diagnosticsDone || ZNetScene.instance == null) return;
            _diagnosticsDone = true;

            if (GroveConfig.LookForPrefabs.Value.Length > 0)
                PropIndex.Search(GroveConfig.LookForPrefabs.Value);

            if (GroveConfig.DumpMaterials.Value)
                SpiritPrefab.DumpMaterials();
        }

        /// <summary>
        /// The chest-rules panel is IMGUI, so it needs a MonoBehaviour to paint from and
        /// this is now the only one in the assembly.
        /// </summary>
        private void OnGUI()
        {
            Stow.FilterPanel.Draw();
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
            BonemealPrefab.Invalidate();

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
            BonemealPrefab.Invalidate();
            GrovePlugin.RegisterNow();
        }
    }
}
