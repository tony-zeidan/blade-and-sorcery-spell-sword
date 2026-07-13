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
    /// penetrate. Each clone despawns after <see cref="projectileLifetime"/> seconds.
    /// </summary>
    public class SpellSwordScript : ThunderScript
    {
        // ---------------------------------------------------------------------
        // Tunables. (cloneSpeed and maxActiveClones live in the menu section below.)
        // ---------------------------------------------------------------------

        /// <summary>
        /// Small forward nudge (m) from the weapon's own position where the clone spawns.
        /// Kept small so close-range targets aren't "spawned over" (which caused bounces);
        /// the clone starts where your weapon is, like throwing it.
        /// </summary>
        public static float spawnForwardOffset = 0.1f;

        /// <summary>How far from a shield's center (m) the shield clone spawns (clears the shield/arm).</summary>
        public static float shieldSpawnOffset = 0.6f;

        /// <summary>Degrees to pitch the shield's aim down (PointDir/wrist tilts upward). 0 = none.</summary>
        public static float shieldAimPitchCorrection = 35f;

        /// <summary>Seconds a thrown clone lives before despawning (skipped if you're holding it).</summary>
        public static float projectileLifetime = 2f;

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

        // Slider steps: 25..80 m/s by 5 (default index 4 = 45).
        public static ModOptionFloat[] speedValues = BuildFloatRange(25f, 80f, 5f);

        // Arrow steps: 5..50 clones by 5 (default index 3 = 20).
        public static ModOptionInt[] countValues = BuildIntRange(5, 50, 5);

        [ModOptionCategory("Spell Sword", 0)]
        [ModOption("Spell Sword Enabled", "Turn the Spell Sword ability on or off.", "boolValues", defaultValueIndex = 1)]
        [ModOptionButton]
        public static bool ModEnabled = true;

        [ModOptionCategory("Spell Sword", 0)]
        [ModOption("Active On", "Which held items can fire a clone.", "scopeValues", defaultValueIndex = 0)]
        [ModOptionButton]
        public static int Scope = 0;

        [ModOptionCategory("Spell Sword", 0)]
        [ModOption("Projectile speed", "Launch speed (m/s) of fired clones.", "speedValues", defaultValueIndex = 4)]
        [ModOptionSlider]
        public static float cloneSpeed = 45f;

        [ModOptionCategory("Spell Sword", 0)]
        [ModOption("Max active clones", "Most clones alive at once; the oldest despawns first.", "countValues", defaultValueIndex = 3)]
        [ModOptionArrows]
        public static int maxActiveClones = 20;

        private static ModOptionFloat[] BuildFloatRange(float start, float end, float step)
        {
            List<ModOptionFloat> list = new List<ModOptionFloat>();
            for (float v = start; v <= end + 0.001f; v += step)
                list.Add(new ModOptionFloat(v.ToString("0"), v));
            return list.ToArray();
        }

        private static ModOptionInt[] BuildIntRange(int start, int end, int step)
        {
            List<ModOptionInt> list = new List<ModOptionInt>();
            for (int v = start; v <= end; v += step)
                list.Add(new ModOptionInt(v.ToString(), v));
            return list.ToArray();
        }

        // ---------------------------------------------------------------------

        private readonly HandState left = new HandState();
        private readonly HandState right = new HandState();

        // Active clone controllers, oldest first. Kept clean because each controller
        // unregisters itself on teardown (item cull), so no stale/pooled references linger.
        private readonly List<CloneController> activeClones = new List<CloneController>();

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

            RagdollHand rhand = playerHand.ragdollHand;
            bool pressed = playerHand.controlHand.castPressed;
            bool pressBegan = pressed && !state.castWasPressed;
            bool released = !pressed && state.castWasPressed;
            state.castWasPressed = pressed;

            if (pressBegan)
            {
                state.pressStartTime = Time.time;
                // Remember if this press started while a menu was open, so it never fires.
                // (Don't use uiClickDown: the right hand is the UI-pointer hand and sets it
                // during normal gameplay, which would block right-hand firing entirely.)
                state.pressBlockedByUI = PlayerControl.systemMenuActive || IsPointingUI(rhand.side);
                BlockSlide(state, rhand);
            }

            // Keep sliding disabled only during the tap window; restore once it's clearly a
            // hold, released, or the held handle changed (so holding to slide still works).
            if (state.slideBlocked)
            {
                bool inTapWindow = pressed
                    && (Time.time - state.pressStartTime <= clickMaxDuration)
                    && rhand.grabbedHandle == state.slideHandle;
                if (!inTapWindow)
                    RestoreSlide(state);
            }

            // Only act on release: a quick CLICK fires; a longer HOLD is left alone so you
            // can still slide your grip along the weapon while holding the button.
            if (!released)
                return;
            if (Time.time - state.pressStartTime > clickMaxDuration)
                return;
            if (state.pressBlockedByUI || PlayerControl.systemMenuActive || IsPointingUI(rhand.side))
                return;

            Item held = HeldItemOf(playerHand);
            if (held == null || !IsEligible(held))
                return;

            FireClone(held, rhand);
            playerHand.controlHand.HapticShort(0.7f, true);
        }

        /// <summary>True if this hand's UI pointer is currently over a book/menu/UI element.</summary>
        private static bool IsPointingUI(Side side)
        {
            try
            {
                Pointer p = Pointer.GetPointer(side);
                return p != null && p.isPointingUI;
            }
            catch { return false; }
        }

        private static void BlockSlide(HandState state, RagdollHand hand)
        {
            if (state.slideBlocked || hand == null)
                return;
            Handle h = hand.grabbedHandle;
            if (h == null)
                return;
            state.slideHandle = h;
            state.slideSaved = h.slideBehavior;
            h.slideBehavior = Handle.SlideBehavior.DisallowSlide;
            state.slideBlocked = true;
        }

        private static void RestoreSlide(HandState state)
        {
            if (state.slideBlocked && state.slideHandle != null)
            {
                try { state.slideHandle.slideBehavior = state.slideSaved; } catch { }
            }
            state.slideBlocked = false;
            state.slideHandle = null;
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

        /// <summary>
        /// Direction to fire a non-shield item: from the grip (hand) toward the weapon's far
        /// end (its farthest solid collider) — i.e. straight along how it's held, business-end
        /// first. Works for arrows, swords, greatswords, shovels, hammers, etc. Falls back to
        /// flyDirRef then transform.forward for degenerate cases.
        /// </summary>
        private static Vector3 WeaponAimDir(Item source, RagdollHand hand)
        {
            Vector3 grip = hand != null ? hand.transform.position : source.transform.position;
            Vector3 farPoint = FarthestColliderPoint(source, grip);

            Vector3 dir = farPoint - grip;
            if (dir.sqrMagnitude < 0.0004f)
                dir = source.flyDirRef != null ? source.flyDirRef.forward : source.transform.forward;
            return dir.normalized;
        }

        private static Vector3 FarthestColliderPoint(Item source, Vector3 grip)
        {
            Collider[] cols = source.GetComponentsInChildren<Collider>();
            Vector3 farthest = source.transform.position;
            float best = -1f;
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i].isTrigger)
                    continue;
                Vector3 p = cols[i].bounds.center;
                float d = (p - grip).sqrMagnitude;
                if (d > best) { best = d; farthest = p; }
            }
            return farthest;
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
                // Fire where the hand points. PointDir is the right heading but tilts upward,
                // so pitch it down a touch so the shield goes where the fist actually points.
                if (hand != null)
                {
                    Vector3 aim = hand.PointDir;
                    Vector3 horizRight = Vector3.Cross(Vector3.up, aim);
                    if (horizRight.sqrMagnitude > 0.0001f)
                        aim = Quaternion.AngleAxis(shieldAimPitchCorrection, horizRight.normalized) * aim;
                    dir = aim.normalized;
                }
                else
                {
                    dir = source.transform.forward.normalized;
                }
                spawnPos = source.transform.position + dir * shieldSpawnOffset;
            }
            else
            {
                // Fire along the weapon's length (grip -> far end), i.e. where the user is
                // pointing it. This is robust for odd shapes (greatswords, shovels, hammers)
                // whose flyDirRef points out a face rather than down the length.
                dir = WeaponAimDir(source, hand);
                // Spawn at the weapon's own position (like throwing it), not out at the tip,
                // so close-range targets aren't spawned over.
                spawnPos = source.transform.position + dir * spawnForwardOffset;
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

                    Rigidbody rb = clone.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.drag = 0f;                       // weight/drag independent travel
                        rb.angularDrag = 0.05f;
                        // Continuous (swept) detection so the fast clone doesn't tunnel, but
                        // NOT speculative (speculative contacts make it bounce off "air").
                        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                        rb.velocity = dir * cloneSpeed;     // straight, fast
                        rb.angularVelocity = Vector3.zero;  // no spin
                    }

                    // Flag it as thrown AFTER setting velocity, so the throw arms its
                    // fly/penetration state with the real velocity (otherwise penetration only
                    // kicks in after the clone has travelled a while = needs distance).
                    clone.Throw(1f, Item.FlyDetection.Forced);

                    // The flight sound, and a controller that (re)applies the imbue over the
                    // first moment (the clone's imbue points aren't ready this exact frame)
                    // and stops the sound on first impact.
                    EffectInstance flightSound = SpawnFlightSound(clone);
                    CloneController controller = clone.gameObject.AddComponent<CloneController>();
                    controller.Init(this, clone, imbueSpell, flightSound);
                    RegisterClone(controller);
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

        /// <summary>Track a new clone and despawn the oldest ones over the cap.</summary>
        internal void RegisterClone(CloneController controller)
        {
            activeClones.RemoveAll(c => c == null);
            activeClones.Add(controller);
            while (activeClones.Count > maxActiveClones)
            {
                CloneController oldest = activeClones[0];
                activeClones.RemoveAt(0);
                if (oldest != null)
                    oldest.DespawnNow();
            }
        }

        internal void UnregisterClone(CloneController controller)
        {
            activeClones.Remove(controller);
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

            // While a click is in progress we disable grip-sliding on the held handle so a
            // quick tap doesn't slide the hand down the hilt; restored after the tap window.
            public Handle slideHandle;
            public Handle.SlideBehavior slideSaved;
            public bool slideBlocked;
        }
    }

    /// <summary>
    /// Rides along on a flying clone. Applies the carried imbue over the first moment (the
    /// clone's imbue points aren't ready the exact frame it spawns), stops the flight sound on
    /// first impact, and despawns the clone after its lifetime.
    ///
    /// IMPORTANT: ThunderRoad pools items — when the clone despawns, its GameObject can be
    /// reused for a real item later. This component tears itself down on the item's cull event
    /// so it can never linger on a pooled object and, e.g., despawn a recycled arrow.
    /// </summary>
    public class CloneController : MonoBehaviour
    {
        private SpellSwordScript owner;
        private Item item;
        private SpellCastCharge imbueSpell;
        private EffectInstance flightSound;
        private float imbueUntil;
        private float dieTime;
        private bool soundStopped;
        private bool despawnRequested;
        private bool tornDown;

        public void Init(SpellSwordScript owner, Item item, SpellCastCharge imbueSpell, EffectInstance flightSound)
        {
            this.owner = owner;
            this.item = item;
            this.imbueSpell = imbueSpell;
            this.flightSound = flightSound;
            this.imbueUntil = Time.time + 0.5f;
            this.dieTime = Time.time + SpellSwordScript.projectileLifetime;
            if (item != null)
                item.OnCullEvent += OnCull;
            ApplyImbue();
        }

        private void Update()
        {
            if (tornDown || item == null)
                return;

            if (imbueSpell != null && Time.time <= imbueUntil)
                ApplyImbue();

            // Despawn after its lifetime (even if the player is holding it). Despawning culls
            // the item, which fires OnCull -> Teardown below.
            if (!despawnRequested && Time.time >= dieTime)
            {
                despawnRequested = true;
                try { item.Despawn(); } catch { Teardown(); }
            }
        }

        /// <summary>Force this clone to despawn now (used by the max-clone cap).</summary>
        public void DespawnNow()
        {
            if (tornDown || despawnRequested)
                return;
            despawnRequested = true;
            if (item != null)
            {
                try { item.Despawn(); } catch { Teardown(); }
            }
            else
            {
                Teardown();
            }
        }

        // Fires whenever the item is culled to the pool (our despawn, the game's cull, etc.).
        private void OnCull(bool culled)
        {
            if (culled)
                Teardown();
        }

        private void Teardown()
        {
            if (tornDown)
                return;
            tornDown = true;
            StopSound();
            if (item != null)
                item.OnCullEvent -= OnCull;
            item = null;
            if (owner != null)
                owner.UnregisterClone(this);
            Destroy(this);
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

        private void StopSound()
        {
            if (soundStopped)
                return;
            soundStopped = true;
            if (flightSound != null)
            {
                try { flightSound.End(false, 1f); } catch { }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            StopSound();
        }
    }
}
