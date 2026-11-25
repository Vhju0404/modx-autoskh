using Mod.ModHelper;

namespace AssemblyCSharp.Mod
{
    public class Attack : ThreadActionUpdate<Attack>
    {
        public override int Interval => 220;

        protected override void update()
        {
            if (!AutoGame.isAuto || AutoGame.isPaused || !PickMob.IsTanSat || GameEvents.Profile.FastAttackNoAnim)
                return;

            var myChar = global::Char.myCharz();
            if (myChar == null || myChar.meDead || myChar.cHP <= 0)
                return;

            Skill desired = PickMobController.GetSkillAttack();
            if (desired == null && GameScr.keySkill != null && GameScr.keySkill.Length > 0)
            {
                desired = GameScr.keySkill[0];
            }
            if (desired != null && myChar.myskill != desired)
            {
                Skill chosen = desired;
                MainThreadDispatcher.dispatcher(() => GameScr.gI().doSelectSkill(chosen, true));
            }

            MyVector vMob = new MyVector();
            MyVector vChar = new MyVector();
            if (myChar.mobFocus != null)
            {
                vMob.addElement(myChar.mobFocus);
            }
            else if (myChar.charFocus != null)
            {
                vChar.addElement(myChar.charFocus);
            }

            if (vMob.size() > 0 || vChar.size() > 0)
            {
                Skill myskill = myChar.myskill;
                if (myskill == null) return;
                long now = mSystem.currentTimeMillis();
                if (now - myskill.lastTimeUseThisSkill > myskill.coolDown)
                {
                    Service.gI().sendPlayerAttack(vMob, vChar, -1);
                    myskill.lastTimeUseThisSkill = now;
                }
            }
        }

        public static void toggleAutoAttack()
        {
            if (GameEvents.Profile.FastAttackNoAnim)
            {
                stopAutoAttack();
                return;
            }
            if (!ThreadAction<Attack>.gI.IsActing)
            {
                ThreadAction<Attack>.gI.toggle(true);
            }
        }

        public static void stopAutoAttack()
        {
            if (ThreadAction<Attack>.gI.IsActing)
            {
                ThreadAction<Attack>.gI.toggle(false);
            }
        }
    }
}
