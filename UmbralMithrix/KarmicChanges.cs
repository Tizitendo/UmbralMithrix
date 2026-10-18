using System;
using EntityStates.BrotherMonster;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using R2API.Utils;
using RoR2;

namespace UmbralMithrix;

public class KarmicChanges
{
    public KarmicChanges()
    {
        IL.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
        IL.EntityStates.BrotherMonster.BaseSlideState.OnEnter += BaseSlideState_OnEnter;
        IL.EntityStates.BrotherMonster.BaseSlideState.AttemptFireHomingBombAuthority += BaseSlideState_AttemptFireHomingBombAuthority;
        IL.EntityStates.BrotherMonster.BaseSlideState.AttemptFireCrystalsAuthority += BaseSlideState_AttemptFireCrystalsAuthority;
        IL.RoR2.BrotherSuperArmorController.OnDamageDealtServer += BrotherSuperArmorController_OnDamageDealtServer;
    }

    private void BrotherSuperArmorController_OnDamageDealtServer(ILContext il)
    {
        ILCursor c = new(il);

        c.EmitDelegate<Func<bool>>(applySuperArmor);
        c.Emit(OpCodes.Ret);
        ILLabel dontSkip = c.MarkLabel();
        c.Index--;
        c.Emit(OpCodes.Brtrue, dontSkip);

        static bool applySuperArmor()
        {
            return ModConfig.Karma1.Value;
        }
    }

    private void BaseSlideState_AttemptFireCrystalsAuthority(ILContext il)
    {
        ILCursor c = new(il);
        ILLabel l = null;
        
        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<BaseSlideState, bool>>(applyCrystals);
        c.Emit(OpCodes.Ret);
        l = c.MarkLabel();
        c.Index--;
        c.Emit(OpCodes.Brtrue, l);

        static bool applyCrystals(BaseSlideState self)
        {
            if (!self.characterBody)
                return false;
            if (ModConfig.Karma2.Value && BrotherKarmaUtils.IsBrotherStrict(self.characterBody))
            {
                return true;
            }
            return false;
        }
    }

    private void BaseSlideState_AttemptFireHomingBombAuthority(ILContext il)
    {
        ILCursor c = new(il);

        ILLabel l = null;

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<BaseSlideState, bool>>(applyHomingBombs);
        c.Emit(OpCodes.Ret);
        l = c.MarkLabel();
        c.Index--;
        c.Emit(OpCodes.Brtrue, l);

        static bool applyHomingBombs(BaseSlideState self)
        {
            if (!self.characterBody)
                return false;
            if (BrotherKarmaUtils.IsBrotherStrict(self.characterBody))
            {
                return true;
            }
            return false;
        }
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
            if (!self.characterBody)
                return false;
            if (BrotherKarmaUtils.IsBrotherStrict(self.characterBody))
            {
                return true;
            }
            return false;
        }
    }

    private void RecalculateStats(ILContext il)
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
