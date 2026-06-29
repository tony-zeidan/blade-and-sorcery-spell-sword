using System;
using System.Collections.Generic;
using ThunderRoad;
using UnityEngine;

namespace SpellSword
{
    /// <summary>
    /// Spell Sword (global ThunderScript — auto-discovered, works with no custom assets).
    ///
    /// Click the imbue/spell button while holding an eligible item to fire a clone of it:
    ///   * Swords/weapons fly straight out the blade tip (item.flyDirRef.forward).
    ///   * Shields fly out perpendicular to their face (the way they defend).
    /// The clone carries the held item's current imbue, or the spell selected on that hand,
    /// if any. Clones travel fast and weight-independent, and are flagged thrown so they
    /// penetrate. Up to <see cref="maxActiveClones"/> clones persist; the oldest despawn first.
    /// </summary>
    public class SpellSwordScript : ThunderScript
    {
        // ---------------------------------------------------------------------
        // Tunables.
        // ---------------------------------------------------------------------

        /// <summary>Launch speed (m/s) of the clone.</summary>
        public static float cloneSpeed = 40f;

        /// <summary>How far past the tip / shield face (m) the clone spawns.</summary>
        public static float spawnForwardOffset = 0.3f;

        /// <summary>Maximum live clones before the oldest start despawning.</summary>
        public static int maxActiveClones = 30;

        /// <summary>Catalog id of the whoosh sound played on the clone.</summary>
        public static string whooshEffectId = "WhooshSwordShort";

        /// <summary>Volume intensity (0..1) of the clone whoosh.</summary>
        public static float thrownWhooshIntensity = 1f;

        /// <summary>The item id used by the "Short sword only" scope.</summary>
        public static string targetSwordId = "SwordShortCommon";

        // =====================================================================
        // In-game config menu (Options -> Mods). Each [ModOption] needs an
        // interaction attribute ([ModOptionButton]) AND a value-source array.
        // =====================================================================

        public static ModOptionBool[] boolValues =
        {
            new ModOptionBool("Disabled", false),
            new ModOptionBool("Enabled", true),
        };

        public static ModOptionInt[] scopeValues =
        {
            new ModOptionInt("Short sword only", 0),
            new ModOptionInt("All swords", 1),
            new ModOptionInt("All daggers", 2),
            new ModOptionInt("Swords & daggers", 3),
            new ModOptionInt("All weapons", 4),
            new ModOptionInt("All weapons & tools", 5),
            new ModOptionInt("All shields", 7),
            new ModOptionInt("Any held item", 6),
        };

        [ModOption("Spell Sword Enabled", "Turn the Spell Sword ability on or off.", "boolValues", defaultValueIndex = 1)]
        [ModOptionButton]
        public static bool ModEnabled = true;

        [ModOption("Active On", "Which held items can fire a clone.", "scopeValues", defaultValueIndex = 0)]
        [ModOptionButton]
        public static int Scope = 0;

        // ---------------------------------------------------------------------

        private readonly HandState left = new HandState();
        private readonly HandState right = new HandState();

        // Live clones, oldest first. Capped at maxActiveClones.
        private readonly List<Item> clones = new List<Item>();

        private EffectData whooshEffectData;
        private bool whooshResolved;

        private const string LogPrefix = "[SpellSword] ";
        private float lastErrorTime = -999f;

        public override void ScriptLoaded(ModManager.ModData modData)
        {
            base.ScriptLoaded(modData);
            Debug.Log(LogPrefix + "Loaded. Click the imbue button on an item to fire a clone.");
        }

        public override void ScriptUpdate()
        {
            base.ScriptUpdate();

            // Never let a per-frame failure spam the log or break the player's session.
            try
            {
                Player player = Player.local;
                if (player != null)
                {
                    UpdateHand(player.handRight, right);
                    UpdateHand(player.handLeft, left);
                }
            }
            catch (Exception e)
            {
                LogException("ScriptUpdate", e);
            }
        }

        public override void ScriptUnload()
        {
            // Clean up our clones when the mod unloads/reloads.
            for (int i = 0; i < clones.Count; i++)
            {
                if (clones[i] != null)
                {
                    try { clones[i].Despawn(); } catch { }
                }
            }
            clones.Clear();
            base.ScriptUnload();
        }

        /// <summary>Log an exception at most once every few seconds (avoids per-frame spam).</summary>
        private void LogException(string where, Exception e)
        {
            if (Time.time - lastErrorTime < 5f)
                return;
            lastErrorTime = Time.time;
            Debug.LogError(LogPrefix + where + " error: " + e);
        }

        private void UpdateHand(PlayerHand playerHand, HandState state)
        {
            if (playerHand == null || playerHand.ragdollHand == null || playerHand.controlHand == null)
                return;

            // Fresh click (rising edge) of the imbue/spell button.
            bool pressed = playerHand.controlHand.castPressed;
            bool clicked = pressed && !state.castWasPressed;
            state.castWasPressed = pressed;

            if (!clicked)
                return;

            RagdollHand hand = playerHand.ragdollHand;
            Item held = HeldItemOf(playerHand);
            if (held == null || !IsEligible(held))
                return;

            FireClone(held, hand);
            playerHand.controlHand.HapticShort(0.7f, true);
        }

        private bool IsEligible(Item item)
        {
            if (!ModEnabled || item == null || item.data == null)
                return false;

            ItemData d = item.data;
            switch (Scope)
            {
                case 0: return d.id == targetSwordId;                              // Short sword only
                case 1: return d.category == "Swords";                            // All swords
                case 2: return d.category == "Daggers";                           // All daggers
                case 3: return d.category == "Swords" || d.category == "Daggers"; // Swords & daggers
                case 4: return d.type == ItemData.Type.Weapon;                    // All weapons
                case 5: return d.type == ItemData.Type.Weapon
                            || d.type == ItemData.Type.Tool;                       // Weapons & tools
                case 7: return IsShield(item);                                    // All shields
                case 6: return true;                                              // Any held item
                default: return false;
            }
        }

        private static bool IsShield(Item item)
        {
            return item.data != null
                && (item.data.type == ItemData.Type.Shield || item.data.category == "Shields");
        }

        private void FireClone(Item source, RagdollHand hand)
        {
            ItemData data = source.data;
            if (data == null)
                return;

            // Launch direction: shields fire perpendicular to their face; everything else
            // fires out along the blade tip.
            Vector3 dir;
            Vector3 originPos;
            if (IsShield(source))
            {
                dir = ShieldFaceNormal(source, hand);
                originPos = source.transform.position;
            }
            else
            {
                Transform tip = source.flyDirRef != null ? source.flyDirRef : source.transform;
                dir = tip.forward;
                originPos = tip.position;
            }

            Vector3 spawnPos = originPos + dir * spawnForwardOffset;
            Quaternion spawnRot = source.transform.rotation;

            // The imbue to carry: the held item's active blade imbue, else the hand's spell.
            SpellCastCharge imbueSpell = GetImbueSpell(source, hand);

            data.SpawnAsync(clone =>
            {
                try
                {
                    if (clone == null)
                        return;

                    if (source != null)
                        clone.IgnoreObjectCollision(source);

                    // Don't injure the caster with their own point-blank clone.
                    Player player = Player.local;
                    if (player != null && player.creature != null && player.creature.ragdoll != null)
                        clone.IgnoreRagdollCollision(player.creature.ragdoll);

                    // Flag it as thrown so the game treats hits as penetrating thrown-weapon hits.
                    clone.Throw(1f, Item.FlyDetection.Forced);

                    Rigidbody rb = clone.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.drag = 0f;                       // weight/drag independent travel
                        rb.angularDrag = 0.05f;
                        rb.velocity = dir * cloneSpeed;     // straight, fast
                        rb.angularVelocity = Vector3.zero;  // no spin
                    }

                    if (imbueSpell != null)
                        ApplyImbue(clone, imbueSpell);

                    PlayWhoosh(clone);
                    Register(clone);
                }
                catch (Exception e)
                {
                    LogException("SpawnClone", e);
                }
            }, spawnPos, spawnRot);
        }

        /// <summary>
        /// The perpendicular-to-face direction of a shield (the way it defends).
        /// Found as the thinnest axis of the shield's solid colliders, pointing away from
        /// the holding hand.
        /// </summary>
        private static Vector3 ShieldFaceNormal(Item item, RagdollHand hand)
        {
            Transform t = item.transform;
            Collider[] cols = item.GetComponentsInChildren<Collider>();

            Vector3 min = Vector3.positiveInfinity;
            Vector3 max = Vector3.negativeInfinity;
            bool any = false;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].isTrigger)
                    continue;
                any = true;
                Bounds wb = cols[i].bounds;
                for (int xi = -1; xi <= 1; xi += 2)
                    for (int yi = -1; yi <= 1; yi += 2)
                        for (int zi = -1; zi <= 1; zi += 2)
                        {
                            Vector3 corner = wb.center + Vector3.Scale(wb.extents, new Vector3(xi, yi, zi));
                            Vector3 local = t.InverseTransformPoint(corner);
                            min = Vector3.Min(min, local);
                            max = Vector3.Max(max, local);
                        }
            }

            if (!any)
                return t.forward;

            Vector3 size = max - min;
            Vector3 localNormal;
            if (size.x <= size.y && size.x <= size.z) localNormal = Vector3.right;
            else if (size.y <= size.x && size.y <= size.z) localNormal = Vector3.up;
            else localNormal = Vector3.forward;

            Vector3 worldNormal = t.TransformDirection(localNormal).normalized;

            // Point away from the hand (i.e. toward the front of the shield).
            Vector3 faceCenter = t.TransformPoint((min + max) * 0.5f);
            Vector3 handPos = hand != null ? hand.transform.position : t.position;
            if (Vector3.Dot(worldNormal, faceCenter - handPos) < 0f)
                worldNormal = -worldNormal;

            return worldNormal;
        }

        /// <summary>Held item's active blade imbue, else the hand's selected spell, else null.</summary>
        private static SpellCastCharge GetImbueSpell(Item source, RagdollHand hand)
        {
            if (source.imbues != null)
            {
                for (int i = 0; i < source.imbues.Count; i++)
                {
                    Imbue imbue = source.imbues[i];
                    if (imbue != null && imbue.spellCastBase != null && imbue.energy > 0f)
                        return imbue.spellCastBase;
                }
            }

            if (hand != null && hand.caster != null && hand.caster.spellInstance is SpellCastCharge selected)
                return selected;

            return null;
        }

        private static void ApplyImbue(Item item, SpellCastCharge spell)
        {
            if (item.imbues == null)
                return;
            for (int i = 0; i < item.imbues.Count; i++)
            {
                Imbue imbue = item.imbues[i];
                if (imbue != null)
                    imbue.Transfer(spell, imbue.maxEnergy);
            }
        }

        private void Register(Item clone)
        {
            if (clone != null)
                clones.Add(clone);
            clones.RemoveAll(c => c == null);

            // Despawn the oldest clones over the cap, but keep any the player is holding.
            int i = 0;
            while (clones.Count > maxActiveClones && i < clones.Count)
            {
                Item c = clones[i];
                if (c == null) { clones.RemoveAt(i); continue; }
                if (c.mainHandler != null) { i++; continue; } // skip held clones
                clones.RemoveAt(i);
                try { c.Despawn(); } catch { }
            }
        }

        private void PlayWhoosh(Item clone)
        {
            EffectData whoosh = GetWhooshEffect();
            if (whoosh == null)
                return;
            EffectInstance ws = whoosh.Spawn(clone.transform, false, null, true);
            if (ws != null)
            {
                ws.SetIntensity(thrownWhooshIntensity);
                ws.Play(0, false, false);
            }
        }

        private EffectData GetWhooshEffect()
        {
            if (!whooshResolved)
            {
                whooshEffectData = Catalog.GetData<EffectData>(whooshEffectId, false);
                if (whooshEffectData != null)
                    whooshResolved = true;
            }
            return whooshEffectData;
        }

        private static Item HeldItemOf(PlayerHand playerHand)
        {
            if (playerHand == null || playerHand.ragdollHand == null)
                return null;
            Handle handle = playerHand.ragdollHand.grabbedHandle;
            return handle != null ? handle.item : null;
        }

        private class HandState
        {
            public bool castWasPressed;
        }
    }
}
