using EntityStates;
using HG;
using R2API;
using RoR2;
using RoR2.CharacterAI;
using RoR2.ContentManagement;
using RoR2.Skills;
using System;
using System.Collections.Generic;
using System.Linq;
using UmbralMithrix.Components;
using UmbralMithrix.EntityStates;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace UmbralMithrix
{
    public class CloneController : MonoBehaviour
    {
        private static SpawnCard _cloneCard;
        private static GameObject _phantomBody;
        // private static Material matNewPhantom;
        
        private List<CharacterBody> playerBodies = new();
        private float stopwatch = 0f;
        private float interval = 8f;

        public static void Init()
        {
            AssetReferenceT<GameObject> glassBodyRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Brother.BrotherPhantomBody_prefab);
            _phantomBody = AssetAsyncReferenceManager<GameObject>.LoadAsset(glassBodyRef).WaitForCompletion();
            AssetReferenceT<SkinDef> originalSkinRef = new AssetReferenceT<SkinDef>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Brother.skinBrotherBodyDefault_asset);
            SkinDef originalSkin = AssetAsyncReferenceManager<SkinDef>.LoadAsset(originalSkinRef).WaitForCompletion();

            // matNewPhantom = UmbralMithrix.Bundle.LoadAsset<Material>("Assets/Assets/Materials/matNewPhantom.mat");

            AssetReferenceT<SpawnCard> phantomCardRef = new AssetReferenceT<SpawnCard>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Junk_BrotherGlass.cscBrotherGlass_asset);
            AssetAsyncReferenceManager<SpawnCard>.LoadAsset(phantomCardRef).Completed += (x) => 
            {
                _cloneCard = x.Result;
            };

            AssetReferenceT<GameObject> glassMasterRef = new AssetReferenceT<GameObject>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Junk_BrotherGlass.BrotherGlassMaster_prefab);
            AssetAsyncReferenceManager<GameObject>.LoadAsset(glassMasterRef).Completed += (x) =>
            {
                GameObject prefab = x.Result;
                List<AISkillDriver> drivers = prefab.GetComponents<AISkillDriver>().ToList();

                foreach (AISkillDriver driver in drivers)
                {
                    if (driver.customName == "CastUlt")
                        driver.requiredSkill = null;
                }
                prefab.GetComponent<CharacterMaster>().bodyPrefab = _phantomBody;
            };

            {
                _phantomBody.GetComponent<EntityStateMachine>().initialStateType = new SerializableEntityStateType(typeof(EntityStates.GlassSpawnState));

                CharacterBody characterBody = _phantomBody.GetComponent<CharacterBody>();
                CharacterDirection characterDirection = _phantomBody.GetComponent<CharacterDirection>();
                CharacterMotor characterMotor = _phantomBody.GetComponent<CharacterMotor>();

                characterBody.baseMaxHealth = ModConfig.basehealth.Value;
                characterBody.levelMaxHealth = ModConfig.levelhealth.Value;

                characterBody.baseDamage = ModConfig.basedamage.Value / 2f;
                characterBody.levelDamage = ModConfig.leveldamage.Value / 2f;
                characterMotor.airControl = ModConfig.aircontrol.Value;
                characterDirection.turnSpeed = ModConfig.turningspeed.Value;

                SkillDef skillDef = ScriptableObject.CreateInstance<SkillDef>();
                skillDef.skillName = "UmbralOrb";
                (skillDef as ScriptableObject).name = "UmbralOrb";

                skillDef.activationState = new SerializableEntityStateType(typeof(GlassOrbAttack));
                skillDef.activationStateMachineName = "Body";
                skillDef.interruptPriority = InterruptPriority.PrioritySkill;

                skillDef.baseMaxStock = 1;
                skillDef.baseRechargeInterval = 6f;

                skillDef.rechargeStock = 1;
                skillDef.requiredStock = 1;
                skillDef.stockToConsume = 1;

                skillDef.dontAllowPastMaxStocks = true;
                skillDef.beginSkillCooldownOnSkillEnd = false;
                skillDef.canceledFromSprinting = true;
                skillDef.forceSprintDuringState = false;
                skillDef.fullRestockOnAssign = true;
                skillDef.resetCooldownTimerOnUse = true;
                skillDef.isCombatSkill = true;
                skillDef.mustKeyPress = false;
                skillDef.cancelSprintingOnActivation = true;

                SkillFamily newFamily = ScriptableObject.CreateInstance<SkillFamily>();
                (newFamily as ScriptableObject).name = "UmbralPhantomPrimaryFamily";
                newFamily.variants = new SkillFamily.Variant[1] { new SkillFamily.Variant { skillDef = skillDef } };

                if (_phantomBody.TryGetComponent(out SkillLocator skillLocator))
                {
                    GenericSkill skill = _phantomBody.AddComponent<GenericSkill>();
                    skill._skillFamily = newFamily;
                    skillLocator.special = skill;
                    skillLocator.primary = null;
                }

                // Hack to get around starting a new async operation in the Complete callback not being allowed
                RoR2Application.onNextUpdate += () =>
                {
                    Transform modelTransform = null;
                    if (_phantomBody && _phantomBody.TryGetComponent(out ModelLocator modelLocator))
                    {
                        modelTransform = modelLocator.modelTransform;
                    }

                    if (!modelTransform)
                        return;

                    ModelSkinController modelSkinController = modelTransform.gameObject.EnsureComponent<ModelSkinController>();
                    modelSkinController.skins ??= Array.Empty<SkinDef>();
                    int replacementSkinIndex = Array.IndexOf(modelSkinController.skins, originalSkin);

                    SkinDef skinDef = Instantiate(originalSkin);
                    skinDef.name = "skinBrotherGlassBodyDefault";
                    Transform originalSkinRoot = skinDef.rootObject.transform;
                    skinDef.rootObject = modelTransform.gameObject;

                    (AssetReferenceT<SkinDefParams> paramsAddress, SkinDefParams paramsDirect) = skinDef.GetSkinParams();
                    AssetOrDirectReference<SkinDefParams> skinDefParamsReference = new AssetOrDirectReference<SkinDefParams>
                    {
                        address = paramsAddress,
                        directRef = paramsDirect
                    };

                    SkinDefParams skinDefParams = Instantiate(skinDefParamsReference.WaitForCompletion());
                    skinDefParams.name = $"{skinDef.name}_params";
                    skinDef.skinDefParams = skinDefParams;
                    skinDef.skinDefParamsAddress = new AssetReferenceT<SkinDefParams>(string.Empty);
                    skinDef.optimizedSkinDefParams = skinDefParams;
                    skinDef.optimizedSkinDefParamsAddress = new AssetReferenceT<SkinDefParams>(string.Empty);

                    List<CharacterModel.RendererInfo> rendererInfos = [.. skinDefParams.rendererInfos];
                    List<SkinDefParams.GameObjectActivation> gameObjectActivations = [.. skinDefParams.gameObjectActivations];
                    List<SkinDefParams.MeshReplacement> meshReplacements = [.. skinDefParams.meshReplacements];
                    List<CharacterModel.LightInfo> lightReplacements = [.. skinDefParams.lightReplacements];

                    for (int i = rendererInfos.Count - 1; i >= 0; i--)
                    {
                        CharacterModel.RendererInfo rendererInfo = rendererInfos[i];

                        Renderer originalRenderer = rendererInfo.renderer;
                        rendererInfo.renderer = null;
                        if (originalRenderer && originalRenderer.transform.IsChildOf(originalSkinRoot))
                        {
                            string rendererPath = Util.BuildPrefabTransformPath(originalSkinRoot, originalRenderer.transform);

                            if (!string.IsNullOrEmpty(rendererPath))
                            {
                                Transform rendererTransform = modelTransform.Find(rendererPath);
                                if (rendererTransform)
                                {
                                    rendererInfo.renderer = rendererTransform.GetComponent(originalRenderer.GetType()) as Renderer;
                                }
                            }
                        }

                        if (rendererInfo.renderer)
                        {
                            switch (rendererInfo.renderer.name)
                            {
                                case "BrotherHammerConcrete":
                                case "BrotherBodyMesh":
                                    rendererInfo.defaultMaterial = null;
                                    rendererInfo.defaultMaterialAddress = new AssetReferenceT<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Brother.maBrotherGlassOverlay_mat);
                                    break;
                            }

                            rendererInfos[i] = rendererInfo;
                        }
                        else
                        {
                            rendererInfos.RemoveAt(i);
                        }
                    }

                    for (int i = gameObjectActivations.Count - 1; i >= 0; i--)
                    {
                        SkinDefParams.GameObjectActivation gameObjectActivation = gameObjectActivations[i];

                        GameObject originalObject = gameObjectActivation.gameObject;
                        gameObjectActivation.gameObject = null;

                        if (originalObject && originalObject.transform.IsChildOf(originalSkinRoot))
                        {
                            string objectPath = Util.BuildPrefabTransformPath(originalSkinRoot, originalObject.transform);

                            if (!string.IsNullOrEmpty(objectPath))
                            {
                                Transform objectTransform = modelTransform.Find(objectPath);
                                if (objectTransform)
                                {
                                    gameObjectActivation.gameObject = objectTransform.gameObject;
                                }
                            }
                        }

                        if (gameObjectActivation.gameObject)
                        {
                            gameObjectActivations[i] = gameObjectActivation;
                        }
                        else
                        {
                            gameObjectActivations.RemoveAt(i);
                        }
                    }

                    for (int i = meshReplacements.Count - 1; i >= 0; i--)
                    {
                        SkinDefParams.MeshReplacement meshReplacement = meshReplacements[i];

                        Renderer originalRenderer = meshReplacement.renderer;
                        meshReplacement.renderer = null;

                        if (originalRenderer && originalRenderer.transform.IsChildOf(originalSkinRoot))
                        {
                            string rendererPath = Util.BuildPrefabTransformPath(originalSkinRoot, originalRenderer.transform);

                            if (!string.IsNullOrEmpty(rendererPath))
                            {
                                Transform rendererTransform = modelTransform.Find(rendererPath);
                                if (rendererTransform)
                                {
                                    meshReplacement.renderer = rendererTransform.GetComponent(originalRenderer.GetType()) as Renderer;
                                }
                            }
                        }

                        if (meshReplacement.renderer)
                        {
                            meshReplacements[i] = meshReplacement;
                        }
                        else
                        {
                            meshReplacements.RemoveAt(i);
                        }
                    }

                    for (int i = lightReplacements.Count - 1; i >= 0; i--)
                    {
                        CharacterModel.LightInfo lightReplacement = lightReplacements[i];

                        Light originalLight = lightReplacement.light;
                        lightReplacement.light = null;

                        if (originalLight && originalLight.transform.IsChildOf(originalSkinRoot))
                        {
                            string lightPath = Util.BuildPrefabTransformPath(originalSkinRoot, originalLight.transform);
                            if (!string.IsNullOrEmpty(lightPath))
                            {
                                Transform lightTransform = modelTransform.Find(lightPath);
                                if (lightTransform)
                                {
                                    lightReplacement.light = lightTransform.GetComponent(originalLight.GetType()) as Light;
                                }
                            }
                        }

                        if (lightReplacement.light)
                        {
                            lightReplacements[i] = lightReplacement;
                        }
                        else
                        {
                            lightReplacements.RemoveAt(i);
                        }
                    }

                    skinDefParams.rendererInfos = [.. rendererInfos];
                    skinDefParams.gameObjectActivations = [.. gameObjectActivations];
                    skinDefParams.meshReplacements = [.. meshReplacements];
                    skinDefParams.lightReplacements = [.. lightReplacements];

                    if (ArrayUtils.IsInBounds(modelSkinController.skins, replacementSkinIndex))
                    {
                        modelSkinController.skins[replacementSkinIndex] = skinDef;
                    }
                    else
                    {
                        ArrayUtils.ArrayAppend(ref modelSkinController.skins, skinDef);
                    }

                    PersistentOverlayController overlayController = modelTransform.gameObject.EnsureComponent<PersistentOverlayController>();
                    overlayController.OverlayMaterialReference = new AssetReferenceT<Material>(RoR2BepInExPack.GameAssetPathsBetter.RoR2_Base_Brother.matBrotherGlassDistortion_mat);

                    skinDefParamsReference.Reset();
                    AssetAsyncReferenceManager<SkinDef>.UnloadAsset(originalSkinRef);

                    AssetAsyncReferenceManager<SkinDefParams>.LoadAsset(modelSkinController.skins[0].skinDefParamsAddress).Completed += (x) =>
                    {
                        for (int i = x.Result.rendererInfos.Count() - 1; i >= 0; i--)
                        {
                            CharacterModel.RendererInfo rendererInfo = x.Result.rendererInfos[i];

                            if (rendererInfo.renderer)
                            {
                                switch (rendererInfo.renderer.name)
                                {
                                    case "BrotherHammerConcrete":
                                    case "BrotherBodyMesh":
                                        rendererInfo.defaultMaterial = UmbralMithrix.Bundle.LoadAsset<Material>("Assets/Assets/Materials/matNewPhantom.mat");
                                        rendererInfo.defaultMaterialAddress = null;
                                        break;
                                }

                                x.Result.rendererInfos[i] = rendererInfo;
                            }
                        }
                    };
                };

                ContentAddition.AddSkillFamily(newFamily);
                ContentAddition.AddSkillDef(skillDef);
            };
        }

        private void Start()
        {
            foreach (CharacterMaster cm in CharacterMaster.readOnlyInstancesList)
            {
                if (cm.teamIndex == TeamIndex.Player)
                {
                    CharacterBody cb = cm.GetBody();
                    if (cb && cb.isPlayerControlled)
                        playerBodies.Add(cb);
                }
            }
        }

        private void FixedUpdate()
        {
            stopwatch += Time.deltaTime;
            if (stopwatch < interval)
                return;
            playerBodies.Clear();
            foreach (CharacterMaster cm in CharacterMaster.readOnlyInstancesList)
            {
                if (cm.teamIndex == TeamIndex.Player)
                {
                    CharacterBody cb = cm.GetBody();
                    if (cb && cb.isPlayerControlled)
                    {
                        playerBodies.Add(cb);
                    }
                }
            }

            stopwatch %= interval;
            DirectorPlacementRule placementRule = new DirectorPlacementRule();
            placementRule.placementMode = DirectorPlacementRule.PlacementMode.NearestNode;
            placementRule.minDistance = 8f;
            placementRule.maxDistance = 16f;
            placementRule.position = playerBodies[UnityEngine.Random.Range(0, playerBodies.Count)].corePosition;
            Xoroshiro128Plus rng = RoR2Application.rng;
            DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(_cloneCard, placementRule, rng)
            {
                summonerBodyObject = gameObject,
                onSpawnedServer = spawnResult =>
                {
                    if (!spawnResult.success)
                        return;

                    if (spawnResult.spawnedInstance && spawnResult.spawnedInstance.TryGetComponent(out Inventory inventory))
                    {
                        inventory.GiveItemPermanent(RoR2Content.Items.HealthDecay, 10);
                    }
                }
            });
        }
    }
}