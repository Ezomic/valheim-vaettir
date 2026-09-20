using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Grove;

namespace Hod
{
    /// <summary>
    /// Makes the chests around a stowing post count as part of your inventory, but only while
    /// the game is asking a crafting question.
    ///
    /// Every requirement check and every consumption in Valheim funnels through three methods
    /// on the player's own Inventory: CountItems, HaveItem and RemoveItem. Those are also
    /// called constantly by everything else - carry weight, the hotbar, the item tooltip,
    /// every other mod - so patching them unconditionally would have chests bleeding into
    /// numbers that have nothing to do with crafting.
    ///
    /// Hence the scope bracket. A depth counter is opened by the methods that are genuinely
    /// asking a crafting question and the Inventory patches are inert unless it is open.
    ///
    /// The three bracketed methods, and why each one:
    ///
    ///   Player.HaveRequirementItems              every recipe, crafting and upgrading. It is
    ///                                            PRIVATE and its first parameter is named
    ///                                            "piece" although its type is Recipe, so
    ///                                            Harmony's named injection wants "piece".
    ///   Player.ConsumeResources                  the spend
    ///   InventoryGui.SetupRequirementList        the requirement lines' red flash
    ///
    /// That last one is display rather than logic and it still belongs here. Vanilla decides
    /// whether the amount flashes red by comparing it against player.GetInventory()
    /// .CountItems(name); leave it outside the bracket and a recipe whose material is entirely
    /// in the chest un-greys in the list and still flashes red on its own requirement line,
    /// which reads as two parts of the panel disagreeing.
    ///
    /// <b>Player.HaveRequirements(Piece, RequirementMode) was a fourth and is deliberately
    /// gone.</b> It is the gate for every buildable piece, and building was cut out of this
    /// feature on 2026-09-06 - the crafting panel is the only seam. Nothing subtle happens as
    /// a result: it is a separate method from the Recipe overload the panel uses
    /// (assembly_valheim 0.221.12, Player.cs:2623 against 2517), neither calls the other, and
    /// the Recipe path reaches CountItems only through the private HaveRequirementItems still
    /// bracketed above. So the hammer counts and spends exactly what vanilla counts and spends.
    ///
    /// ConsumeResources is the one bracket that still spans a building call - it is one method
    /// serving both the craft and the placement, Player.cs:1167 and InventoryGui.cs:1564 - and
    /// it is inert on that side in ordinary play plus one explicit check. Ordinary play:
    /// UpdatePlacement asks HaveRequirements(piece, CanBuild) and calls ConsumeResources in
    /// the same frame, so a placement that happens at all is one the pack could already pay
    /// for and TakeFromChests computes a shortfall of zero. The check is for the exception
    /// that argument used to miss - the real line is
    /// <c>if (m_noPlacementCost || HaveRequirements(...))</c>, so the noplacementcost cheat
    /// skips the gate and still reaches the spend. TakeFromChests refuses on NoCostCheat for
    /// that reason and no other.
    ///
    /// <b>And SetupRequirementList rather than SetupRequirement.</b> SetupRequirement is the
    /// single draw method for BOTH panels, so bracketing it would put chest stock into the
    /// build HUD's red flash as well - a lie, since the hammer does not draw on chests.
    /// SetupRequirementList is private, is called from one place, and wraps every one of the
    /// crafting panel's requirement lines and none of the build HUD's. The obvious
    /// alternative was to read SetupRequirement's own `craft` argument, and it does not work:
    /// Hud.UpdateBuild passes <c>piece.FreeBuildKey() == GlobalKeys.NoCraftCost</c>, which is
    /// TRUE for any piece carrying an ItemDrop or a Feast, so the build HUD calls it with
    /// craft:true for item stands and feasts.
    ///
    /// Opened with a prefix and closed with a FINALIZER rather than a postfix. A postfix does
    /// not run when the original throws, and a bracket that leaks open once turns the two
    /// Inventory patches on for the rest of the session - at which point a chest six feet
    /// away is contributing to your carry weight. A void finalizer always runs and does not
    /// touch the exception on its way past.
    ///
    /// ------------------------------------------------------------------------------------
    ///
    /// <b>Where is not a parameter here, and that is the one structural change the fold
    /// made.</b> Hirsla passed <c>Player.m_localPlayer.transform.position</c> from nine call
    /// sites, with a comment on the chest cache saying it was safe only because every caller
    /// happened to pass the same point - an invariant maintained by eye, whose failure is the
    /// worst one this feature has. Every one of those nine now asks <see cref="HodChests"/>
    /// with no point at all, and HodChests asks <see cref="HodScope"/>, which resolves the
    /// post once a frame. A new caller cannot pass the wrong centre because there is nowhere
    /// to pass one. The centre is the stowing post carrying the hod jib, not the player; see
    /// HodScope for why a fixed thing in the world is the right origin for a service that was
    /// bought with a piece.
    ///
    /// ------------------------------------------------------------------------------------
    ///
    /// <b>Display is optimistic; payment is authoritative.</b> That sentence is the whole of
    /// the multiplayer design, and everything below is arranged around it.
    ///
    /// The counting patches stay optimistic and that is correct rather than a compromise:
    /// InventoryGui.UpdateRecipeList asks HaveRequirements for every recipe in the game once a
    /// frame, so the numbers on screen cannot round-trip to anybody, and what they report is
    /// what this client's replica of each chest says. A replica is refreshed by vanilla once a
    /// second, so a number can be a second stale. It always could be.
    ///
    /// What may NOT be optimistic is the moment material is spent. Nothing here ever writes a
    /// container this client does not own, so at the instant of payment the only stock that
    /// counts is
    ///
    ///   what the player is carrying, plus
    ///   what is in a reachable chest whose ZDO this client owns
    ///
    /// - "spendable", as against "countable". Anything else has to be brought into the pack
    /// first, by asking the owner (see HodWithdraw), and paid for out of the pack afterwards.
    ///
    /// That question - <b>when does the round trip fit?</b> - is why the service is the
    /// crafting panel and nothing else. It fits here and it fits nowhere else in the game.
    /// InventoryGui.OnCraftPressed sets m_craftTimer to 0 and DoCrafting runs when it reaches
    /// m_craftDuration - two seconds, less up to 60% for skill, so never below 0.8s. The
    /// prefetch is fired on the press and the material is really in the pack before
    /// ConsumeResources is reached, which leaves that method untouched and honest.
    ///
    /// Building had no such timer: Player.UpdatePlacement asks HaveRequirements, calls
    /// TryPlacePiece and calls ConsumeResources in ONE frame on a click, so paying for a wall
    /// out of somebody else's chest meant refusing the first click. That path was removed
    /// rather than fixed, and the stutter went with it.
    ///
    /// The spend is still verified, in CraftStart, because a prefetch can come back short and
    /// a chest can be emptied by somebody else during the craft timer. A craft that has been
    /// promised and cannot be paid for declines with a message rather than happening for free.
    /// </summary>
    internal static class HodCrafting
    {
        private static int _depth;

        /// <summary>InventoryGui.DoCrafting - the spend. Where material really moves.</summary>
        private static int _crafting;

        /// <summary>InventoryGui.OnCraftPressed - the button, one craft timer earlier.</summary>
        private static int _pressing;

        /// <summary>
        /// A counter and not a bool, which it used to be.
        ///
        /// Two separate blocks raise this - the honest carried count below, and the fetch in
        /// the one-ingredient path - and a bool means an inner block's finally clears the
        /// outer block's suspension rather than restoring it. The fetch is the re-entrant one:
        /// HodChests.MoveFromOwned calls Inventory.AddItem, which ends in Changed() and
        /// reaches Player.OnInventoryChanged, which walks every recipe and every piece in the
        /// game. Nothing on that path calls CountItems today, so the bug was latent rather
        /// than live - but the failure it would produce is the exact one this whole file
        /// exists to prevent: the honest count silently becomes the chest-inflated count, the
        /// shortfall computes as zero, nothing is taken from the chest, and vanilla's void
        /// RemoveItem quietly underpays the recipe. A counter costs one character and removes
        /// the class of bug.
        /// </summary>
        private static int _suspend;

        /// <summary>
        /// Whether the game is asking a crafting question right now.
        ///
        /// Internal rather than private, and read by <see cref="HodRequirement"/>. The bracket
        /// is opened only by the crafting panel's own methods, so "is the bracket open" and
        /// "is this the crafting panel rather than the build HUD" are the same fact - which is
        /// how the requirement line tells the two apart without trusting SetupRequirement's
        /// `craft` argument, whose meaning is not what its name suggests. See the class
        /// docstring.
        ///
        /// <b>It deliberately does NOT ask whether the scope is open.</b> "Am I in the
        /// crafting panel" and "is there a jib-bearing post in reach" are two different
        /// questions and folding them together would quietly change what InScope means for its
        /// other reader. There is nothing to gain by it either: with no post in reach
        /// HodChests.Near returns an empty list, every count adds zero and every take returns
        /// zero, so the patches below are already no-ops by arithmetic rather than by a flag.
        /// </summary>
        internal static bool InScope
        {
            get { return _depth > 0 && _suspend == 0 && HodConfig.Enabled.Value; }
        }

        private static bool IsPlayerInventory(Inventory inventory)
        {
            var player = Player.m_localPlayer;
            return player != null && ReferenceEquals(player.GetInventory(), inventory);
        }

        // ------------------------------------------------------------------ scope brackets

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        private static void RecipeStart() { _depth++; }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), "HaveRequirementItems")]
        private static void RecipeEnd() { if (_depth > 0) _depth--; }

        /// <summary>
        /// The spend, for a craft - and, unavoidably, for a placement too.
        ///
        /// ConsumeResources is one public method with two callers: InventoryGui.DoCrafting and
        /// Player.UpdatePlacement. Bracketing it therefore opens the scope during a build as
        /// well, and that costs nothing in ordinary play. UpdatePlacement gates on
        /// HaveRequirements(piece, CanBuild) - vanilla, because the Piece overload is not
        /// bracketed - and then calls ConsumeResources in the same frame. A placement that
        /// gets that far is one the pack can already pay for, so TakeFromChests computes a
        /// shortfall of zero and returns before it looks at a chest.
        ///
        /// That is not the whole of it. The vanilla line is
        /// <c>if (m_noPlacementCost || HaveRequirements(...))</c>, so the noplacementcost cheat
        /// bypasses the gate and still reaches ConsumeResources - a shortfall arrives, and
        /// before the narrowing the chests paid it. TakeFromChests returns on NoCostCheat,
        /// which is one branch on the spend rather than a second flag on the bracket.
        /// Narrowing the bracket by caller was considered and rejected again: it would need a
        /// counter kept in step with this one to prevent the same one case.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static void ConsumeStart() { _depth++; }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
        private static void ConsumeEnd() { if (_depth > 0) _depth--; }

        /// <summary>
        /// The crafting panel's requirement lines, and only those.
        ///
        /// SetupRequirementList is private, is called from exactly one place - the panel's own
        /// UpdateRecipe - and calls SetupRequirement once per line with craft:true. Bracketing
        /// it rather than SetupRequirement itself is what keeps the build HUD out: that method
        /// is public static and draws both panels. See the class docstring for why its `craft`
        /// argument cannot be used to tell them apart.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "SetupRequirementList")]
        private static void RequirementLinesStart() { _depth++; }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), "SetupRequirementList")]
        private static void RequirementLinesEnd() { if (_depth > 0) _depth--; }

        /// <summary>
        /// The second bracket, and a narrower one: the moment a craft is actually being
        /// pressed and carried out.
        ///
        /// It exists for the require-one-ingredient path below, which must not fire from
        /// anything except a real craft. Recipe.GetAmount's four-argument overload has exactly
        /// two callers in the shipped game and both are here, but another mod calling it to
        /// ask a question would otherwise have items moved out of a chest into somebody's pack
        /// for having asked.
        ///
        /// The two callers are counted SEPARATELY, and that is not tidiness. Pressing the
        /// craft button is not spending anything: OnCraftPressed calls GetAmount, throws away
        /// both of its out parameters and uses only the return value, for one CanAddItem check
        /// and the craft-start effect. It then sets m_craftTimer and DoCrafting arrives a
        /// second later. OnCraftCancelPressed in between does nothing but set that timer to -1.
        ///
        /// So moving material at press time meant a press-then-cancel left it stranded in the
        /// pack for a craft that never happened. The press now PREDICTS the number without
        /// touching a chest, and DoCrafting - which really is the spend - does the moving.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
        private static void CraftPressStart() { _pressing++; }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
        private static void CraftPressEnd() { if (_pressing > 0) _pressing--; }

        /// <summary>
        /// The spend, and the last moment anything can be refused.
        ///
        /// It opens the crafting bracket and carries the authoritative check. The prefetch
        /// fired on the button press has had the craft timer to land in; if it did not land,
        /// or came back short, or somebody emptied the chest in the meantime, then the recipe
        /// the panel showed as available cannot actually be paid for. Letting vanilla run at
        /// that point would reach ConsumeResources, whose RemoveItem(string, ...) returns void
        /// and stops quietly when it runs out - a sword made for less than it cost, with
        /// nothing anywhere to say so.
        ///
        /// A prefix returning false, and it is the ONLY prefix on DoCrafting, so nothing else
        /// is skipped by the refusal. CraftEnd is a finalizer and runs either way, so the
        /// bracket cannot leak open.
        ///
        /// The message is ShortMessage from the config, and its default is deliberately NOT
        /// "$msg_missingrequirement". That token is what vanilla says for "you simply do not
        /// have this", and hearing it two seconds after the requirement line drew the chest
        /// total in blue reads as the mod having broken rather than as an explanation - which
        /// is the one failure the display-versus-payment split exists to avoid. Losing the
        /// token costs its translations, which is a smaller loss than a message that cannot be
        /// told from a bug, and the config comment says how to get it back.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static bool CraftStart(Player player, Recipe ___m_craftRecipe,
            ItemDrop.ItemData ___m_craftUpgradeItem, bool ___m_multiCrafting,
            int ___m_multiCraftAmount)
        {
            _crafting++;

            if (!HodConfig.Enabled.Value) return true;
            if (player == null || player != Player.m_localPlayer) return true;
            if (___m_craftRecipe == null) return true;

            // <b>Deliberately NOT gated on HodScope.IsOpen, and this is the one place in the
            // file where that guard looks obviously right and is wrong.</b> The scope is
            // resolved per frame from where the player is standing, and a craft takes a timer:
            // a recipe can be un-greyed at a post's bench on one frame and pressed on a frame
            // where the post has been torn down, the player has stepped out of range, or the
            // jib has been broken. Skipping the check on those frames is precisely the case it
            // exists for - the panel promised chest stock that is no longer countable, the
            // chests contribute nothing to the spend, and vanilla's void RemoveItem underpays
            // the recipe in silence.
            //
            // Running it away from a post costs a walk of the recipe's requirements once per
            // craft press, and it cannot refuse anything vanilla would have allowed: with the
            // scope shut, Spendable is the pack and nothing else, asked over the same quality
            // levels as Player.HaveRequirementItems.
            //
            // Cheats and the world's own free-craft key both skip ConsumeResources entirely,
            // so there is nothing to be short of.
            if (player.NoCostCheat()) return true;

            var zone = ZoneSystem.instance;
            if (zone != null && zone.GetGlobalKey(GlobalKeys.NoCraftCost)) return true;

            var quality = ___m_craftUpgradeItem == null
                ? 1
                : ___m_craftUpgradeItem.m_quality + 1;

            var multiplier = ___m_multiCrafting ? ___m_multiCraftAmount : 1;

            if (CanPayFor(player, ___m_craftRecipe, quality, multiplier)) return true;

            // Empty means the player asked for silence. The craft is still refused - the
            // setting is about the line, not about the rule.
            if (!string.IsNullOrEmpty(HodConfig.ShortMessage.Value))
                player.Message(MessageHud.MessageType.Center, HodConfig.ShortMessage.Value);

            if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo(
                    "Refused the craft of " + ___m_craftRecipe.name + ": the post's chests "
                    + "could not supply it by the time it had to be paid for.");

            return false;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
        private static void CraftEnd() { if (_crafting > 0) _crafting--; }

        /// <summary>
        /// The button, one craft timer before the spend. Where the fetching starts.
        ///
        /// A postfix rather than the CraftPressStart prefix above, and gated on the timer:
        /// OnCraftPressed returns early with "$inventory_full" without starting anything, and
        /// m_craftTimer only leaves -1 on a press that was accepted. Fetching for a press that
        /// was refused would drain a chest into a pack for a craft that never begins.
        ///
        /// Nothing here promises anything. The requests go out, the timer runs, and whatever
        /// arrived is what CraftStart above measures when the timer expires.
        ///
        /// <b>A press that is then cancelled leaves the fetched material in the pack</b>, and
        /// that is a real cost rather than an oversight - the one-ingredient path further down
        /// was rewritten specifically to stop doing this and it is back here for the network
        /// case. InventoryGui.OnCraftCancelPressed sets m_craftTimer to -1 and does nothing
        /// else; Hide() does the same on closing the panel; dying does it by closing the
        /// panel. In every one of them the owner has already taken the material out of its
        /// chest and sent it, so it lands in the pack for a craft that never happened.
        ///
        /// It is not undone, and the reason is the shape of the alternative rather than the
        /// difficulty. Handing it back means a second request in the other direction, a record
        /// of what each press fetched so the return knows what to send, and an answer to what
        /// happens when the return itself is not answered - a transaction log, for a
        /// convenience feature, to save the player walking six feet. What the material
        /// actually does in the meantime is sit in the pack, which is where material a player
        /// asked for belongs.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
        private static void FetchForCraft(float ___m_craftTimer, Recipe ___m_craftRecipe,
            ItemDrop.ItemData ___m_craftUpgradeItem, bool ___m_multiCrafting,
            int ___m_multiCraftAmount)
        {
            if (!HodConfig.Enabled.Value) return;

            // Purely an optimisation, unlike the two guards above it in this file. Everything
            // below ends in Ask, which walks HodChests.Near - an empty list when the scope is
            // shut - so without this line the answer is identical and the cost is one walk of
            // the recipe per press. It is safe to short-circuit here precisely because the
            // only thing skipped is asking somebody else for material, and nothing has been
            // promised on the strength of a request that was never sent.
            if (!HodScope.IsOpen) return;

            if (___m_craftTimer < 0f || ___m_craftRecipe == null) return;

            var player = Player.m_localPlayer;
            if (player == null || player.GetInventory() == null) return;

            var quality = ___m_craftUpgradeItem == null
                ? 1
                : ___m_craftUpgradeItem.m_quality + 1;

            var multiplier = ___m_multiCrafting ? ___m_multiCraftAmount : 1;

            _suspend++;
            try
            {
                if (___m_craftRecipe.m_requireOnlyOneIngredient)
                    FetchOneIngredient(player, ___m_craftRecipe, quality, multiplier);
                else
                    FetchEveryIngredient(player, ___m_craftRecipe, quality, multiplier);
            }
            finally
            {
                _suspend--;
            }
        }

        // ------------------------------------------------------------------ counting

        /// <summary>
        /// Adds what the post's chests hold to what you are carrying.
        ///
        /// matchWorldLevel is accepted and forwarded rather than ignored. It is a THIRD
        /// parameter this overload gained, defaulting to true, and it decides whether an item
        /// from a lower world level counts at all. Dropping it would have Ashlands items
        /// counted one way in the chest and another way in the pack, which is a count and a
        /// consume disagreeing - the free-craft failure this whole file is arranged to
        /// prevent.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.CountItems))]
        private static void CountChestItems(Inventory __instance, ref int __result,
            string name, int quality, bool matchWorldLevel)
        {
            if (!InScope || !IsPlayerInventory(__instance)) return;

            __result += HodChests.CountAllowed(name, quality, matchWorldLevel);
        }

        /// <summary>
        /// The other question the game asks, and the one Tether never answered.
        ///
        /// PieceTable's hide-unavailable filter runs HaveRequirements in
        /// RequirementMode.CanAlmostBuild, and that mode routes to Inventory.HaveItem, not to
        /// CountItems.
        ///
        /// <b>That caller is gone and this patch is now dormant, and it is kept anyway.</b>
        /// CanAlmostBuild is only reached through HaveRequirements(Piece, mode), which is not
        /// bracketed, so nothing inside the scope asks HaveItem today: HaveRequirementItems
        /// and SetupRequirement both count, and ConsumeResources only removes. Deleting it
        /// would be deleting the answer rather than the question. The invariant this whole
        /// file rests on is that a count and a consume inside one bracket never disagree, and
        /// a chest-aware CountItems sitting beside a pack-only HaveItem is exactly that
        /// disagreement, waiting for whichever vanilla version or sibling mod routes a
        /// crafting check through the other method. It costs one branch on a call the scope
        /// already gates.
        ///
        /// The string overload explicitly. There is an ItemType overload beside it that this
        /// must not touch: it answers "do you have any weapon", and a chest full of them is
        /// not the same as a weapon in your hands.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.HaveItem), typeof(string), typeof(bool))]
        private static void HaveChestItem(Inventory __instance, ref bool __result,
            string name, bool matchWorldLevel)
        {
            if (__result || !InScope || !IsPlayerInventory(__instance)) return;

            __result = HodChests.HasAllowed(name, matchWorldLevel);
        }

        // ------------------------------------------------------------------ consuming

        /// <summary>
        /// Takes the shortfall out of the post's chests before the game removes what it can
        /// from the player, then trims the amount so vanilla only removes what is actually
        /// there.
        ///
        /// The trim is not tidiness. This overload returns VOID and stops quietly when it runs
        /// out of stock, so without the trim a recipe whose material came from a chest would
        /// be built for less than it cost and nothing anywhere would say so.
        ///
        /// The chests are walked in exactly the order the counting patch walked them, over
        /// exactly the same frame-cached list, with exactly the same per-stack gate. That is
        /// the whole guarantee: if the count summed a chest this loop skips, HaveRequirements
        /// has already said yes on stock that is never spent. Neither half takes a position
        /// any more, so they cannot even be asked about different spheres - see HodScope.
        ///
        /// Only chests this client OWNS are touched here, and there is no fallback for the
        /// others. HodChests.TakeFromOwned answers zero for anything else, and this method is
        /// synchronous - ConsumeResources expects the material gone when it returns, so there
        /// is nowhere in it to wait for a network reply. Everything remote had to be brought
        /// into the pack before this ran, by the craft-press prefetch, and CraftStart verifies
        /// that it arrived before letting the spend happen. Anything the loop cannot cover is
        /// therefore left in the amount vanilla removes, which under-removes rather than
        /// over-removes - and the craft it belongs to has already been refused.
        ///
        /// It also runs during a placement, because ConsumeResources is the build spend as
        /// well. Nothing happens there: the shortfall below is zero, since vanilla only
        /// reaches this line for a piece the pack could already pay for - except under the
        /// noplacementcost cheat, which skips that gate and keeps the spend, and which the
        /// first lines of the body refuse for exactly that reason. See ConsumeStart.
        ///
        /// <b>The show flight is started from here and nowhere else</b>, because this is the
        /// one place in the feature where material really leaves a chest for a craft. It is
        /// started AFTER the spend, out of a list of positions and prefab names, and the craft
        /// has already been paid for correctly by the time HodShow sees anything. See HodShow
        /// for why that ordering is what makes it impossible for a spirit to carry something.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem),
            typeof(string), typeof(int), typeof(int), typeof(bool))]
        private static void TakeFromChests(Inventory __instance, string name, ref int amount,
            int itemQuality, bool worldLevelBased)
        {
            if (!InScope || !IsPlayerInventory(__instance)) return;
            if (amount <= 0) return;

            // The one placement that can reach this line with an unpaid shortfall, and the
            // reason it is a check here rather than an argument in a comment. UpdatePlacement
            // reads `if (m_noPlacementCost || HaveRequirements(piece, CanBuild))` and then
            // calls ConsumeResources anyway (Player.cs:1160 and 1167, assembly_valheim
            // 0.221.12) - the cheat short-circuits the gate but not the spend. So with
            // noplacementcost on, a wall placed with an empty pack arrived here with a real
            // shortfall and the loop below emptied a chest to pay for a build the cheat had
            // already declared free, silently, and against the one thing this feature promises
            // absolutely: it is the crafting panel and nothing else.
            //
            // Costs the craft path nothing: DoCrafting only calls ConsumeResources inside
            // `if (!player.NoCostCheat() && !GetGlobalKey(NoCraftCost))` (InventoryGui.cs:1555
            // and 1564), and CraftStart returns early on the same flag, so under the cheat
            // there is no craft spend to miss.
            if (Player.m_localPlayer.NoCostCheat()) return;

            var carried = CarriedOnly(__instance, name, itemQuality, worldLevelBased);
            var shortfall = amount - carried;
            if (shortfall <= 0) return;

            var chests = HodChests.Near();

            // Allocated after the shortfall check, so an ordinary craft paid for out of the
            // pack - and every placement - allocates nothing at all. Null when the flight is
            // switched off, which TakeFromOwned reads as "do not bother recording".
            var spent = Watching() ? new List<HodShow.Spent>() : null;

            for (var i = 0; i < chests.Count && shortfall > 0; i++)
            {
                var container = chests[i];
                if (container == null) continue;

                shortfall -= HodChests.TakeFromOwned(
                    container, name, itemQuality, worldLevelBased, shortfall, null, spent);
            }

            // What vanilla should still take is everything the chests did not cover, which
            // works out to the carried stock plus any shortfall they could not meet. When the
            // chests covered it all that is exactly the carried stock; when they had nothing
            // it is the original amount, unchanged.
            amount = carried + shortfall;

            // Last, and after the amount is settled, so that a spirit is never launched for a
            // spend the lines above had not finished accounting for.
            //
            // <b>That ordering is not on its own enough, and the other half is inside
            // HodShow.Send.</b> This is a PREFIX: an exception escaping it means vanilla's own
            // RemoveItem never runs, so the chests would already be lighter while the pack was
            // not - a visual effect causing a real loss of material. Send therefore swallows
            // everything it can throw. Both halves are needed; neither alone is a guarantee.
            HodShow.Send(spent);
        }

        /// <summary>
        /// Whether a spend should be recorded for the spirit to re-enact.
        ///
        /// Asked here as well as inside HodShow.Send because the list is what costs something:
        /// Send can refuse cheaply, but the entries are gathered inside the removal loop and
        /// there is no reason to build them for a player who has turned the flight off.
        /// </summary>
        private static bool Watching()
        {
            return HodConfig.Enabled.Value && HodConfig.ShowFlight.Value;
        }

        /// <summary>
        /// The genuine carried count, with the counting patch held off.
        ///
        /// Needed because the shortfall calculation runs inside an open scope, where
        /// CountItems already answers with the chests included - asking there would report
        /// that nothing is missing and take nothing out of the chest.
        /// </summary>
        internal static int CarriedOnly(Inventory inventory, string name, int quality,
                                        bool matchWorldLevel)
        {
            _suspend++;
            try { return inventory.CountItems(name, quality, matchWorldLevel); }
            finally { _suspend--; }
        }

        // ------------------------------------------------------------ spendable and asking

        /// <summary>
        /// What can be paid with right now: the pack, plus the chests this client owns.
        ///
        /// The distinction between this and HodChests.CountAllowed is the entire multiplayer
        /// design in one line. See the class docstring - display is optimistic, payment is
        /// authoritative, and this is the authoritative half.
        /// </summary>
        private static int Spendable(Player player, string name, int quality)
        {
            return CarriedOnly(player.GetInventory(), name, quality, true)
                   + HodChests.CountSpendable(name, quality, true);
        }

        /// <summary>
        /// Asks the owners of the reachable chests we do NOT own to send this material over.
        /// Returns whether anything went out.
        ///
        /// Each chest is asked for at most what this client's replica says it holds. That
        /// replica can be a second stale, so the sum of the requests can exceed what really
        /// exists - which is fine and is the point of the owner-side clamp: every owner
        /// answers with what it really had, and the requester credits only the answers.
        ///
        /// HodWithdraw.Ask refuses a duplicate of a request already in flight, so calling this
        /// again on the next frame's press does not drain a chest twice for one craft.
        /// </summary>
        private static bool Ask(Player player, string name, int quality, int shortfall)
        {
            if (shortfall <= 0) return false;

            var pack = player.GetInventory();
            var chests = HodChests.Near();
            var asked = false;

            for (var i = 0; i < chests.Count && shortfall > 0; i++)
            {
                var container = chests[i];
                if (container == null || HodChests.Owned(container)) continue;

                var held = HodChests.CountAllowed(container, name, quality, true);
                if (held <= 0) continue;

                var want = Mathf.Min(held, shortfall);

                if (!HodWithdraw.Ask(container, pack, name, quality, true, want)) continue;

                asked = true;
                shortfall -= want;
            }

            return asked;
        }

        /// <summary>
        /// Whether every requirement of this recipe can be paid for out of spendable stock.
        ///
        /// The per-quality maximum, because that is exactly how Player.HaveRequirementItems
        /// asks it - take the largest single level, three at quality 1 and three at quality 2
        /// satisfying nothing that wants five. ConsumeResources then removes at quality -1,
        /// which is every level summed, so a check that passes here certainly finds enough
        /// there.
        ///
        /// A require-one-ingredient recipe needs ONE requirement covered rather than all of
        /// them, which is what the flag means and what GetFirstRequiredItem implements.
        ///
        /// The two gates start their quality loop at a DIFFERENT number and it is not a typo
        /// in either of them. HaveRequirementItems, which is what an ordinary recipe is judged
        /// by, runs 1 to maxQuality. GetFirstRequiredItem, which is what a one-ingredient
        /// recipe is judged by, runs 0 to maxQuality - and quality 0 is a real level that some
        /// items sit at. Starting both at 1 would refuse a one-ingredient craft vanilla was
        /// about to allow, which reads as the mod having eaten the button.
        /// </summary>
        private static bool CanPayFor(Player player, Recipe recipe, int quality, int multiplier)
        {
            var one = recipe.m_requireOnlyOneIngredient;
            var from = one ? 0 : 1;

            foreach (var requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null) continue;

                var needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;

                var data = requirement.m_resItem.m_itemData;
                var covered = false;

                var best = 0;

                for (var q = from; q <= data.m_shared.m_maxQuality && !covered; q++)
                {
                    var spendable = Spendable(player, data.m_shared.m_name, q);
                    if (spendable > best) best = spendable;

                    covered = spendable >= needed;
                }

                if (one && covered) return true;

                if (!one && !covered)
                {
                    // The numbers, not just the verdict. This refusal is the one place the
                    // panel and the payment can disagree, and "the chests could not supply
                    // it" beside a chest the player can see the contents of is the least
                    // useful true sentence in the mod. Which requirement, how much was
                    // wanted, what the pack held and what the owned chests held separates a
                    // scope problem from an ownership one from a quality one without
                    // another build.
                    if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                        GrovePlugin.Log.LogInfo(
                            "Hod cannot pay for " + data.m_shared.m_name + ": needs " + needed
                            + ", best spendable across qualities " + from + ".."
                            + data.m_shared.m_maxQuality + " was " + best
                            + " (carried " + CarriedOnly(player.GetInventory(),
                                                         data.m_shared.m_name, 1, true)
                            + ", owned chests " + HodChests.CountSpendable(
                                                         data.m_shared.m_name, 1, true)
                            + ", all chests in reach " + HodChests.CountAllowed(
                                                         data.m_shared.m_name, 1, true)
                            + ", chests in reach " + HodChests.Near().Count + ").");

                    return false;
                }
            }

            // An all-ingredients recipe that never failed is paid for; a one-ingredient recipe
            // that never succeeded is not. Vanilla's own HaveRequirementItems ends exactly here
            // with exactly this asymmetry.
            return !one;
        }

        /// <summary>Fires a fetch for every requirement the pack and owned chests cannot cover.</summary>
        private static void FetchEveryIngredient(Player player, Recipe recipe, int quality,
                                                 int multiplier)
        {
            foreach (var requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null) continue;

                var needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;

                var data = requirement.m_resItem.m_itemData;
                var name = data.m_shared.m_name;

                // Per quality level, and the level that is already closest to covering it, so
                // the fetch tops up the same stack the gate is going to measure - vanilla's
                // HaveRequirementItems takes the MAXIMUM across levels rather than the sum, so
                // spreading a fetch over two levels satisfies neither. Materials sit at one
                // quality in vanilla, so in practice this is one pass.
                var best = -1;
                var bestQuality = 1;

                for (var q = 1; q <= data.m_shared.m_maxQuality; q++)
                {
                    var have = Spendable(player, name, q);
                    if (have <= best) continue;

                    best = have;
                    bestQuality = q;
                }

                if (best < 0 || best >= needed) continue;

                Ask(player, name, bestQuality, needed - best);
            }
        }

        /// <summary>
        /// The same for a recipe that wants one ingredient of its choosing.
        ///
        /// The walk is GetFirstRequiredItem's order - requirements in declaration order, then
        /// quality 0 upward - so the material fetched is the material vanilla is going to pick
        /// when the timer expires. Fetching a different one would leave both in the pack and
        /// craft with the wrong one.
        ///
        /// It stops at the first requirement the chests could cover at all, countable rather
        /// than spendable, because the point of asking is to turn countable into spendable.
        /// </summary>
        private static void FetchOneIngredient(Player player, Recipe recipe, int quality,
                                               int multiplier)
        {
            foreach (var requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null) continue;

                var needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;

                var data = requirement.m_resItem.m_itemData;
                var name = data.m_shared.m_name;

                for (var q = 0; q <= data.m_shared.m_maxQuality; q++)
                {
                    var spendable = Spendable(player, name, q);
                    if (spendable >= needed) return;

                    var countable = CarriedOnly(player.GetInventory(), name, q, true)
                                    + HodChests.CountAllowed(name, q, true);

                    if (countable < needed) continue;

                    Ask(player, name, q, needed - spendable);
                    return;
                }
            }
        }

        // ------------------------------------------------------ the one-ingredient recipes

        /// <summary>
        /// Recipes marked m_requireOnlyOneIngredient do not go through ConsumeResources at
        /// all, and faking their count is a free craft and then a crash.
        ///
        /// What vanilla actually does. InventoryGui.DoCrafting asks HaveRequirements, which
        /// opens the bracket above and answers yes on chest stock; it then spends the material
        /// by calling player.GetInventory().RemoveItem(name, need, quality) DIRECTLY, with the
        /// bracket long since closed. Nothing is taken from the chest, RemoveItem returns void
        /// and stops quietly, and the item is crafted for nothing.
        ///
        /// Worse than that, it does not get as far as the free craft. Recipe.GetAmount reaches
        /// Player.GetFirstRequiredItem, which counts against m_inventory - the count this
        /// feature inflates - and then RETURNS inventory.GetItem(...), which is null when the
        /// player carries none of it. GetAmount immediately dereferences
        /// singleReqItem.m_quality with no null guard, so the first press throws a
        /// NullReferenceException inside OnCraftPressed. That exception lands in
        /// AppData/LocalLow/IronGate/Valheim/Player.log and NOT in BepInEx's LogOutput, so
        /// from the usual log it looks like the button did nothing at all.
        ///
        /// So this path does not fake anything. It physically moves the material out of the
        /// chest and into the pack a moment before vanilla looks, and every vanilla check,
        /// message, effect and removal afterwards runs untouched on real items. It is the only
        /// place in the feature that does that.
        ///
        /// Recipe.GetAmount and not GetFirstRequiredItem, although the null is born in the
        /// latter: GetAmount is the frame that dereferences it, so it is the only place a
        /// guard can stand. The four-argument overload has exactly two callers in the shipped
        /// game, DoCrafting and OnCraftPressed, and the two brackets confine it to those.
        ///
        /// Those two callers are handled differently and it matters which is which.
        ///
        ///   DoCrafting      the spend. Material is moved out of the chest here, because the
        ///                   very next thing vanilla does with it is remove it.
        ///   OnCraftPressed  the button. Everything it does with GetAmount is throw away both
        ///                   out parameters and check whether the RESULT will fit in the pack.
        ///                   So the number is predicted from what the chests hold and nothing
        ///                   moves - a press followed by a cancel then leaves nothing behind,
        ///                   where moving at press time drained a recipe's worth of material
        ///                   into the pack for a craft that never happened.
        ///
        /// The prediction is not optional there either: leaving vanilla to run would hand
        /// GetFirstRequiredItem's null straight to the dereference, which is the crash this
        /// whole path exists to prevent.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Recipe), nameof(Recipe.GetAmount))]
        private static bool MoveBeforeSingleIngredientCraft(Recipe __instance, int quality,
            ref int need, ref ItemDrop.ItemData singleReqItem, int craftMultiplier,
            ref int __result)
        {
            if (_crafting <= 0 && _pressing <= 0) return true;
            if (!HodConfig.Enabled.Value) return true;
            if (__instance == null || !__instance.m_requireOnlyOneIngredient) return true;

            // Not gated on HodScope.IsOpen either, and for the sharper half of the same
            // reason. The scope closing between the panel drawing and the button being pressed
            // is the exact state this guard exists for: the list un-greyed the recipe on chest
            // stock, the player stepped out of range, and GetFirstRequiredItem now finds
            // nothing in the pack and returns null - which GetAmount dereferences with no
            // guard, throwing into Player.log where the usual log shows only a button that did
            // nothing. Standing aside on those frames would hand the crash straight back.
            //
            // Away from a post it costs one walk of the requirements and then returns true on
            // the first line of Carrying, because with the scope shut the pack's count is
            // honest and vanilla can answer for itself.

            var player = Player.m_localPlayer;
            if (player == null || player.GetInventory() == null) return true;

            // Honest counts throughout. Everything below asks what the player is really
            // carrying, and the whole point is that the answer must not already include the
            // chest.
            _suspend++;
            try
            {
                // Already carrying enough of something: vanilla answers correctly on its own
                // and this path has nothing to add, in either bracket.
                if (Carrying(player, __instance, quality, craftMultiplier)) return true;

                if (_crafting > 0 && Fetch(player, __instance, quality, craftMultiplier))
                    return true;

                if (_crafting <= 0)
                {
                    int predicted;
                    if (Predict(player, __instance, quality, craftMultiplier, out predicted))
                    {
                        // singleReqItem stays null and need stays zero, both of which
                        // OnCraftPressed discards. Only the result is real, and it is the
                        // number CanAddItem is about to be asked about.
                        need = 0;
                        singleReqItem = null;
                        __result = predicted;
                        return false;
                    }
                }
            }
            finally
            {
                _suspend--;
            }

            // Nothing in reach could supply it - the chest was emptied by somebody else
            // between the panel drawing and the button being pressed, or the pack is full.
            // Vanilla would now dereference a null and throw, so the guard is to answer the
            // way a recipe with no single ingredient answers. DoCrafting's own
            // "m_requireOnlyOneIngredient && singleReqItem == null" line then returns without
            // crafting, which is the correct outcome and a quiet one.
            need = 0;
            singleReqItem = null;
            __result = __instance.m_amount * craftMultiplier;

            if (HodConfig.Verbose.Value && GrovePlugin.Log != null)
                GrovePlugin.Log.LogInfo(
                    "No chest around the post could supply " + __instance.name
                    + "; the craft was refused rather than allowed to throw.");

            return false;
        }

        /// <summary>
        /// Whether the player already carries enough of any one ingredient, asked exactly the
        /// way GetFirstRequiredItem asks it.
        ///
        /// Quality levels 0 through m_maxQuality INCLUSIVE, because that is vanilla's loop.
        /// Materials sit at quality 1 and level 0 is a real level that some items use, so
        /// starting at 1 would miss them.
        /// </summary>
        private static bool Carrying(Player player, Recipe recipe, int quality, int multiplier)
        {
            var pack = player.GetInventory();

            foreach (var requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null) continue;

                var needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;

                var data = requirement.m_resItem.m_itemData;

                for (var q = 0; q <= data.m_shared.m_maxQuality; q++)
                    if (pack.CountItems(data.m_shared.m_name, q) >= needed) return true;
            }

            return false;
        }

        /// <summary>
        /// The number vanilla's GetAmount would have returned if the chest's material were
        /// already in the pack - worked out without moving any of it.
        ///
        /// For the press, which is not the spend. Vanilla's arithmetic is
        /// <c>m_amount + ceil((q - 1) * m_amount * m_qualityResultAmountMultiplier) +
        /// m_extraAmountOnlyOneIngredient</c>, times the craft multiplier, where q is the
        /// quality of the stack GetFirstRequiredItem picked. It is copied here rather than
        /// approximated with m_amount alone, because the number feeds OnCraftPressed's
        /// CanAddItem check: under-report it and the craft starts, the timer runs, and
        /// DoCrafting's own capacity check silently refuses a second later. An immediate
        /// "$inventory_full" is the honest answer and this is what buys it.
        ///
        /// The search walks requirements and quality levels in exactly GetFirstRequiredItem's
        /// order, so the stack this predicts from is the stack Fetch will go after when
        /// DoCrafting arrives. Pack and chest are added together, because by then they will be
        /// the same pile.
        /// </summary>
        private static bool Predict(Player player, Recipe recipe, int quality, int multiplier,
                                    out int amount)
        {
            amount = 0;

            var pack = player.GetInventory();

            foreach (var requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null) continue;

                var needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;

                var data = requirement.m_resItem.m_itemData;
                var name = data.m_shared.m_name;

                for (var q = 0; q <= data.m_shared.m_maxQuality; q++)
                {
                    var available = pack.CountItems(name, q)
                                    + HodChests.CountAllowed(name, q, true);

                    if (available < needed) continue;

                    amount = (recipe.m_amount
                              + Mathf.CeilToInt((q - 1) * recipe.m_amount
                                                * recipe.m_qualityResultAmountMultiplier)
                              + requirement.m_extraAmountOnlyOneIngredient) * multiplier;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Moves enough of one ingredient out of the post's chests to make the craft real.
        ///
        /// Per quality level rather than in aggregate, because vanilla's own lookup is per
        /// quality level: three of something at quality 1 and two at quality 2 satisfies
        /// nothing that wants five.
        ///
        /// Owned chests only, and it does not need to be anything else. This runs inside
        /// DoCrafting, which CraftStart has already refused unless spendable stock covers one
        /// ingredient - and spendable means the pack or a chest this client owns. Anything
        /// that had to come across the network was fetched a craft timer ago by
        /// FetchOneIngredient.
        ///
        /// The show flight is raised from here as well as from TakeFromChests, and it is the
        /// same event seen from a different angle: on this path the material leaves the chest
        /// for the pack and vanilla removes it from the pack a few lines later, so a chest a
        /// spirit did not fly from would be a chest that visibly emptied for a craft. The list
        /// is handed to MoveFromOwned, which records the prefab name while the stack is still
        /// whole - a string and a position, and no route back to an item. See HodShow.
        /// </summary>
        private static bool Fetch(Player player, Recipe recipe, int quality, int multiplier)
        {
            var pack = player.GetInventory();

            foreach (var requirement in recipe.m_resources)
            {
                if (requirement == null || requirement.m_resItem == null) continue;

                var needed = requirement.GetAmount(quality) * multiplier;
                if (needed <= 0) continue;

                var data = requirement.m_resItem.m_itemData;
                var name = data.m_shared.m_name;

                for (var q = 0; q <= data.m_shared.m_maxQuality; q++)
                {
                    var shortfall = needed - pack.CountItems(name, q);
                    if (shortfall <= 0) return true;

                    // Asked before anything moves. Half a transfer leaves material stranded
                    // in the pack for a craft that then cannot happen.
                    if (HodChests.CountSpendable(name, q, true) < shortfall) continue;

                    var chests = HodChests.Near();
                    var spent = Watching() ? new List<HodShow.Spent>() : null;

                    for (var i = 0; i < chests.Count && shortfall > 0; i++)
                    {
                        var container = chests[i];
                        if (container == null) continue;

                        shortfall -= HodChests.MoveFromOwned(
                            container, pack, name, q, true, shortfall, spent);
                    }

                    HodShow.Send(spent);

                    if (shortfall <= 0) return true;
                }
            }

            return false;
        }
    }
}
