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
    ///   * Shields fly out in the direction the hand is pointing.
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

        /// <summary>How far past the blade tip (m) a weapon clone spawns (clears the held weapon).</summary>
        public static float spawnForwardOffset = 0.5f;

        /// <summary>How far from a shield's center (m) the shield clone spawns (clears the shield/arm).</summary>
        public static float shieldSpawnOffset = 0.6f;

        /// <summary>Maximum live clones before the oldest start despawning.</summary>
        public static int maxActiveClones = 30;

        /// <summary>
        /// Catalog id of the sound played while the clone flies. Other options:
        /// WhooshSpin, WhooshSwordShort, WhooshSwordLong, WhooshDagger, WhooshPropLight.
        /// </summary>
        public static string whooshEffectId = "WhooshSpin";

        /// <summary>Volume intensity (0..1) of the flight sound.</summary>
        public static float thrownWhooshIntensity = 1f;

        /// <summary>
        /// Max press length (s) that counts as a "click" (fires). Holding longer than this
        /// does NOT fire, so you can still hold the button to slide your grip along a weapon.
        /// </summary>
        public static float clickMaxDuration = 0.3f;

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

            bool pressed = playerHand.controlHand.castPressed;
            bool pressBegan = pressed && !state.castWasPressed;
            bool released = !pressed && state.castWasPressed;
            state.castWasPressed = pressed;

            if (pressBegan)
            {
                state.pressStartTime = Time.time;
                // Remember if this press started on UI / in a menu, so it never fires.
                state.pressBlockedByUI = PlayerControl.systemMenuActive || PlayerControl.uiClickDown;
            }

            // Only act on release: a quick CLICK fires; a longer HOLD is left alone so you
            // can still slide your grip along the weapon while holding the button.
            if (!released)
                return;
            if (Time.time - state.pressStartTime > clickMaxDuration)
                return;
            if (state.pressBlockedByUI || PlayerControl.systemMenuActive || PlayerControl.uiClickDown)
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

            // Launch direction & spawn point: shields fire in the direction the hand is
            // pointing (spawned clear of the shield/arm); everything else fires out the tip.
            Vector3 dir;
            Vector3 spawnPos;
            if (IsShield(source))
            {
                // Fire where the hand is aimed. PlayerHand follows the controller directly and
                // its forward tracks "fist pointing straight out" better than the anatomical
                // PointDir (which tilts upward).
                if (hand != null && hand.playerHand != null)
                    dir = hand.playerHand.transform.forward;
                else if (hand != null)
                    dir = hand.PointDir;
                else
                    dir = source.transform.forward;
                dir = dir.normalized;
                spawnPos = source.transform.position + dir * shieldSpawnOffset;
            }
            else
            {
                Transform tip = source.flyDirRef != null ? source.flyDirRef : source.transform;
                dir = tip.forward.normalized;
                spawnPos = tip.position + dir * spawnForwardOffset;
            }

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
                        // Fast projectiles bounce/tunnel with discrete detection. Speculative
                        // continuous detection works against ALL colliders (including dynamic
                        // ragdoll parts), so close-range hits register and penetrate.
                        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        rb.velocity = dir * cloneSpeed;     // straight, fast
                        rb.angularVelocity = Vector3.zero;  // no spin
                    }

                    // The flight sound, and a controller that (re)applies the imbue over the
                    // first moment (the clone's imbue points aren't ready this exact frame)
                    // and stops the sound on first impact.
                    EffectInstance flightSound = SpawnFlightSound(clone);
                    clone.gameObject.AddComponent<CloneController>().Init(clone, imbueSpell, flightSound);

                    Register(clone);
                }
                catch (Exception e)
                {
                    LogException("SpawnClone", e);
                }
            }, spawnPos, spawnRot);
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

        private EffectInstance SpawnFlightSound(Item clone)
        {
            EffectData whoosh = GetWhooshEffect();
            if (whoosh == null)
                return null;
            EffectInstance ws = whoosh.Spawn(clone.transform, false, null, true);
            if (ws == null)
                return null;
            ws.SetIntensity(thrownWhooshIntensity);
            ws.Play(0, false, false);
            return ws;
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
            public float pressStartTime;
            public bool pressBlockedByUI;
        }
    }

    /// <summary>
    /// Rides along on a flying clone. Applies the carried imbue over the first moment (the
    /// clone's imbue points aren't ready the exact frame it spawns), and stops the looping
    /// flight sound the first time the clone hits something solid.
    /// </summary>
    public class CloneController : MonoBehaviour
    {
        private Item item;
        private SpellCastCharge imbueSpell;
        private EffectInstance flightSound;
        private float imbueUntil;
        private bool soundStopped;

        public void Init(Item item, SpellCastCharge imbueSpell, EffectInstance flightSound)
        {
            this.item = item;
            this.imbueSpell = imbueSpell;
            this.flightSound = flightSound;
            this.imbueUntil = Time.time + 0.5f;
            ApplyImbue();
        }

        private void Update()
        {
            if (imbueSpell != null && Time.time <= imbueUntil)
                ApplyImbue();
        }

        private void ApplyImbue()
        {
            if (item == null || imbueSpell == null || item.imbues == null)
                return;
            for (int i = 0; i < item.imbues.Count; i++)
            {
                Imbue imbue = item.imbues[i];
                if (imbue != null)
                {
                    try { imbue.Transfer(imbueSpell, imbue.maxEnergy); } catch { }
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (soundStopped)
                return;
            soundStopped = true;
            if (flightSound != null)
            {
                try { flightSound.End(false, 1f); } catch { }
            }
        }
    }
}
