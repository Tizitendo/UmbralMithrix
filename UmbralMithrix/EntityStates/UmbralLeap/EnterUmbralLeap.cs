using RoR2;
using UnityEngine;
using EntityStates;
using EntityStates.BrotherMonster;
using RoR2.Projectile;
using HG;

namespace UmbralMithrix.EntityStates;

public class EnterUmbralLeap : BaseState
{
    public static float baseDuration = 0.25f;
    public static string soundString = "Play_voidRaid_snipe_shoot_final";
    private float duration;
    private static int EnterUmbralLeapStateHash = Animator.StringToHash(nameof(EnterSkyLeap));
    private static int BufferEmptyStateHash = Animator.StringToHash("BufferEmpty");
    private static int SkyLeapParamHash = Animator.StringToHash("SkyLeap.playbackRate");

    public override void OnEnter()
    {
        base.OnEnter();
        this.duration = EnterUmbralLeap.baseDuration / this.attackSpeedStat;
        Util.PlaySound(EnterUmbralLeap.soundString, this.gameObject);
        this.PlayAnimation("Body", EnterUmbralLeap.EnterUmbralLeapStateHash, EnterUmbralLeap.SkyLeapParamHash, this.duration);
        this.PlayAnimation("FullBody Override", EnterUmbralLeap.BufferEmptyStateHash);
        this.characterDirection.moveVector = this.characterDirection.forward;
        this.characterBody.AddTimedBuff(RoR2Content.Buffs.ArmorBoost, EnterUmbralLeap.baseDuration);
        if (base.isAuthority)
		{
			AttemptFireSkyLeapBombsAuthority();
		}
        AimAnimator aimAnimator = this.GetAimAnimator();
        if (!(bool)aimAnimator)
            return;
        aimAnimator.enabled = true;
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!this.isAuthority || (double)this.fixedAge <= this.duration)
            return;
        this.outer.SetNextState(new HoldUmbralLeap());
    }

    private void AttemptFireSkyLeapBombsAuthority()
	{
		if (KarmaManager.instance.GetEffectiveKarma() < EnterSkyLeap.requiredKarmaForSkyLeapBombs || !ModConfig.Karma2.Value)
		{
			return;
		}
		FireProjectileInfo fireProjectileInfo = new FireProjectileInfo
		{
			procChainMask = default(ProcChainMask),
			projectilePrefab = EnterSkyLeap.skyLeapBombPrefab,
			damage = base.characterBody.damage,
			damageTypeOverride = new DamageTypeCombo
			{
				damageType = DamageType.CrippleOnHit
			},
			crit = base.characterBody.RollCrit(),
			owner = base.characterBody.gameObject,
			rotation = Quaternion.identity,
			useFuseOverride = true
		};
		for (int i = 0; i < 4; i++)
		{
			FireProjectileInfo fireProjectileInfo2 = fireProjectileInfo;
            fireProjectileInfo2.position = UmbralMithrix.p23PizzaPoints[i] + EnterSkyLeap.skyLeapBombHeightOffset * Vector3.up;
			fireProjectileInfo2.fuseOverride = duration + HoldSkyLeap.duration + EnterSkyLeap.extraFusePerSkyLeapBomb * (float)(i + 1);
			ProjectileManager.instance.FireProjectile(fireProjectileInfo2);
		}
	}

	
}
