using System;
using System.Collections.ObjectModel;
using RoR2;
using RoR2.Projectile;
using UnityEngine;

//honestly this is probably exactly what a patcher would be good for, I'm just adding pullMagnitudeDistanceCurve
public class PullController : MonoBehaviour
{
	[NonSerialized]
	public GameObject owner;

	private CharacterBody ownerBody;

	private TeamFilter teamFilter;

	private CharacterLosTracker losTracker;

	public float losObstructionFactor = 0.5f;

	public AnimationCurve pullMagnitudeTimeCurve;
    public AnimationCurve pullMagnitudeDistanceCurve;

	public float durationOfMaxPull;

	public float distanceForMaxPull;

	[Tooltip("If true, affects every team EXCEPT the defined team.")]
	[SerializeField]
	private bool invertTeamFilter;

	private float age;

	private void Start()
	{
		if (transform.parent.TryGetComponent(out ProjectileController projectileController))
		{
			owner = projectileController.owner;
			ownerBody = owner.GetComponent<CharacterBody>();
		}
		teamFilter = transform.parent.GetComponent<TeamFilter>();
        losTracker = new CharacterLosTracker
        {
            enabled = true
        };
    }

	private void OnDisable()
	{
		losTracker.enabled = false;
		losTracker.Dispose();
		losTracker = null;
	}

	private void FixedUpdate()
	{

	}

	private bool CheckTeamFilter(CharacterBody body)
	{
		bool isSameTeam = false;
		if (body.teamComponent.teamIndex == teamFilter.teamIndex)
		{
			isSameTeam = true;
		}
		if (invertTeamFilter)
		{
			isSameTeam = !isSameTeam;
		}
		return isSameTeam;
	}
}