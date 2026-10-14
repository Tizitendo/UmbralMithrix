using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;

namespace UmbralMithrix;

public class KarmicChanges
{
    public KarmicChanges()
    {
        IL.RoR2.CharacterBody.RecalculateStats += RecalculateStats;
    }

    private void RecalculateStats(ILContext il)
    {
        ILCursor c = new(il);
        ILLabel label = null;

        if (c.TryGotoNext(MoveType.After,
            x => x.MatchLdsfld(typeof(KarmaManager), nameof(KarmaManager.maxKarma)),
            x => x.MatchBlt(out label)
        ))
        {
            c.EmitDelegate<Func<bool>>(KarmaHealthBoost);
            c.Emit(OpCodes.Brfalse, label);
        } else
        {
            Log.Error(il.Method.Name + " Failed to find patch location");
        }

        static bool KarmaHealthBoost()
        {
            return ModConfig.Karma8.Value && PhaseCounter.instance.phase != 4;
        }
    }
}
