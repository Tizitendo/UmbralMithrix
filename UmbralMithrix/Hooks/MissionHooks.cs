using EntityStates;
using EntityStates.BrotherMonster;
using EntityStates.Missions.BrotherEncounter;
using MiscFixes.Modules;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace UmbralMithrix
{
    public class MissionHooks
    {
        public MissionHooks()
        {
            On.EntityStates.Missions.BrotherEncounter.BrotherEncounterPhaseBaseState.OnEnter += BrotherEncounterPhaseBaseStateOnEnter;
            On.EntityStates.Missions.BrotherEncounter.Phase1.OnEnter += Phase1OnEnter;
            On.EntityStates.Missions.BrotherEncounter.Phase2.OnEnter += Phase2OnEnter;
            On.EntityStates.Missions.BrotherEncounter.Phase3.OnEnter += Phase3OnEnter;
            On.EntityStates.Missions.BrotherEncounter.Phase4.OnEnter += Phase4OnEnter;
            On.EntityStates.Missions.BrotherEncounter.BossDeath.OnEnter += BossDeathOnEnter;

            //skybox child is gone, so nullchecking it
            IL.EntityStates.Missions.BrotherEncounter.BrotherEncounterPhaseBaseState.UpdateSkyboxColor += NullcheckSkybox;
            IL.EntityStates.Missions.BrotherEncounter.BossDeath.OnEnter += SkipSkyBoxCheck;
        }

        private static void NullcheckSkybox(ILContext il)
        {
            ILCursor c = new(il);
            ILLabel afterIfBlock = null;
            int nullcheckChildLoc = 0;

            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("SkyboxReplacement"),
                x => x.MatchCallOrCallvirt(typeof(ChildLocator), nameof(ChildLocator.FindChild)),
                x => x.MatchStloc(out nullcheckChildLoc)
            ))
            {
                Log.Error(il.Method.Name + " Failed to find patch location");
            }

            ILLabel nullcheckPos = c.MarkLabel();

            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchBrfalse(out afterIfBlock)
            ))
            {
                Log.Error(il.Method.Name + " Failed to find patch location");
            }

            c.GotoLabel(nullcheckPos);
            c.Emit(OpCodes.Ldloc, nullcheckChildLoc);
            c.EmitOpImplicit();
            c.Emit(OpCodes.Brfalse, afterIfBlock);
        }

        private static void SkipSkyBoxCheck(ILContext il)
        {
            ILCursor c = new(il);

            if (!c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(out _),
                x => x.MatchLdfld(typeof(BrotherEncounterBaseState), nameof(BrotherEncounterBaseState.childLocator)),
                x => x.MatchLdstr("SkyboxReplacement")
            ))
            {
                Log.Error(il.Method.Name + " Failed to find patch location");
            }

            ILLabel skipStartPos = c.MarkLabel();

            if (!c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt(typeof(GameObject), nameof(GameObject.SetActive))
            ))
            {
                Log.Error(il.Method.Name + " Failed to find patch location");
            }

            ILLabel skipEndPos = c.MarkLabel();
            c.GotoLabel(skipStartPos);

            c.Emit(OpCodes.Br, skipEndPos);
        }

        private void Phase1OnEnter(On.EntityStates.Missions.BrotherEncounter.Phase1.orig_OnEnter orig, Phase1 self)
        {
            UmbralMithrix.mithrix.GetComponent<EntityStateMachine>().initialStateType = new SerializableEntityStateType(typeof(ThroneSpawnState));
            orig(self);
        }

        private void Phase2OnEnter(On.EntityStates.Missions.BrotherEncounter.Phase2.orig_OnEnter orig, Phase2 self)
        {
            UmbralMithrix.mithrix.GetComponent<EntityStateMachine>().initialStateType = new SerializableEntityStateType(typeof(SkySpawnState));
            self.KillAllMonsters();
            orig(self);
        }

        private void Phase3OnEnter(On.EntityStates.Missions.BrotherEncounter.Phase3.orig_OnEnter orig, Phase3 self)
        {
            self.KillAllMonsters();
            orig(self);
        }

        private void Phase4OnEnter(On.EntityStates.Missions.BrotherEncounter.Phase4.orig_OnEnter orig, Phase4 self)
        {
            GameObject escapeSequenceController = GameObject.Find("EscapeSequenceController");
            if (escapeSequenceController)
            {
                Transform megaGlowsParticles = escapeSequenceController.transform.Find("EscapeSequenceObjects/Mega Glows");
                if (megaGlowsParticles)
                {
                    GameObject arenaMegaGlowsParticles = GameObject.Instantiate<GameObject>(megaGlowsParticles.gameObject, new Vector3(-88.5f, 491.5f, -0.3f), Quaternion.Euler(270f, 0f, 0f));
                    arenaMegaGlowsParticles.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                }
            }
            GameObject gameObject = GameObject.Instantiate(UmbralMithrix.voidling, new Vector3(-88.5f, 520f, -0.3f), Quaternion.identity);
            gameObject.AddComponent<DeathZoneController>();
            gameObject.GetComponent<TeamComponent>().teamIndex = TeamIndex.Monster;
            SkillLocator skillLocator = gameObject.GetComponent<CharacterBody>().skillLocator;
            skillLocator.primary = new GenericSkill();
            skillLocator.secondary = new GenericSkill();
            skillLocator.utility = new GenericSkill();
            skillLocator.special = new GenericSkill();
            NetworkServer.Spawn(gameObject);
            orig(self);
        }

        private void BossDeathOnEnter(On.EntityStates.Missions.BrotherEncounter.BossDeath.orig_OnEnter orig, BossDeath self)
        {
            orig(self);
            GameObject.Find("InactiveVoidling(Clone)").GetComponent<HealthComponent>().Suicide();
        }

        private void BrotherEncounterPhaseBaseStateOnEnter(On.EntityStates.Missions.BrotherEncounter.BrotherEncounterPhaseBaseState.orig_OnEnter orig, BrotherEncounterPhaseBaseState self)
        {
            orig(self);

            if (!NetworkServer.active || self.phaseScriptedCombatEncounter == null || !PhaseCounter.instance)
                return;

            self.childLocator = self.GetComponent<ChildLocator>();

            Transform child1 = self.childLocator.FindChild("ArenaWalls");

            Material material = UmbralMithrix.doppelMat;

            if (child1)
            {
                child1.gameObject.SetActive(self.shouldEnableArenaWalls);
                if (ModConfig.purpleArena.Value)
                {
                    foreach (Renderer componentsInChild in child1.GetComponentsInChildren<Renderer>())
                    {
                        componentsInChild.material = material;
                    }
                }
            }

            ScriptedCombatEncounter.SpawnInfo spawnInfo1;
            if (PhaseCounter.instance.phase == 2)
            {
                Transform transform = new GameObject().transform;
                transform.position = new Vector3(-88.5f, 491.5f, -0.3f);

                spawnInfo1 = new ScriptedCombatEncounter.SpawnInfo();
                spawnInfo1.explicitSpawnPosition = transform;
                spawnInfo1.spawnCard = UmbralMithrix.mithrixCard;
                ScriptedCombatEncounter.SpawnInfo spawnInfo2 = spawnInfo1;
                self.phaseScriptedCombatEncounter.spawns = [spawnInfo2];
            }

            if (PhaseCounter.instance.phase == 3)
            {
                Transform transform = new GameObject().transform;
                transform.position = new Vector3(-88.5f, 491.5f, -0.3f);

                spawnInfo1 = new ScriptedCombatEncounter.SpawnInfo();
                spawnInfo1.explicitSpawnPosition = transform;
                spawnInfo1.spawnCard = UmbralMithrix.mithrixHurtP3Card;
                ScriptedCombatEncounter.SpawnInfo spawnInfo3 = spawnInfo1;
                self.phaseScriptedCombatEncounter.spawns = [spawnInfo3];
            }

            if (PhaseCounter.instance.phase == 4)
            {
                Transform transform = new GameObject().transform;
                transform.position = new Vector3(-88.5f, 491.5f, -0.3f);

                spawnInfo1 = new ScriptedCombatEncounter.SpawnInfo();
                spawnInfo1.explicitSpawnPosition = transform;
                spawnInfo1.spawnCard = UmbralMithrix.mithrixHurtCard;
                ScriptedCombatEncounter.SpawnInfo spawnInfo3 = spawnInfo1;
                self.phaseScriptedCombatEncounter.spawns = [spawnInfo3];
            }
        }
    }
}
