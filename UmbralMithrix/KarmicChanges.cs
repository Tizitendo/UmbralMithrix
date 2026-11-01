using System;
using EntityStates;
using EntityStates.BrotherMonster;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API.Utils;
using RoR2;
using RoR2.ContentManagement;
using RoR2BepInExPack.GameAssetPathsBetter;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace UmbralMithrix;

public class KarmicChanges
{

    [SystemInitializer]
    public static void Init()
    {
        AssetAsyncReferenceManager<GameObject>.LoadAsset(new AssetReferenceT<GameObject>(RoR2_Base_Brother.BrotherGravityWell_prefab)).Completed += (x) => 
        {
            if (x.Result.TryGetComponent(out CharacterBodyDisplacementProvider characterBodyDisplacementProvider))
            {
                GameObject.Destroy(characterBodyDisplacementProvider);
            }
            GameObject PullController = UmbralMithrix.Bundle.LoadAsset<GameObject>("Assets/Assets/Objects/PullController.prefab");
            PullController.transform.parent = x.Result.transform;
        };

        IL.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
        IL.EntityStates.BrotherMonster.BaseSlideState.OnEnter += BaseSlideState_OnEnter;
        IL.EntityStates.BrotherMonster.BaseSlideState.AttemptFireHomingBombAuthority += BaseSlideState_AttemptFireHomingBombAuthority;
        IL.EntityStates.BrotherMonster.BaseSlideState.AttemptFireCrystalsAuthority += BaseSlideState_AttemptFireCrystalsAuthority;
        IL.EntityStates.BrotherMonster.FistSlam.FireCrystalsAuthority += FistSlam_FireCrystalsAuthority;
        IL.RoR2.BrotherSuperArmorController.OnDamageDealtServer += BrotherSuperArmorController_OnDamageDealtServer;
        On.EntityStates.BrotherMonster.ThroneSpawnState.OnEnter += ThroneSpawnState_OnEnter;
        IL.EntityStates.BrotherMonster.SkySpawnState.AttemptSpawnSpireServer += SkySpawnState_AttemptSpawnSpireServer;
    }

    private static void SkySpawnState_AttemptSpawnSpireServer(ILContext il)
    {
        SkipStateFuncContent(il, (self) =>
        {
            return ModConfig.Karma5.Value;
        });
    }

    private static void ThroneSpawnState_OnEnter(On.EntityStates.BrotherMonster.ThroneSpawnState.orig_OnEnter orig, ThroneSpawnState self)
    {
        orig(self);

        if (!ModConfig.Karma5.Value)
            return;

        CharacterMaster master = self.characterBody.master;
        if (!master)
            return;
        if (!NetworkServer.active || KarmaManager.instance.GetEffectiveKarma() < SkySpawnState.requiredKarmaForSpires)
            return;

        Vector3 spireForward = Vector3.ProjectOnPlane(UnityEngine.Random.onUnitSphere, Vector3.up).normalized;
        DirectorSpawnRequest directorSpawnRequest = new DirectorSpawnRequest(SkySpawnState.spireSpawnCard, null, Run.instance.spawnRng)
        {
            summonerBodyObject = self.characterBody.gameObject,
            teamIndexOverride = self.teamComponent.teamIndex,
            ignoreTeamMemberLimit = true
        };
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

        for(int spiresSpawned = master.GetDeployableCount(DeployableSlot.BrotherKarmaSpire); spiresSpawned < 6; spiresSpawned++)
        {
            Vector3 vector = Quaternion.AngleAxis(360/6 * (float)spiresSpawned, Vector3.up) * spireForward;
            
            directorSpawnRequest.placementRule = new()
            {
                placementMode = DirectorPlacementRule.PlacementMode.Direct,
                maxDistance = SkySpawnState.maxSpawnOffset,
                position = self.characterBody.footPosition + vector * SkySpawnState.spireSpawnRadius
            };;
            
            DirectorCore.instance.TrySpawnObject(directorSpawnRequest);
        }
    }

    private static void SkipStateFuncContent(ILContext il, Func<EntityState, bool> skipReq)
    {
        ILCursor c = new(il);

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<EntityState, bool>>(skipReq);
        c.Emit(OpCodes.Ret);
        ILLabel dontSkip = c.MarkLabel();
        c.Index--;
        c.Emit(OpCodes.Brtrue, dontSkip);
    }

    private static void FistSlam_FireCrystalsAuthority(ILContext il)
    {
        SkipStateFuncContent(il, (self) =>
        {
            return ModConfig.Karma4.Value;
        });
    }

    private static void BrotherSuperArmorController_OnDamageDealtServer(ILContext il)
    {
        SkipStateFuncContent(il, (self) =>
        {
            return ModConfig.Karma1.Value;
        });
    }

    private static void BaseSlideState_AttemptFireCrystalsAuthority(ILContext il)
    {
        SkipStateFuncContent(il, (self) =>
        {
            if (!self.characterBody)
                return false;
            if (ModConfig.Karma2.Value && BrotherKarmaUtils.IsBrotherStrict(self.characterBody))
            {
                return true;
            }
            return false;
        });
    }

    private static void BaseSlideState_AttemptFireHomingBombAuthority(ILContext il)
    {
        SkipStateFuncContent(il, (self) =>
        {
            if (!self.characterBody)
                return false;
            if (BrotherKarmaUtils.IsBrotherStrict(self.characterBody))
            {
                return true;
            }
            return false;
        });
    }

    private static void BaseSlideState_OnEnter(ILContext il)
    {
        ILCursor c = new(il);
        ILLabel skipForceImmunityPos = null;

        if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdsfld(typeof(BaseSlideState), nameof(BaseSlideState.requiredKarmaForPhysForceImmunity)),
            x => x.MatchBlt(out skipForceImmunityPos)
        ))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<BaseSlideState, bool>>(applyForceImmunity);
            c.Emit(OpCodes.Brfalse, skipForceImmunityPos);
        } else
        {
           Log.Error("Failed to find patch location");
        }

        static bool applyForceImmunity(BaseSlideState self)
        {
            return false;
        }
    }

    private static void RecalculateStats(ILContext il)
    {
        int karmaLevelLoc = GetKarmaLevelLoc(il);

        CooldownReductionToggle(il, karmaLevelLoc);
        DamageBoostToggle(il, karmaLevelLoc);
        SpeedBoostToggle(il, karmaLevelLoc);
        HealthBoostToggle(il);
    }

    private static int GetKarmaLevelLoc(ILContext il)
    {
        ILCursor c = new(il);
        int karmaLevelLoc = 0;

        if (c.TryGotoNext(
        x => x.MatchCallOrCallvirt(typeof(KarmaManager), nameof(KarmaManager.GetEffectiveKarma))
        ))
        {
            c.TryGotoNext(x => x.MatchStloc(out karmaLevelLoc));
        }

        if (karmaLevelLoc == 0)
            Log.Error("Failed to find patch location");

        return karmaLevelLoc;
    }

    private static void CooldownReductionToggle(ILContext il, int karmaLevelLoc)
    {
        ILCursor c = new(il);
        ILLabel skipReductionPos = null;

        if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdloc(karmaLevelLoc),
            x => x.MatchLdcI4(6),
            x => x.MatchBlt(out skipReductionPos)
        ))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, bool>>(applyCooldownReduction);
            c.Emit(OpCodes.Brfalse, skipReductionPos);
        } else
        {
            Log.Error("Failed to find patch location");
        }

        static bool applyCooldownReduction(CharacterBody self)
        {
            if (ModConfig.KarmaStatChanges.Value && BrotherKarmaUtils.IsBrotherStrict(self))
            {
                return true;
            }
            return false;
        }
    }

    private static void DamageBoostToggle(ILContext il, int karmaLevelLoc)
    {
        ILCursor c = new(il);

        if (!c.TryGotoNext(
            x => x.MatchLdcR8(0.01),
            x => x.MatchLdloc(karmaLevelLoc)
        ) || !c.TryGotoNext(MoveType.After,
            x => x.MatchCallOrCallvirt(typeof(CharacterBody).GetPropertySetter(nameof(CharacterBody.damage)))
        )
        )
        {
            Log.Error("Failed to find patch location");
            return;
        }

        ILLabel afterDamageBoost = c.MarkLabel();

        if (c.TryGotoPrev(MoveType.After,
            x => x.MatchBrfalse(out _)
        ))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, bool>>(applyDamageBoost);
            c.Emit(OpCodes.Brfalse, afterDamageBoost);
        } else
        {
            Log.Error("Failed to find patch location");
        }

        static bool applyDamageBoost(CharacterBody self)
        {
            if (ModConfig.KarmaStatChanges.Value && BrotherKarmaUtils.IsBrotherStrict(self))
            {
                return true;
            }
            return false;
        }
    }

    private static void SpeedBoostToggle(ILContext il, int karmaLevelLoc)
    {
        ILCursor c = new(il);
        ILLabel skipBoostPos = null;

        if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdloc(karmaLevelLoc),
            x => x.MatchLdcI4(2),
            x => x.MatchBlt(out skipBoostPos)
        ))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, bool>>(applySpeedBoost);
            c.Emit(OpCodes.Brfalse, skipBoostPos);
        } else
        {
            Log.Error("Failed to find patch location");
        }

        static bool applySpeedBoost(CharacterBody self)
        {
            if (ModConfig.KarmaStatChanges.Value && BrotherKarmaUtils.IsBrotherStrict(self))
            {
                return true;
            }
            return false;
        }
    }

    private static void HealthBoostToggle(ILContext il)
    {
        ILCursor c = new(il);
        ILLabel label = null;

        if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdsfld(typeof(KarmaManager), nameof(KarmaManager.maxKarma)),
            x => x.MatchBlt(out label)
        ))
        {
            c.Emit(OpCodes.Ldarg_0);
            c.EmitDelegate<Func<CharacterBody, bool>>(applyKarmaHealthBoost);
            c.Emit(OpCodes.Brfalse, label);
        } else
        {
            Log.Error("Failed to find patch location");
        }

        static bool applyKarmaHealthBoost(CharacterBody self)
        {
            if (ModConfig.Karma8.Value && PhaseCounter.instance.phase != 4 && BrotherKarmaUtils.IsBrotherStrict(self))
            {
                return true;
            }
            return false;
        }
    }
}
