using RoR2;
using UnityEngine;
using EntityStates;

namespace UmbralMithrix.EntityStates;

public class ExitUmbralUlt : BaseState
{
    public static float lendInterval = 0f;
    public static float duration = 2f;
    public static string soundString = "";
    public static GameObject channelFinishEffectPrefab = UmbralMithrix.umbralUltMuzzleFlash;
    private static int ExitUmbralUltHash = Animator.StringToHash("UltExit");
    private static int UltParamHash = Animator.StringToHash("Ult.playbackRate");

    public override void OnEnter()
    {
        base.OnEnter();
        this.PlayAnimation("Body", ExitUmbralUlt.ExitUmbralUltHash, ExitUmbralUlt.UltParamHash, ExitUmbralUlt.duration);
        Util.PlaySound(ExitUmbralUlt.soundString, this.gameObject);
        if (!(bool)ExitUmbralUlt.channelFinishEffectPrefab)
            return;
        EffectManager.SimpleMuzzleFlash(ExitUmbralUlt.channelFinishEffectPrefab, this.gameObject, "MuzzleUlt", false);
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if ((double)this.fixedAge <= ExitUmbralUlt.duration)
            return;
        this.outer.SetNextStateToMain();
    }

    public override void OnExit()
    {
        GenericSkill special = (bool)this.skillLocator ? this.skillLocator.special : null;
        if ((bool)special)
            special.UnsetSkillOverride(this.outer, ChannelUmbralUlt.replacementSkillDef, GenericSkill.SkillOverridePriority.Contextual);
        base.OnExit();
    }
}
