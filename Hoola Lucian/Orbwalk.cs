#region LICENSE

/*
 Copyright 2014 - 2015 LeagueSharp
 Orbwalking.cs is part of LeagueSharp.Common.
 
 LeagueSharp.Common is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 
 LeagueSharp.Common is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.
 
 You should have received a copy of the GNU General Public License
 along with LeagueSharp.Common. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;
using Color = System.Drawing.Color;

#endregion

namespace HoolaLucian
{

    /// <summary>
    ///     This class offers everything related to auto-attacks and orbwalking.
    /// </summary>
    public static class Orbwalking
    {
        public delegate void OnAttackEvenH(AttackableUnit unit, AttackableUnit target);

        public delegate void OnNonKillableMinionH(AttackableUnit minion);

        public delegate void OnTargetChangeH(AttackableUnit oldTarget, AttackableUnit newTarget);
        public enum OrbwalkingMode
        {
            LastHit,
            Mixed,
            LaneClear,
            Combo,
            None
        }
        

        //Spells that are not attacks even if they have the "attack" word in their name.
        private static readonly string[] NoAttacks =
        {
            "volleyattack", "volleyattackwithsound", "jarvanivcataclysmattack",
            "monkeykingdoubleattack", "shyvanadoubleattack",
            "shyvanadoubleattackdragon", "zyragraspingplantattack",
            "zyragraspingplantattack2", "zyragraspingplantattackfire",
            "zyragraspingplantattack2fire", "viktorpowertransfer",
            "sivirwattackbounce", "asheqattacknoonhit",
            "elisespiderlingbasicattack", "heimertyellowbasicattack",
            "heimertyellowbasicattack2", "heimertbluebasicattack",
            "annietibbersbasicattack", "annietibbersbasicattack2",
            "yorickdecayedghoulbasicattack", "yorickravenousghoulbasicattack",
            "yorickspectralghoulbasicattack", "malzaharvoidlingbasicattack",
            "malzaharvoidlingbasicattack2", "malzaharvoidlingbasicattack3",
            "kindredwolfbasicattack", "kindredbasicattackoverridelightbombfinal"
        };

        //Spells that are attacks even if they dont have the "attack" word in their name.
        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2",
            "kennenmegaproc", "lucianpassiveattack", "masteryidoublestrike", "quinnwenhanced", "renektonexecute",
            "renektonsuperexecute", "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2",
            "xenzhaothrust3", "viktorqbuff"
        };

        // Champs whose auto attacks can't be cancelled
        public static int LastAATick;
        public static bool Attack = true;
        public static bool Move = true;
        private static AttackableUnit _lastTarget;
        private static readonly Obj_AI_Hero Player;

        static Orbwalking()
        {
            Player = ObjectManager.Player;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Spellbook.OnStopCast += SpellbookOnStopCast;
        }
        /// <summary>
        ///     This event is fired when a unit is about to auto-attack another unit.
        /// </summary>
        public static event OnAttackEvenH OnAttack;
        
        /// <summary>
        ///     Gets called on target changes
        /// </summary>
        public static event OnTargetChangeH OnTargetChange;

        //  <summary>
        //      Gets called if you can't kill a minion with auto attacks
        //  </summary>
        public static event OnNonKillableMinionH OnNonKillableMinion;
        
        private static void FireOnAttack(AttackableUnit unit, AttackableUnit target)
        {
            OnAttack?.Invoke(unit, target);
        }


        private static void FireOnTargetSwitch(AttackableUnit newTarget)
        {
            if (OnTargetChange != null && (!_lastTarget.IsValidTarget() || _lastTarget != newTarget))
            {
                OnTargetChange(_lastTarget, newTarget);
            }
        }

        private static void FireOnNonKillableMinion(AttackableUnit minion)
        {
            OnNonKillableMinion?.Invoke(minion);
        }

        /// <summary>
        ///     Returns true if the spellname is an auto-attack.
        /// </summary>
        public static bool IsAutoAttack(string name) => (name.ToLower().Contains("attack") && !NoAttacks.Contains(name.ToLower())) ||
                   Attacks.Contains(name.ToLower());

        /// <summary>
        ///     Returns the auto-attack range of local player with respect to the target.
        /// </summary>
        public static float GetRealAutoAttackRange(AttackableUnit target)
        {
            var result = Player.AttackRange + Player.BoundingRadius;
            if (target.IsValidTarget())
            {
                return result + target.BoundingRadius;
            }
            return result;
        }

        /// <summary>
        ///     Returns the auto-attack range of the target.
        /// </summary>
        public static float GetAttackRange(Obj_AI_Hero target)
        {
            var result = target.AttackRange + target.BoundingRadius;
            return result;
        }

        /// <summary>
        ///     Returns true if the target is in auto-attack range.
        /// </summary>
        public static bool InAutoAttackRange(AttackableUnit target)
        {
            if (!target.IsValidTarget())
            {
                return false;
            }
            var myRange = GetRealAutoAttackRange(target);
            return
                Vector2.DistanceSquared(
                    (target as Obj_AI_Base)?.ServerPosition.To2D() ?? target.Position.To2D(),
                    Player.ServerPosition.To2D()) <= myRange * myRange;
        }
        

        /// <summary>
        ///     Returns if the player's auto-attack is ready.
        /// </summary>
        public static bool CanAttack => Utils.GameTimeTickCount >= LastAATick + (Player.AttackDelay * 1000) && Attack;

        /// <summary>
        ///     Returns true if moving won't cancel the auto-attack.
        /// </summary>
        public static bool CanMove(double extraWindup) => Move && !Player.IsWindingUp && (Utils.GameTimeTickCount >= LastAATick + Player.AttackCastDelay * 970 + extraWindup);
        
        /// <summary>
        ///     Orbwalk a target while moving to Position.
        /// </summary>

        /// <summary>
        ///     Resets the Auto-Attack timer.
        /// </summary>
        public static void ResetAutoAttackTimer() => LastAATick = 0;

        private static void SpellbookOnStopCast(Spellbook spellbook, SpellbookStopCastEventArgs args)
        {
            if (spellbook.Owner.IsValid && spellbook.Owner.IsMe && args.DestroyMissile && args.StopAnimation)
            {
                ResetAutoAttackTimer();
            }
        }
        
        private static void OnProcessSpell(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs Spell)
        {
            var spellName = Spell.SData.Name;

            if (!IsAutoAttack(spellName))
            {
                return;
            }

            if (unit.IsMe &&
                (Spell.Target is Obj_AI_Base || Spell.Target is Obj_BarracksDampener || Spell.Target is Obj_HQ))
            {
                LastAATick = Utils.GameTimeTickCount;

                if (Spell.Target is Obj_AI_Base)
                {
                    var target = (Obj_AI_Base)Spell.Target;
                    if (target.IsValid)
                    {
                        FireOnTargetSwitch(target);
                        _lastTarget = target;
                    }
                }
            }

            FireOnAttack(unit, _lastTarget);
        }

        /// <summary>
        ///     This class allows you to add an instance of "Orbwalker" to your assembly in order to control the orbwalking in an
        ///     easy way.
        /// </summary>
        public class Orbwalker
        {
            private const float LaneClearWaitTimeMod = 2f;
            private static Menu _config;
            private readonly Obj_AI_Hero Player;
            private Obj_AI_Base _forcedTarget;
            private OrbwalkingMode _mode = OrbwalkingMode.None;
            private Vector3 _orbwalkingPoint;
            private Obj_AI_Minion _prevMinion;
            public static List<Orbwalker> Instances = new List<Orbwalker>();

            public Orbwalker(Menu attachToMenu)
            {
                _config = attachToMenu;
                /* Drawings submenu */
                var drawings = new Menu("Drawings", "drawings");
                drawings.AddItem(
                    new MenuItem("AACircle", "AACircle").SetShared()
                        .SetValue(new Circle(true, Color.FromArgb(155, 255, 255, 0))));
                drawings.AddItem(
                    new MenuItem("AACircle2", "Enemy AA circle").SetShared()
                        .SetValue(new Circle(false, Color.FromArgb(155, 255, 255, 0))));
                drawings.AddItem(
                    new MenuItem("HoldZone", "HoldZone").SetShared()
                        .SetValue(new Circle(false, Color.FromArgb(155, 255, 255, 0))));
                drawings.AddItem(
                    new MenuItem("AALineWidth", "Line Width")).SetShared()
                    .SetValue(new Slider(2, 1, 6));
                _config.AddSubMenu(drawings);

                /* Misc options */
                var misc = new Menu("Misc", "Misc");
                misc.AddItem(
                    new MenuItem("HoldPosRadius", "Hold Position Radius").SetValue(new Slider(65, 0, 250)));
                misc.AddItem(new MenuItem("PriorizeFarm", "Priorize farm over harass").SetValue(true));
                misc.AddItem(new MenuItem("AttackWards", "Auto attack wards").SetShared().SetValue(false));
                misc.AddItem(new MenuItem("AttackPetsnTraps", "Auto attack pets & traps").SetShared().SetValue(true));
                misc.AddItem(new MenuItem("Smallminionsprio", "Jungle clear small first").SetShared().SetValue(false));
                _config.AddSubMenu(misc);
                
                _config.AddItem(
                    new MenuItem("ExtraMoveup", "Move delay After AA").SetValue(new Slider(50, 0, 100)));


                /*Load the menu*/

                _config.AddItem(
                    new MenuItem("LastHit", "Last hit").SetValue(new KeyBind('X', KeyBindType.Press)));

                _config.AddItem(new MenuItem("Harass", "Harass").SetValue(new KeyBind('C', KeyBindType.Press)));

                _config.AddItem(new MenuItem("Harass.MLH", "Lasthit While Harass").SetValue(true));

                _config.AddItem(
                    new MenuItem("LaneClear", "LaneClear").SetValue(new KeyBind('V', KeyBindType.Press)));

                _config.AddItem(
                    new MenuItem("Orbwalk", "Combo").SetValue(new KeyBind(32, KeyBindType.Press)));

                Player = ObjectManager.Player;
                Game.OnUpdate += GameOnOnGameUpdate;
                Drawing.OnDraw += DrawingOnOnDraw;
                Instances.Add(this);
            }

            public virtual bool InAutoAttackRange(AttackableUnit target)=> Orbwalking.InAutoAttackRange(target);
            
            public OrbwalkingMode ActiveMode
            {
                get
                {
                    if (_mode != OrbwalkingMode.None)
                    {
                        return _mode;
                    }

                    if (_config.Item("Orbwalk").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Combo;
                    }
                    
                    if (_config.Item("LaneClear").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LaneClear;
                    }

                    if (_config.Item("Harass").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.Mixed;
                    }

                    if (_config.Item("LastHit").GetValue<KeyBind>().Active)
                    {
                        return OrbwalkingMode.LastHit;
                    }
                    
                    return OrbwalkingMode.None;
                }
                set { _mode = value; }
            }

            /// <summary>
            ///     Enables or disables the auto-attacks.
            /// </summary>
            public void SetAttack(bool b)
            {
                Attack = b;
            }

            /// <summary>
            ///     Enables or disables the movement.
            /// </summary>
            public void SetMovement(bool b)
            {
                Move = b;
            }

            /// <summary>
            ///     Forces the orbwalker to attack the set target if valid and in range.
            /// </summary>
            public void ForceTarget(Obj_AI_Base target)
            {
                _forcedTarget = target;
            }

            /// <summary>
            ///     Forces the orbwalker to move to that point while orbwalking (Game.CursorPos by default).
            /// </summary>
            public void SetOrbwalkingPoint(Vector3 point)
            {
                _orbwalkingPoint = point;
            }

            private bool ShouldWait()
            {
                return
                    ObjectManager.Get<Obj_AI_Minion>()
                        .Any(
                            minion =>
                                minion.IsValidTarget() && minion.Team != GameObjectTeam.Neutral &&
                                InAutoAttackRange(minion) && MinionManager.IsMinion(minion) &&
                                HealthPrediction.LaneClearHealthPrediction(
                                    minion, (int)((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), 0) <=
                                Player.GetAutoAttackDamage(minion));
            }

            public virtual AttackableUnit GetTarget()
            {
                AttackableUnit result = null;

                if ((ActiveMode == OrbwalkingMode.Mixed || ActiveMode == OrbwalkingMode.LaneClear) &&
                    !_config.Item("PriorizeFarm").GetValue<bool>())
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                    if (target != null && InAutoAttackRange(target))
                    {
                        return target;
                    }
                }

                /*Killable Minion*/
                if (ActiveMode == OrbwalkingMode.LaneClear || ActiveMode == OrbwalkingMode.Mixed && _config.Item("Harass.MLH").GetValue<bool>() ||
                    ActiveMode == OrbwalkingMode.LastHit)
                {
                    var MinionList =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                minion =>
                                    minion.IsValidTarget() && InAutoAttackRange(minion))
                                    .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                                    .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                                    .ThenBy(minion => minion.Health)
                                    .ThenByDescending(minion => minion.MaxHealth);

                    foreach (var minion in MinionList)
                    {
                        var t = (int)(Player.AttackCastDelay * 1000) - 100 +
                                1000 * (int)Math.Max(0, Player.Distance(minion) - Player.BoundingRadius) / int.MaxValue;
                        var predHealth = HealthPrediction.GetHealthPrediction(minion, t, 0);

                        if (minion.Team != GameObjectTeam.Neutral && (_config.Item("AttackPetsnTraps").GetValue<bool>() || MinionManager.IsMinion(minion, _config.Item("AttackWards").GetValue<bool>())))
                        {
                            if (predHealth <= 0)
                            {
                                FireOnNonKillableMinion(minion);
                            }

                            if (predHealth > 0 && predHealth <= Player.GetAutoAttackDamage(minion, true))
                            {
                                return minion;
                            }
                        }
                    }
                }

                //Forced target
                if (_forcedTarget.IsValidTarget() && InAutoAttackRange(_forcedTarget))
                {
                    return _forcedTarget;
                }

                /* turrets / inhibitors / nexus */
                if (ActiveMode == OrbwalkingMode.LaneClear)
                {
                    /* turrets */
                    foreach (var turret in
                        ObjectManager.Get<Obj_AI_Turret>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* inhibitor */
                    foreach (var turret in
                        ObjectManager.Get<Obj_BarracksDampener>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return turret;
                    }

                    /* nexus */
                    foreach (var nexus in
                        ObjectManager.Get<Obj_HQ>().Where(t => t.IsValidTarget() && InAutoAttackRange(t)))
                    {
                        return nexus;
                    }
                }

                /*Champions*/
                if (ActiveMode != OrbwalkingMode.LastHit && !Player.HasBuff("LucianR"))
                {
                    var target = TargetSelector.GetTarget(-1, TargetSelector.DamageType.Physical);
                    if (target.IsValidTarget() && InAutoAttackRange(target))
                    {
                        return target;
                    }
                }

                /*Jungle minions*/
                if (ActiveMode == OrbwalkingMode.LaneClear || ActiveMode == OrbwalkingMode.Mixed)
                {
                    var jminions =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                mob =>
                                    mob.IsValidTarget() && mob.Team == GameObjectTeam.Neutral && InAutoAttackRange(mob) &&
                                    mob.CharData.BaseSkinName != "gangplankbarrel");

                    result = _config.Item("Smallminionsprio").GetValue<bool>() ? jminions.MinOrDefault(mob => mob.MaxHealth) : jminions.MaxOrDefault(mob => mob.MaxHealth);

                    if (result != null)
                    {
                        return result;
                    }
                }

                /*Lane Clear minions*/
                if (ActiveMode == OrbwalkingMode.LaneClear)
                {
                    if (!ShouldWait())
                    {
                        if (_prevMinion.IsValidTarget() && InAutoAttackRange(_prevMinion))
                        {
                            var predHealth = HealthPrediction.LaneClearHealthPrediction(
                                _prevMinion, (int)((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), 0);
                            if (predHealth >= 2 * Player.GetAutoAttackDamage(_prevMinion) ||
                                Math.Abs(predHealth - _prevMinion.Health) < float.Epsilon)
                            {
                                return _prevMinion;
                            }
                        }

                        result = (from minion in
                                      ObjectManager.Get<Obj_AI_Minion>()
                                          .Where(minion => minion.IsValidTarget() && InAutoAttackRange(minion) &&
                                          (_config.Item("AttackWards").GetValue<bool>() || !MinionManager.IsWard(minion)) &&
                                          (_config.Item("AttackPetsnTraps").GetValue<bool>() && minion.CharData.BaseSkinName != "jarvanivstandard" || MinionManager.IsMinion(minion, _config.Item("AttackWards").GetValue<bool>())) &&
                                          minion.CharData.BaseSkinName != "gangplankbarrel")
                                  let predHealth =
                                      HealthPrediction.LaneClearHealthPrediction(
                                          minion, (int)((Player.AttackDelay * 1000) * LaneClearWaitTimeMod), 0)
                                  where
                                      predHealth >= 2 * Player.GetAutoAttackDamage(minion) ||
                                      Math.Abs(predHealth - minion.Health) < float.Epsilon
                                  select minion).MaxOrDefault(m => !MinionManager.IsMinion(m, true) ? float.MaxValue : m.Health);

                        if (result != null)
                        {
                            _prevMinion = (Obj_AI_Minion)result;
                        }
                    }
                }

                return result;
            }

            private void GameOnOnGameUpdate(EventArgs args)
            {

                if (ActiveMode == OrbwalkingMode.None)
                {
                    return;
                }

                var target = GetTarget();
                if (target.IsValidTarget() && CanAttack && Attack) Player.IssueOrder(GameObjectOrder.AttackUnit, target);
                else if (CanMove(_config.Item("ExtraMoveup").GetValue<Slider>().Value) && Move) Player.IssueOrder(GameObjectOrder.MoveTo, (_orbwalkingPoint.To2D().IsValid()) ? _orbwalkingPoint : Game.CursorPos);
            }
            
            private void DrawingOnOnDraw(EventArgs args)
            {
                if (_config.Item("AACircle").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        Player.Position, GetRealAutoAttackRange(null) + 65,
                        _config.Item("AACircle").GetValue<Circle>().Color,
                        _config.Item("AALineWidth").GetValue<Slider>().Value);
                }

                if (_config.Item("AACircle2").GetValue<Circle>().Active)
                {
                    foreach (var target in
                        HeroManager.Enemies.FindAll(target => target.IsValidTarget(1175)))
                    {
                        Render.Circle.DrawCircle(
                            target.Position, GetAttackRange(target),
                            _config.Item("AACircle2").GetValue<Circle>().Color,
                            _config.Item("AALineWidth").GetValue<Slider>().Value);
                    }
                }

                if (_config.Item("HoldZone").GetValue<Circle>().Active)
                {
                    Render.Circle.DrawCircle(
                        Player.Position, _config.Item("HoldPosRadius").GetValue<Slider>().Value,
                        _config.Item("HoldZone").GetValue<Circle>().Color,
                        _config.Item("AALineWidth").GetValue<Slider>().Value, true);
                }

            }
        }
    }
}
