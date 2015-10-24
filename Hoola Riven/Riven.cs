using System;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using Color = System.Drawing.Color;
using ItemData = LeagueSharp.Common.Data.ItemData;
using SharpDX;

namespace HoolaRiven
{
    internal class Riven
    {
        private static Menu Menu;
        private static Orbwalking.Orbwalker Orbwalker;
        private static Obj_AI_Hero Player = ObjectManager.Player;
        private static string IsFirstR = "RivenFengShuiEngine";
        private static string IsSecondR = "rivenizunablade";
        private static SpellSlot Flash = Player.GetSpellSlot("summonerFlash");
        private static HpBarIndicator Indicator = new HpBarIndicator();
        private static Spell Q, W, E, R;
        private static int QStack = 1;
        private static bool forceQ;
        private static float lastQ;
        private static AttackableUnit QTarget = null;
        private static bool Dind { get { return Menu.Item("Dind").GetValue<bool>(); } }
        private static bool DrawCB { get { return Menu.Item("DrawCB").GetValue<bool>(); } }
        private static bool KillstealW { get { return Menu.Item("killstealw").GetValue<bool>(); } }
        private static bool KillstealR { get { return Menu.Item("killstealr").GetValue<bool>(); } }
        private static bool DrawAlwaysR { get { return Menu.Item("DrawAlwaysR").GetValue<bool>(); } }
        private static bool DrawUseHoola { get { return Menu.Item("DrawUseHoola").GetValue<bool>(); } }
        private static bool DrawFH { get { return Menu.Item("DrawFH").GetValue<bool>(); } }
        private static bool DrawHS { get { return Menu.Item("DrawHS").GetValue<bool>(); } }
        private static bool DrawBT { get { return Menu.Item("DrawBT").GetValue<bool>(); } }
        private static bool UseHoola { get { return Menu.Item("UseHoola").GetValue<KeyBind>().Active; } }
        private static bool AlwaysR { get { return Menu.Item("AlwaysR").GetValue<KeyBind>().Active; } }
        private static bool AutoShield { get { return Menu.Item("AutoShield").GetValue<bool>(); } }
        private static bool Shield { get { return Menu.Item("Shield").GetValue<bool>(); } }
        private static bool KeepQ { get { return Menu.Item("KeepQ").GetValue<bool>(); } }
        private static int QD { get { return Menu.Item("QD").GetValue<Slider>().Value; } }
        private static int QLD { get { return Menu.Item("QLD").GetValue<Slider>().Value; } }
        private static int AutoW { get { return Menu.Item("AutoW").GetValue<Slider>().Value; } }
        private static bool ComboW { get { return Menu.Item("ComboW").GetValue<bool>(); } }
        private static bool RMaxDam { get { return Menu.Item("RMaxDam").GetValue<bool>(); } }
        private static bool RKillable { get { return Menu.Item("RKillable").GetValue<bool>(); } }
        private static int LaneW { get { return Menu.Item("LaneW").GetValue<Slider>().Value; } }
        private static bool LaneE { get { return Menu.Item("LaneE").GetValue<bool>(); } }
        private static bool WInterrupt { get { return Menu.Item("WInterrupt").GetValue<bool>(); } }
        private static bool Qstrange { get { return Menu.Item("Qstrange").GetValue<bool>(); } }
        private static bool FirstHydra { get { return Menu.Item("FirstHydra").GetValue<bool>(); } }
        private static bool LaneQ { get { return Menu.Item("LaneQ").GetValue<bool>(); } }

        public Riven()
        {
            if (Player.ChampionName != "Riven") return;
            Game.PrintChat("Hoola Riven - Loaded Successfully, Good Luck! :)");
            Q = new Spell(SpellSlot.Q);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 300);
            R = new Spell(SpellSlot.R, 900);
            R.SetSkillshot(0.25f, 45, 1600, false, SkillshotType.SkillshotCone);

            OnMenuLoad();

            Game.OnUpdate += Game_OnGameUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Drawing.OnEndScene += Drawing_OnEndScene;
            Orbwalking.AfterAttack += Orbwalking_AfterAttack;
            Orbwalking.AfterAttack += Orbwalking_AfterAttacklc;
            Obj_AI_Base.OnProcessSpellCast += OnCast;
            Obj_AI_Base.OnPlayAnimation += OnPlay;
            Obj_AI_Base.OnProcessSpellCast += OnCasting;
            Interrupter2.OnInterruptableTarget += interrupt;
        }

        private static void OnMenuLoad()
        {
            Menu = new Menu("Hoola Riven", "hoolariven", true);
            Menu ts = Menu.AddSubMenu(new Menu("Target Selector", "Target Selector"));
            TargetSelector.AddToMenu(ts);
            var orbwalker = new Menu("Orbwalk", "rorb");
            Orbwalker = new Orbwalking.Orbwalker(orbwalker);
            Menu.AddSubMenu(orbwalker);
            var Combo = new Menu("Combo", "Combo");

            Combo.AddItem(new MenuItem("AlwaysR", "Always Use R (Toggle)").SetValue(new KeyBind('G', KeyBindType.Toggle)));
            Combo.AddItem(new MenuItem("UseHoola", "Use Hoola Combo Logic (Toggle)").SetValue(new KeyBind('L', KeyBindType.Toggle)));
            Combo.AddItem(new MenuItem("ComboW", "Always use W").SetValue(true));
            Combo.AddItem(new MenuItem("RKillable", "Use R When Target Can Killable").SetValue(true));


            Menu.AddSubMenu(Combo);
            var Lane = new Menu("Lane", "Lane");
            Lane.AddItem(new MenuItem("LaneQ", "Use Q While Laneclear").SetValue(true));
            Lane.AddItem(new MenuItem("LaneW", "Use W X Minion").SetValue(new Slider(5, 0, 5)));
            Lane.AddItem(new MenuItem("LaneE", "Use E While Laneclear").SetValue(true));



            Menu.AddSubMenu(Lane);
            var Misc = new Menu("Misc", "Misc");

            Misc.AddItem(new MenuItem("FirstHydra", "Burst Hydra Cast before W").SetValue(false));
            Misc.AddItem(new MenuItem("Qstrange", "Strange Q For Speed").SetValue(false));
            Misc.AddItem(new MenuItem("Winterrupt", "W interrupt").SetValue(true));
            Misc.AddItem(new MenuItem("AutoW", "Auto W When x Enemy").SetValue(new Slider(5, 0, 5)));
            Misc.AddItem(new MenuItem("RMaxDam", "Use Second R Max Damage").SetValue(true));
            Misc.AddItem(new MenuItem("killstealw", "Killsteal W").SetValue(true));
            Misc.AddItem(new MenuItem("killstealr", "Killsteal Second R").SetValue(true));
            Misc.AddItem(new MenuItem("AutoShield", "Auto Cast E").SetValue(true));
            Misc.AddItem(new MenuItem("Shield", "Auto Cast E While LastHit").SetValue(true));
            Misc.AddItem(new MenuItem("KeepQ", "Keep Q Alive").SetValue(true));
            Misc.AddItem(new MenuItem("QD", "First,Second Q Delay").SetValue(new Slider(29, 23, 43)));
            Misc.AddItem(new MenuItem("QLD", "Third Q Delay").SetValue(new Slider(39, 36, 53)));


            Menu.AddSubMenu(Misc);

            var Draw = new Menu("Draw", "Draw");

            Draw.AddItem(new MenuItem("DrawAlwaysR", "Draw Always R Status").SetValue(true));
            Draw.AddItem(new MenuItem("DrawUseHoola", "Draw Hoola Logic Status").SetValue(true));
            Draw.AddItem(new MenuItem("Dind", "Draw Damage Indicator").SetValue(true));
            Draw.AddItem(new MenuItem("DrawCB", "Draw Combo Engage Range").SetValue(true));
            Draw.AddItem(new MenuItem("DrawBT", "Draw Burst Engage Range").SetValue(true));
            Draw.AddItem(new MenuItem("DrawFH", "Draw FastHarass Engage Range").SetValue(true));
            Draw.AddItem(new MenuItem("DrawHS", "Draw Harass Engage Range").SetValue(true));

            Menu.AddSubMenu(Draw);

            var Credit = new Menu("Credit", "Credit");

            Credit.AddItem(new MenuItem("hoola", "Made by Hoola :)"));
            Credit.AddItem(new MenuItem("notfixe", "If High ping will be many buggy"));
            Credit.AddItem(new MenuItem("notfixed", "Not Fixed Anything Yet"));
            Credit.AddItem(new MenuItem("feedback", "So Feedback To Hoola!"));

            Menu.AddSubMenu(Credit);

            Menu.AddToMainMenu();
        }

        public static void interrupt(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            if (sender.IsEnemy && W.IsReady() && sender.IsValidTarget() && !sender.IsZombie && WInterrupt)
            {
                if (sender.IsValidTarget(125 + Player.BoundingRadius + sender.BoundingRadius)) W.Cast();
            }
        }

        static void Game_OnGameUpdate(EventArgs args)
        {
            statereset();
            UseRMaxDam();
            killsteal();
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo) Combo();
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Burst) Burst();
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) Jungleclear();
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.FastHarass) FastHarass();
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) Harass();
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Flee) Flee();
            if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Flee) Orbwalker.SetAttack(true);
        }

        static void killsteal()
        {
            if (KillstealW && W.IsReady())
            {
                var targets = HeroManager.Enemies.Where(x => x.IsValidTarget(R.Range) && !x.IsZombie);
                foreach (var target in targets)
                {
                    if (target.Health < W.GetDamage2(target) && InWRange(target))
                        W.Cast();
                }
            }
            if (KillstealR && R.IsReady() && R.Instance.Name == IsSecondR)
            {
                var targets = HeroManager.Enemies.Where(x => x.IsValidTarget(R.Range) && !x.IsZombie);
                foreach (var target in targets)
                {
                    if (target.Health < Rdame(target, target.Health) && (!target.HasBuff2("kindrednodeathbuff") && !target.HasBuff2("Undying Rage") && !target.HasBuff2("JudicatorIntervention")))
                        R.Cast(target.Position);
                }
            }
        }
        static void UseRMaxDam()
        {
            if (RMaxDam && R.IsReady() && R.Instance.Name == IsSecondR)
            {
                var targets = HeroManager.Enemies.Where(x => x.IsValidTarget(R.Range) && !x.IsZombie);
                foreach (var target in targets)
                {
                    if (target.HealthPercent <= 25 && (!target.HasBuff2("kindrednodeathbuff") && !target.HasBuff2("Undying Rage") && !target.HasBuff2("JudicatorIntervention")))
                        R.Cast(target.Position);
                }
            }
        }

        static void Drawing_OnDraw(EventArgs args)
        {
            if (Player.IsDead)
                return;
            var heropos = Drawing.WorldToScreen(ObjectManager.Player.Position);

            if (DrawCB) Render.Circle.DrawCircle(Player.Position, 250 + Player.AttackRange + 70, E.IsReady() ? Color.FromArgb(120, 0, 170, 255) : Color.IndianRed);
            if (DrawBT && Flash != SpellSlot.Unknown) Render.Circle.DrawCircle(Player.Position, 800, R.IsReady() && Flash.IsReady() ? Color.FromArgb(120, 0, 170, 255) : Color.IndianRed);
            if (DrawFH) Render.Circle.DrawCircle(Player.Position, 340 + Player.AttackRange + 70, E.IsReady() && Q.IsReady() ? Color.FromArgb(120, 0, 170, 255) : Color.IndianRed);
            if (DrawHS) Render.Circle.DrawCircle(Player.Position, 310, Q.IsReady() && W.IsReady() ? Color.FromArgb(120, 0, 170, 255) : Color.IndianRed);
            if (DrawAlwaysR) Drawing.DrawText(heropos.X, heropos.Y + 20, Color.Cyan, AlwaysR ? "Always R On" : "Always R Off");
            if (DrawUseHoola) Drawing.DrawText(heropos.X, heropos.Y + 50, Color.Cyan, UseHoola ? "Hoola Logic On" : "Hoola Logic Off");
        }

        private static void Drawing_OnEndScene(EventArgs args)
        {
            foreach (
                var enemy in
                    ObjectManager.Get<Obj_AI_Hero>()
                        .Where(ene => ene.IsValidTarget() && !ene.IsZombie))
            {
                if (Dind)
                {
                    Indicator.unit = enemy;
                    Indicator.drawDmg(getComboDamage(enemy), new ColorBGRA(255, 204, 0, 160));
                }

            }
        }

        static void Jungleclear()
        {

            var Mobs = MinionManager.GetMinions(250 + Player.AttackRange + 70, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

            if (Mobs.Count <= 0)
                return;

            if (W.IsReady() && E.IsReady() && !Orbwalking.InAutoAttackRange(Mobs[0]))
            {
                E.Cast(Mobs[0].Position);
                Utility.DelayAction.Add(10, () => UseCastItem(300));
                Utility.DelayAction.Add(170, () => UseW(300));
            }
        }

        static void Combo()
        {
            var targetR = TargetSelector.GetTarget(250 + Player.AttackRange + 70, TargetSelector.DamageType.Physical);
            if (R.IsReady() && R.Instance.Name == IsFirstR && Orbwalker.InAutoAttackRange(targetR) && AlwaysR && targetR != null) R.Cast();
            if (R.IsReady() && R.Instance.Name == IsFirstR && W.IsReady() && InWRange(targetR) && ComboW && AlwaysR && targetR != null)
            {
                R.Cast();
                UseW(500);
            }
            if (W.IsReady() && InWRange(targetR) && ComboW && targetR != null) W.Cast();
            if (UseHoola && R.IsReady() && R.Instance.Name == IsFirstR && W.IsReady() && targetR != null && E.IsReady() && targetR.IsValidTarget() && !targetR.IsZombie && (IsKillableR(targetR) || AlwaysR))
            {
                if (!InWRange(targetR))
                {
                    E.Cast(targetR.Position);
                    R.Cast();
                    Utility.DelayAction.Add(170, () => UseW(270));
                    Utility.DelayAction.Add(310, () => forcecastQ(targetR));
                }
            }
            else if (!UseHoola && R.IsReady() && R.Instance.Name == IsFirstR && W.IsReady() && targetR != null && E.IsReady() && targetR.IsValidTarget() && !targetR.IsZombie && (IsKillableR(targetR) || AlwaysR))
            {
                if (!InWRange(targetR))
                {
                    E.Cast(targetR.Position);
                    R.Cast();
                    Utility.DelayAction.Add(170, () => UseW(300));
                }
            }
            else if (UseHoola && W.IsReady() && E.IsReady())
            {
                if (targetR.IsValidTarget() && targetR != null && !targetR.IsZombie && !InWRange(targetR))
                {
                    E.Cast(targetR.Position);
                    Utility.DelayAction.Add(10, () => UseCastItem(500));
                    Utility.DelayAction.Add(170, () => UseW(500));
                    Utility.DelayAction.Add(310, () => forcecastQ(targetR));
                }
            }
            else if (!UseHoola && W.IsReady() && targetR != null && E.IsReady())
            {
                if (targetR.IsValidTarget() && targetR != null && !targetR.IsZombie && !InWRange(targetR))
                {
                    E.Cast(targetR.Position);
                    Utility.DelayAction.Add(10, () => UseCastItem(500));
                    Utility.DelayAction.Add(170, () => UseW(500));
                }
            }
            else if (E.IsReady())
            {
                if (targetR.IsValidTarget() && !targetR.IsZombie && targetR != null && !InWRange(targetR))
                {
                    E.Cast(targetR.Position);
                }
            }
        }

        static void Burst()
        {
            var target = TargetSelector.GetSelectedTarget();
            if (target != null && target.IsValidTarget() && !target.IsZombie)
            {
                if (Flash != SpellSlot.Unknown && Flash.IsReady()
                    && R.IsReady() && R.Instance.Name == IsFirstR && Player.Distance(target.Position) <= 800 && !FirstHydra)
                {
                    E.Cast(Player.Position.Extend(target.Position, 200));
                    R.Cast();
                    Utility.DelayAction.Add(160, () => FlashW());
                }
                else if (Flash != SpellSlot.Unknown && Flash.IsReady()
                    && R.IsReady() && R.Instance.Name == IsFirstR && Player.Distance(target.Position) <= 800 && FirstHydra)
                {
                    E.Cast(Player.Position.Extend(target.Position, 200));
                    R.Cast();
                    UseCastItem(200);
                    Utility.DelayAction.Add(230, () => FlashW());
                }
            }
        }

        static void FastHarass()
        {
            if (Q.IsReady() && E.IsReady())
            {
                var target = TargetSelector.GetTarget(340 + Player.AttackRange + 70, TargetSelector.DamageType.Physical);
                if (target.IsValidTarget() && !target.IsZombie)
                {
                    if (!Orbwalking.InAutoAttackRange(target) && !InWRange(target)) E.Cast(target.Position);
                    Utility.DelayAction.Add(10, () => UseCastItem(500));
                    Utility.DelayAction.Add(130, () => forcecastQ(target));
                }
            }
        }

        static void Harass()
        {
            var target = TargetSelector.GetTarget(310, TargetSelector.DamageType.Physical);
            if (Q.IsReady() && W.IsReady() && E.IsReady() && QStack == 1)
            {
                if (target.IsValidTarget() && !target.IsZombie)
                {
                    Q.Cast(target.Position);
                    UseW(1000);
                }
            }
            if (Q.IsReady() && E.IsReady() && QStack == 3 && !Orbwalking.CanAttack() && Orbwalking.CanMove(10))
            {
                var epos = Player.ServerPosition +
                          (Player.ServerPosition - target.ServerPosition).Normalized() * 300;
                E.Cast(epos);
                Utility.DelayAction.Add(190, () => Q.Cast(epos));
            }
        }

        static void Flee()
        {
            Orbwalker.SetAttack(false);
            var x = Player.Position.Extend(Game.CursorPos, 300);
            if (Q.IsReady() && !Player.IsDashing()) Q.Cast(x);
            if (E.IsReady() && !Player.IsDashing()) E.Cast(x);
        }

        static void OnPlay(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            if (sender.IsMe && (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LastHit && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Flee))
            {
                switch (args.Animation)
                {
                    case "Spell1a":
                        Utility.DelayAction.Add((QD * 10) + 1, () => Reset());
                        break;
                    case "Spell1b":
                        Utility.DelayAction.Add((QD * 10) + 1, () => Reset());
                        break;
                    case "Spell1c":
                        Utility.DelayAction.Add((QLD * 10) + 3, () => Reset());
                        break;
                }
            }
        }

        static void OnCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsMe) return;

            if (args.SData.Name == "RivenTriCleave")
            {
                forceQ = false;
                lastQ = Utils.GameTimeTickCount;
                if (Qstrange && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Flee) Game.Say("/d");
                QStack += 1;
            }
            if (args.SData.Name.Contains("rivenizunablade"))
            {
                var target = TargetSelector.GetSelectedTarget();
                if (Q.IsReady() && target.IsValidTarget()) forcecastQ(target);
            }
        }

        static void Reset()
        {
            Game.Say("/d");
            Orbwalking.LastAATick = 0;
            Player.IssueOrder(GameObjectOrder.MoveTo, Player.Position.Extend(Game.CursorPos, Player.Distance(Game.CursorPos) + 10));
        }

        static bool InWRange(AttackableUnit target)
        {
            if (Player.HasBuff("RivenFengShuiEngine") && target != null)
            {
                return
                    70 + 195 + Player.BoundingRadius >= Player.Distance(target.Position);
            }
            else
            {
                return
                   70 + 120 + Player.BoundingRadius >= Player.Distance(target.Position);
            }
        }

        static void saveq()
        {
            if (QStack != 1)
            {
                if (Q.IsReady())
                {
                    Q.Cast(Game.CursorPos);
                }
            }
        }

        static void statereset()
        {
            if (AutoW > 0)
            {
                float wrange = 0;
                if (Player.HasBuff("RivenFengShuiEngine"))
                {
                    wrange = 195 + Player.BoundingRadius + 70;
                    if (Player.CountEnemiesInRange(wrange) >= AutoW)
                    {
                        W.Cast();
                    }
                }
                else
                {
                    wrange = 120 + Player.BoundingRadius + 70;
                    if (Player.CountEnemiesInRange(wrange) >= AutoW)
                    {
                        W.Cast();
                    }
                }

            }
            if (Utils.GameTimeTickCount - lastQ >= 3650 && QStack != 1 && !Player.IsRecalling() && KeepQ) saveq();
            if (!Q.IsReady(500) || QStack == 4) QStack = 1;
            if (forceQ == true && Orbwalking.CanMove(10))
            {
                var target = TargetSelector.GetTarget(310, TargetSelector.DamageType.Physical);
                //if (Utils.GameTimeTickCount - cQ >= 350 + Player.AttackCastDelay - Game.Ping / 2)
                if (Q.IsReady() && QTarget.IsValidTarget())
                    Q.Cast(QTarget.Position);
                else if (Q.IsReady() && target.IsValidTarget() && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LastHit && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear)
                {
                    Q.Cast(target.Position);
                }
                else if (Q.IsReady() && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear)
                {
                    var Minions = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.Enemy);

                    if (Minions.Count <= 0)
                        return;

                    Q.Cast(Minions[0].Position);
                }
                else
                    Q.Cast(Game.CursorPos);
                //else
                //    forceQ = false;
            }
            if (QTarget == null)
            {
                var Minions = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.Enemy);

                if (Minions.Count <= 0)
                    return;

                QTarget = Minions[0];
            }
        }

        private static void forcecastQ(AttackableUnit target)
        {
            forceQ = true;
            QTarget = target;
        }

        static void Orbwalking_AfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            QTarget = target;
            if (!unit.IsMe || !target.IsValidTarget()) return;

            if (KillstealR && R.IsReady() && R.Instance.Name == IsSecondR && target is Obj_AI_Hero)
            {
                var targets = HeroManager.Enemies.Where(x => x.IsValidTarget(Orbwalking.GetRealAutoAttackRange(unit)) && !x.IsZombie);
                foreach (var targetR in targets)
                {
                    if (targetR.Health < (Rdame(targetR, targetR.Health) + Player.GetAutoAttackDamage2(targetR)) && targetR.Health > Player.GetAutoAttackDamage2(targetR))
                        R.Cast(targetR.Position);
                }
            }

            if (KillstealW && W.IsReady() && target is Obj_AI_Hero)
            {
                var targets = HeroManager.Enemies.Where(x => x.IsValidTarget(Orbwalking.GetRealAutoAttackRange(unit)) && !x.IsZombie);
                foreach (var targetR in targets)
                {
                    if (targetR.Health < (W.GetDamage2(targetR) + Player.GetAutoAttackDamage2(targetR)) && targetR.Health > Player.GetAutoAttackDamage2(targetR))
                        W.Cast();
                }
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                if (Q.IsReady() && HasItem())
                {
                    UseCastItem(500);
                    forcecastQ(QTarget);
                }
                else if (Q.IsReady()) forcecastQ(QTarget);
                else if (W.IsReady() && InWRange(target)) W.Cast();
                else if (E.IsReady() && !Orbwalking.InAutoAttackRange(target)) E.Cast(target.Position);
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.FastHarass && target is Obj_AI_Hero)
            {
                if (W.IsReady() && InWRange(target))
                {
                    W.Cast();
                    if (Q.IsReady() && QStack != 3)
                    {
                        forcecastQ(QTarget);
                    }
                }
                else if (HasItem() && Q.IsReady())
                {
                    UseCastItem(200);
                    forcecastQ(QTarget);
                }
                else if (Q.IsReady())
                {
                    forcecastQ(QTarget);
                }
                else if (E.IsReady() && !Orbwalking.InAutoAttackRange(target) && !InWRange(target))
                {
                    E.Cast(target.Position);
                }
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed && QStack == 2)
            {
                UseCastItem(300);
                forcecastQ(QTarget);
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Burst && target is Obj_AI_Hero)
            {
                if (HasItem())
                {
                    if (R.IsReady() && R.Instance.Name == IsSecondR)
                    {
                        var targets = TargetSelector.GetSelectedTarget();
                        UseCastItem(300);
                        UseR(300);
                    }
                    if (Q.IsReady() && !R.IsReady())
                    {
                        forcecastQ(QTarget);

                    }
                }
                else if (R.IsReady() && R.Instance.Name == IsSecondR)
                {
                    var targets = TargetSelector.GetSelectedTarget();
                    UseR(300);
                }
                if (!R.IsReady() && Q.IsReady())
                {
                    forcecastQ(QTarget);
                }
            }

        }

        static void Orbwalking_AfterAttacklc(AttackableUnit unit, AttackableUnit target)
        {
            QTarget = target;
            if (!unit.IsMe && target != null) return;

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && LaneQ && (target is Obj_Building || target is Obj_AI_Turret || target is Obj_BarracksDampener))
            {
                if (Q.IsReady())
                    forcecastQ(QTarget);
            }
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && LaneQ)
            {
                if (target is Obj_Building || target is Obj_AI_Turret || target is Obj_Barracks || target is Obj_BarracksDampener)
                    return;
                if (Q.IsReady() && HasItem())
                {
                    UseCastItem(300);
                    forcecastQ(QTarget);
                }
                else if (W.IsReady())
                {
                    float wrange = 0;
                    if (Player.HasBuff("RivenFengShuiEngine"))
                    {
                        wrange = 195 + Player.BoundingRadius + 70;
                        var Minions = MinionManager.GetMinions(wrange, MinionTypes.All, MinionTeam.Enemy);
                        if (Minions[0].IsValidTarget() && Minions.Count >= LaneW && LaneW != 0 &&
                            Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                        {
                            W.Cast();
                        }
                    }
                    else
                    {
                        wrange = 120 + Player.BoundingRadius + 70;
                        var Minions = MinionManager.GetMinions(wrange, MinionTypes.All, MinionTeam.Enemy);
                        if (Minions[0].IsValidTarget() && Minions.Count >= LaneW && LaneW != 0 &&
                            Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                        {
                            W.Cast();
                        }
                    }
                }
                else if (E.IsReady() && LaneE) E.Cast(target.Position);
                if (Q.IsReady())
                {
                    var Mobs = MinionManager.GetMinions(E.Range + Player.BoundingRadius, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                    if (Mobs[0].IsValidTarget() && Mobs.Count >= 1 &&
                        Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                    {
                        if (HasItem()) UseCastItem(300);
                        forcecastQ(QTarget);
                    }
                }
                else if (W.IsReady())
                {
                    float wrange = 0;
                    if (Player.HasBuff("RivenFengShuiEngine"))
                    {
                        wrange = 195 + Player.BoundingRadius + 70;
                        var Minions = MinionManager.GetMinions(wrange, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                        if (Minions[0].IsValidTarget() && Minions.Count >= 1 &&
                            Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                        {
                            W.Cast();
                        }
                    }
                    else
                    {
                        wrange = 120 + Player.BoundingRadius + 70;
                        var Minions = MinionManager.GetMinions(wrange, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                        if (Minions[0].IsValidTarget() && Minions.Count >= 1 && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                        {
                            W.Cast();
                        }
                    }
                }
                var Mob = MinionManager.GetMinions(E.Range + Player.BoundingRadius, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);

                if (!W.IsReady() && E.IsReady() && Mob[0].IsValidTarget() && Mob.Count >= 1) E.Cast(Mob[0].Position);
            }
        }

        static void UseR(int t)
        {
            var target = TargetSelector.GetSelectedTarget();
            if (target != null && target.IsValidTarget() && !target.IsZombie && R.IsReady() && R.Instance.Name == IsSecondR)
            {
                for (int i = 0; i < t; i = i + 1)
                {
                    Utility.DelayAction.Add(i, () => R.Cast(target.Position));
                }
            }
        }

        static void UseW(int t)
        {
            for (int i = 0; i < t; i = i + 1)
            {
                if (W.IsReady())
                    Utility.DelayAction.Add(i, () => W.Cast());
            }
        }
        static void UseCastItem(int t)
        {
            for (int i = 0; i < t; i = i + 1)
            {
                if (HasItem())
                    Utility.DelayAction.Add(i, () => CastItem());
            }
        }

        private static void FlashW()
        {
            var target = TargetSelector.GetSelectedTarget();
            if (target != null && target.IsValidTarget() && !target.IsZombie)
            {
                W.Cast();
                Utility.DelayAction.Add(10, () => Player.Spellbook.CastSpell(Flash, target.Position));
            }
        }

        static bool HasItem()
        {
            if (ItemData.Tiamat_Melee_Only.GetItem().IsReady() || ItemData.Ravenous_Hydra_Melee_Only.GetItem().IsReady())
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        static void CastItem()
        {

            if (ItemData.Tiamat_Melee_Only.GetItem().IsReady())
                ItemData.Tiamat_Melee_Only.GetItem().Cast();
            if (ItemData.Ravenous_Hydra_Melee_Only.GetItem().IsReady())
                ItemData.Ravenous_Hydra_Melee_Only.GetItem().Cast();
        }
        static void OnCasting(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            var targets = HeroManager.Enemies.Where(x => x.IsValidTarget() && !x.IsZombie && InWRange(x));
            if (sender.IsEnemy && sender.Type == Player.Type && (AutoShield || (Shield && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)))
            {
                var epos = Player.ServerPosition +
                          (Player.ServerPosition - sender.ServerPosition).Normalized() * 300;

                if (Player.Distance(sender.ServerPosition) <= args.SData.CastRange)
                {
                    switch (args.SData.TargettingType)
                    {
                        case SpellDataTargetType.Unit:

                            if (args.Target.NetworkId == Player.NetworkId)
                            {
                                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit && !args.SData.Name.Contains("NasusW"))
                                {
                                    if (E.IsReady()) E.Cast(epos);
                                }
                            }

                            break;
                        case SpellDataTargetType.SelfAoe:

                            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)
                            {
                                if (E.IsReady()) E.Cast(epos);
                            }

                            break;
                    }
                    if (args.SData.Name.Contains("IreliaEquilibriumStrike"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (W.IsReady() && InWRange(sender)) W.Cast();
                            else if (E.IsReady()) E.Cast(epos);
                        }
                    }
                    if (args.SData.Name.Contains("TalonCutthroat"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (W.IsReady()) W.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("RenektonPreExecute"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (W.IsReady()) W.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("GarenRPreCast"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast(epos);
                        }
                    }
                    if (args.SData.Name.Contains("GarenQAttack"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("XenZhaoThrust3"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (W.IsReady()) W.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("RengarQ"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("RengarPassiveBuffDash"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("RengarPassiveBuffDashAADummy"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("TwitchEParticle"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("FizzPiercingStrike"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("HungeringStrike"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("YasuoDash"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("KatarinaRTrigger"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (W.IsReady() && InWRange(sender)) W.Cast();
                            else if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("YasuoDash"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("KatarinaE"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (W.IsReady()) W.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("MonkeyKingQAttack"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("MonkeyKingSpinToWin"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                            else if (W.IsReady()) W.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("MonkeyKingQAttack"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("MonkeyKingQAttack"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                    if (args.SData.Name.Contains("MonkeyKingQAttack"))
                    {
                        if (args.Target.NetworkId == Player.NetworkId)
                        {
                            if (E.IsReady()) E.Cast();
                        }
                    }
                }
            }
        }

        static double basicdmg(Obj_AI_Base target)
        {
            if (target != null)
            {
                double dmg = 0;
                double passivenhan = 0;
                if (Player.Level >= 18) { passivenhan = 0.5; }
                else if (Player.Level >= 15) { passivenhan = 0.45; }
                else if (Player.Level >= 12) { passivenhan = 0.4; }
                else if (Player.Level >= 9) { passivenhan = 0.35; }
                else if (Player.Level >= 6) { passivenhan = 0.3; }
                else if (Player.Level >= 3) { passivenhan = 0.25; }
                else { passivenhan = 0.2; }
                if (HasItem()) dmg = dmg + Player.GetAutoAttackDamage(target) * 0.7;
                if (W.IsReady()) dmg = dmg + W.GetDamage(target);
                if (Q.IsReady())
                {
                    var qnhan = 4 - QStack;
                    dmg = dmg + Q.GetDamage(target) * qnhan + Player.GetAutoAttackDamage(target) * qnhan * (1 + passivenhan);
                }
                dmg = dmg + Player.GetAutoAttackDamage(target) * (1 + passivenhan);
                return dmg;
            }
            else { return 0; }
        }


        static float getComboDamage(Obj_AI_Base enemy)
        {
            if (enemy != null)
            {
                float damage = 0;
                float passivenhan = 0;
                if (Player.Level >= 18) { passivenhan = 0.5f; }
                else if (Player.Level >= 15) { passivenhan = 0.45f; }
                else if (Player.Level >= 12) { passivenhan = 0.4f; }
                else if (Player.Level >= 9) { passivenhan = 0.35f; }
                else if (Player.Level >= 6) { passivenhan = 0.3f; }
                else if (Player.Level >= 3) { passivenhan = 0.25f; }
                else { passivenhan = 0.2f; }
                if (HasItem()) damage = damage + (float)Player.GetAutoAttackDamage2(enemy) * 0.7f;
                if (W.IsReady()) damage = damage + W.GetDamage2(enemy);
                if (Q.IsReady())
                {
                    var qnhan = 4 - QStack;
                    damage = damage + Q.GetDamage2(enemy) * qnhan + (float)Player.GetAutoAttackDamage2(enemy) * qnhan * (1 + passivenhan);
                }
                damage = damage + (float)Player.GetAutoAttackDamage2(enemy) * (1 + passivenhan);
                if (R.IsReady())
                {
                    return damage * 1.2f + R.GetDamage2(enemy);
                }

                return damage;
            }
            else return 0;
        }

        static bool IsKillableR(Obj_AI_Hero target)
        {
            if (RKillable && target.IsValidTarget() && (totaldame(target) >= target.Health
                 && basicdmg(target) <= target.Health) || Player.CountEnemiesInRange(900) >= 2 && (!target.HasBuff2("kindrednodeathbuff") && !target.HasBuff2("Undying Rage") && !target.HasBuff2("JudicatorIntervention")))
            {
                return true;
            }
            else return false;
        }
        static double totaldame(Obj_AI_Base target)
        {
            if (target != null)
            {
                double dmg = 0;
                double passivenhan = 0;
                if (Player.Level >= 18) { passivenhan = 0.5; }
                else if (Player.Level >= 15) { passivenhan = 0.45; }
                else if (Player.Level >= 12) { passivenhan = 0.4; }
                else if (Player.Level >= 9) { passivenhan = 0.35; }
                else if (Player.Level >= 6) { passivenhan = 0.3; }
                else if (Player.Level >= 3) { passivenhan = 0.25; }
                else { passivenhan = 0.2; }
                if (HasItem()) dmg = dmg + Player.GetAutoAttackDamage(target) * 0.7;
                if (W.IsReady()) dmg = dmg + W.GetDamage(target);
                if (Q.IsReady())
                {
                    var qnhan = 4 - QStack;
                    dmg = dmg + Q.GetDamage(target) * qnhan + Player.GetAutoAttackDamage(target) * qnhan * (1 + passivenhan);
                }
                dmg = dmg + Player.GetAutoAttackDamage(target) * (1 + passivenhan);
                if (R.IsReady())
                {
                    var rdmg = Rdame(target, target.Health - dmg * 1.2);
                    return dmg * 1.2 + rdmg;
                }
                else return dmg;
            }
            else return 0;
        }
        static double Rdame(Obj_AI_Base target, double health)
        {
            if (target != null)
            {
                var missinghealth = (target.MaxHealth - health) / target.MaxHealth > 0.75 ? 0.75 : (target.MaxHealth - health) / target.MaxHealth;
                var pluspercent = missinghealth * (8 / 3);
                var rawdmg = new double[] { 80, 120, 160 }[R.Level - 1] + 0.6 * Player.FlatPhysicalDamageMod;
                return Player.CalcDamage(target, Damage.DamageType.Physical, rawdmg * (1 + pluspercent));
            }
            else return 0;
        }
    }
}
