using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;
using Ezomic.Shared;

namespace Grove
{
    /// <summary>
    /// The plantable ancient seed.
    ///
    /// Cloned from a vanilla crop sapling rather than assembled from nothing, because
    /// unlike the spirit this one genuinely wants a creature's worth of machinery: a
    /// Piece with placement rules, a WearNTear so it can be destroyed, a ZNetView, and
    /// the whole "can this go here" question that the cultivator already answers.
    ///
    /// What the donor's own `Plant` does is exactly what must not happen - it grows on
    /// a timer - so that component is the first thing torn out.
    /// </summary>
    internal static class SaplingPrefab
    {
        /// <summary>
        /// The network identity. ZNetScene keys prefabs by name.GetStableHashCode() and
        /// a saved ZDO stores that hash, so renaming this orphans every planted seed in
        /// a save. Treat it as permanent.
        /// </summary>
        public const string Name = "grove_ancient_sapling";

        private const string StageMesh = "grove_sapling_{0}.obj";

        /// <summary>
        /// How many growth models the sapling wears. One, for now.
        ///
        /// It was four, and the staging works: Show swaps them as the count crosses each
        /// quarter, and the whole chain has been played through all of them. What is wrong is
        /// the art. Stages two, three and four were not good enough to ship, so they are held
        /// back for 1.1 rather than released and apologised for, and the machinery is left
        /// exactly as it is. Putting them back is this number and three model files.
        ///
        /// The hover text still names four states - newly planted, rooting, swelling, ready
        /// to open - because it reads the count rather than the model, and with one model it
        /// is the only thing telling you the sapling is getting anywhere.
        /// </summary>
        private const int Stages = 1;

        /// <summary>
        /// Builds the sapling once. The scene, the cultivator's build menu, and being put
        /// back into both on every world load are Ezomic.Shared.Prefabs' business - it is
        /// handed this method, the name and the tool in GrovePlugin.Awake.
        ///
        /// The cultivator rather than the hammer, and that is a decision rather than a
        /// detail: it is a seed, so the tool already in your hand when you think "I want to
        /// plant this" is the one it should be under. On the hammer it would work and you
        /// would go looking for it among the furniture.
        /// </summary>
        // ------------------------------------------------------------------ building

        private static GameObject Donor()
        {
            var scene = ZNetScene.instance;

            foreach (var name in new[] { GroveConfig.SaplingDonor.Value, "sapling_carrot" })
            {
                if (string.IsNullOrEmpty(name)) continue;

                var found = scene.GetPrefab(name);
                if (found != null) return found;

                GrovePlugin.LogOnce("Sapling donor '" + name + "' does not exist.");
            }

            return null;
        }

        internal static GameObject Build()
        {
            var source = Donor();
            if (source == null) return null;

            var directory = Path.GetDirectoryName(typeof(SaplingPrefab).Assembly.Location);
            var models = new ModelData[Stages];

            for (var i = 0; i < Stages; i++)
            {
                models[i] = ObjMesh.Load(
                    Path.Combine(directory, string.Format(StageMesh, i + 1)));

                if (models[i] != null) continue;

                GrovePlugin.Log.LogError(
                    "Sapling stage mesh " + (i + 1) + " is missing from beside the dll.");
                return null;
            }

            // Holder and init suppression both from Prefabs. The suppression is the half
            // that matters: cloned under an active parent, the ZNetView's Awake runs and
            // tries to register the thing on the network while it is still half-built.
            var clone = Prefabs.Clone(source, Name);
            if (clone == null) return null;

            clone.transform.localRotation = Quaternion.identity;
            clone.transform.localScale = Vector3.one * GroveConfig.SaplingScale.Value;

            Strip(clone);
            Dress(clone);
            Fortify(clone);

            var stages = Visuals(clone, models);

            // After Fortify, so the spawner is put on a piece that can survive what it
            // calls, and before the Sapling component so a first Update finds it there.
            Beckon.Attach(clone);

            var sapling = clone.GetComponent<Sapling>();
            if (sapling == null) sapling = clone.AddComponent<Sapling>();
            sapling.Stages = stages;

            GrovePlugin.Log.LogInfo("Built " + Name + " from " + source.name + ".");
            return clone;
        }

        /// <summary>
        /// Everything the donor does that this must not.
        ///
        /// DestroyImmediate rather than Destroy: ordinary Destroy is deferred to the end
        /// of the frame, and this prefab is registered and can be planted within that
        /// frame - which would hand out a seed that really is a carrot.
        /// </summary>
        private static void Strip(GameObject clone)
        {
            // The timer. This is the whole reason the mod has its own component.
            foreach (var plant in clone.GetComponentsInChildren<Plant>(true))
                Object.DestroyImmediate(plant);

            // A crop you can pull up. The seed is not harvestable - the spirit is the
            // harvest, and leaving this on would let you pick the seed straight back up
            // for free the moment you planted it.
            foreach (var pickable in clone.GetComponentsInChildren<Pickable>(true))
                Object.DestroyImmediate(pickable);

            // Null-checked because destroying a renderer's GameObject takes its children with
            // it, and GetComponentsInChildren returns parents before descendants - so a
            // renderer deeper in the same branch is already gone by the time the loop reaches
            // it, and asking a destroyed Component for its gameObject throws. This surfaced as
            // an NRE every frame on a dedicated server, where the donor's hierarchy is nested
            // differently, but the bug was always here.
            foreach (var renderer in clone.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer == null) continue;
                Object.DestroyImmediate(renderer.gameObject);
            }
        }

        private static void Dress(GameObject clone)
        {
            var piece = clone.GetComponent<Piece>();
            if (piece == null) return;

            piece.m_name = GroveConfig.SaplingName.Value;
            piece.m_description = "It grows on what dies near it.";
            piece.m_resources = Requirements(GroveConfig.SaplingCost.Value);

            // Without this the clone keeps the donor's picture and the cultivator
            // offers you a carrot - the name, the description and the cost were all
            // overridden here and the icon was simply forgotten. Left alone when the
            // PNG is missing, because the donor's wrong icon still beats no icon.
            var icon = Icons.Load(GroveConfig.SaplingIcon.Value, Name);
            if (icon != null) piece.m_icon = icon;

            piece.m_groundPiece = true;
            piece.m_groundOnly = true;
            piece.m_cultivatedGroundOnly = GroveConfig.NeedsCultivated.Value;

            // Not comfort, not a crafting station, and emphatically not something a
            // raid should target ahead of your walls.
            piece.m_targetNonPlayerBuilt = false;
        }

        /// <summary>
        /// Real health, and nothing that rots it while you are away.
        ///
        /// The donor is a crop, so the clone inherits a crop's health - a few points -
        /// and the first greydwarf that swung anywhere near it ended an hour of work
        /// for an ancient seed that is not refunded. Found by playing: fifteen brutes
        /// spawned beside a sapling and it was gone before any of them died, which
        /// looked exactly like the feeding hook being broken because nothing logs when
        /// there is no sapling left to feed.
        ///
        /// Which component carries that health is the donor's business and not ours -
        /// WearNTear for anything built, Destructible for anything grown - so both are
        /// handled and the log says which one was actually there.
        /// </summary>
        private static void Fortify(GameObject clone)
        {
            var health = Mathf.Max(1f, GroveConfig.SaplingHealth.Value);
            var found = new List<string>();

            foreach (var wear in clone.GetComponentsInChildren<WearNTear>(true))
            {
                if (wear == null) continue;

                wear.m_health = health;

                // A plant is not a building. Support wear collapses anything with
                // nothing under it and roof wear rots anything standing in the rain,
                // and this is a seed in open ground that has to survive an hour of you
                // being somewhere else. Losing it to weather would read as a bug even
                // though it is the donor's own behaviour.
                wear.m_noSupportWear = true;
                wear.m_noRoofWear = true;

                found.Add("WearNTear");
            }

            foreach (var destructible in clone.GetComponentsInChildren<Destructible>(true))
            {
                if (destructible == null) continue;

                destructible.m_health = health;

                // A timer that deletes it. Awake does InvokeRepeating("DestroyNow", m_ttl)
                // for any non-zero ttl, so whatever the donor happened to carry would take
                // the sapling away on its own schedule - and the symptom is bare ground
                // with no attacker and no message, which is the worst kind of bug to chase.
                destructible.m_ttl = 0f;

                // Anything can hurt it. A non-zero tier fails the hit with a "too hard"
                // popup, and a sapling that greydwarfs cannot damage is not the tough but
                // breakable thing that was asked for - it is invulnerable with extra steps.
                destructible.m_minToolTier = 0;

                found.Add("Destructible");
            }

            if (found.Count == 0)
            {
                GrovePlugin.LogOnce(
                    "The sapling donor carries neither WearNTear nor Destructible, so it "
                    + "cannot be damaged and cannot be given health either.");
                return;
            }

            GrovePlugin.Log.LogInfo("Sapling health set to " + health + " on "
                                    + string.Join(" + ", found.ToArray()) + ".");
        }

        private static Transform[] Visuals(GameObject clone, ModelData[] models)
        {
            var stages = new Transform[models.Length];

            for (var i = 0; i < models.Length; i++)
            {
                var go = new GameObject("stage" + (i + 1));
                go.transform.SetParent(clone.transform, false);

                go.AddComponent<MeshFilter>().sharedMesh = models[i].Mesh;

                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = Skins.Skin(models[i].Groups);

                // After Skin, because that is what measures each group's atlas rect.
                Skins.Remap(models[i].Mesh, models[i].Groups);

                // All off. Sapling.Show turns exactly one on, and leaving them all
                // enabled here would draw four saplings inside each other for the one
                // frame before Start runs.
                go.SetActive(false);

                stages[i] = go.transform;
            }

            return stages;
        }

        private static Piece.Requirement[] Requirements(string spec)
        {
            var list = new List<Piece.Requirement>();

            foreach (var entry in (spec ?? "").Split(','))
            {
                var parts = entry.Split(':');
                if (parts.Length != 2) continue;

                var itemName = parts[0].Trim();
                if (itemName.Length == 0) continue;

                int amount;
                if (!int.TryParse(parts[1].Trim(), out amount) || amount <= 0) continue;

                var prefab = ObjectDB.instance.GetItemPrefab(itemName);
                var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (drop == null)
                {
                    GrovePlugin.LogOnce("Sapling cost mentions unknown item '" + itemName + "'.");
                    continue;
                }

                list.Add(new Piece.Requirement
                {
                    m_resItem = drop,
                    m_amount = amount,

                    // Not recoverable. An ancient seed put into the ground is spent -
                    // being able to plant one, change your mind, and get it back would
                    // make the whole ritual free to attempt.
                    m_recover = false
                });
            }

            return list.ToArray();
        }

    }
}
