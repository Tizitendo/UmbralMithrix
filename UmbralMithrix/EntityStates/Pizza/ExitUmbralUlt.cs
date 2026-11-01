using RoR2;
using UnityEngine;
using EntityStates;
using UnityEngine.Networking;
using EntityStates.BrotherMonster;

namespace UmbralMithrix.EntityStates;

public class ExitUmbralUlt : BaseState
{
    public static float lendInterval = 0f;
    public static float duration = 2f;
    public static string soundString = "";
    public static GameObject channelFinishEffectPrefab = UmbralMithrix.umbralUltMuzzleFlash;
    private static int ExitUmbralUltHash = Animator.StringToHash("UltExit");
    private static int UltParamHash = Animator.StringToHash("Ult.playbackRate");
    private float spireTimer;
	private int spiresSpawned;
	private Vector3 spireForward;
    private int spiresToSpawn;

    public override void OnEnter()
    {
        base.OnEnter();

        spireForward = Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, Vector3.up).normalized;
        spiresToSpawn = 0;
		if (characterBody.master)
		{
			spiresToSpawn = characterBody.master.GetDeployableSameSlotLimit(DeployableSlot.BrotherKarmaSpire);
		}

        this.PlayAnimation("Body", ExitUmbralUlt.ExitUmbralUltHash, ExitUmbralUlt.UltParamHash, ExitUmbralUlt.duration);
        Util.PlaySound(ExitUmbralUlt.soundString, this.gameObject);
        if (!(bool)ExitUmbralUlt.channelFinishEffectPrefab)
            return;
        EffectManager.SimpleMuzzleFlash(ExitUmbralUlt.channelFinishEffectPrefab, this.gameObject, "MuzzleUlt", false);
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (NetworkServer.active && KarmaManager.instance.GetEffectiveKarma() >= UltExitState.requiredKarmaForSpires && spiresSpawned < spiresToSpawn)
		{
			spireTimer += Time.deltaTime;
			if (spireTimer >= UltExitState.spireSpawnDuration / (float)spiresToSpawn)
			{
				AttemptSpawnSpireServer();
				spireTimer = 0f;
			}
		}

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

    public void AttemptSpawnSpireServer()
	{
		if (!NetworkServer.active || spiresToSpawn <= 0 || !ModConfig.Karma5.Value)
		{
			return;
		}
		float num = 360f / spiresToSpawn;
		CharacterMaster master = base.characterBody.master;
		if (!master || !master.IsDeployableSlotAvailable(DeployableSlot.BrotherKarmaSpire))
		{
			return;
		}
		Vector3 vector = Quaternion.AngleAxis(num * spiresSpawned, Vector3.up) * spireForward;
		DirectorPlacementRule placementRule = new DirectorPlacementRule
		{
			placementMode = DirectorPlacementRule.PlacementMode.Direct,
			maxDistance = UltExitState.maxSpawnOffset,
			position = UmbralMithrix.arenaCenter + vector * UltExitState.spireSpawnRadius
		};
		DirectorSpawnRequest directorSpawnRequest = new DirectorSpawnRequest(UltExitState.spireSpawnCard, placementRule, Run.instance.spawnRng);
		directorSpawnRequest.summonerBodyObject = base.characterBody.gameObject;
		directorSpawnRequest.teamIndexOverride = base.teamComponent.teamIndex;
		directorSpawnRequest.ignoreTeamMemberLimit = true;
		directorSpawnRequest.onSpawnedServer = result =>
        {
            GameObject spawnedInstance = result.spawnedInstance;
            if ((bool)spawnedInstance)
            {
                if (spawnedInstance.TryGetComponent<Deployable>(out var component))
                {
                    master.AddDeployable(component, DeployableSlot.BrotherKarmaSpire);
                }
                if (spawnedInstance.TryGetComponent<MinionOwnership>(out var component2))
                {
                    component2.SetOwner(master);
                }
            }
        };
		if ((bool)DirectorCore.instance.TrySpawnObject(directorSpawnRequest))
		{
			spiresSpawned++;
		}
	}
}
