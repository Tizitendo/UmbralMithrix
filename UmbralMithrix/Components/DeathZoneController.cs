using RoR2;
using UnityEngine;

namespace UmbralMithrix
{
    public class DeathZoneController : MonoBehaviour
    {
        private SphereZone zone;
        private float stopwatch = 0f;
        private float interval = 1f;
        private float zoneRadius = 100f;

        private void Start()
        {
            zone = GetComponent<SphereZone>();
        }

        private void FixedUpdate()
        {
            stopwatch += Time.deltaTime;
            if (stopwatch < interval)
                return;

            stopwatch %= interval;

            if (zone.Networkradius > zoneRadius)
            {
                zone.Networkradius -= 10f;
            }
        }
    }
}