using UnityEngine;

namespace StarterAssets
{
    public class HeadlampController : MonoBehaviour
    {
        [Header("Light Settings")]
        public Light headlight;
        public float maxIntensity = 6f;
        public float minIntensity = 1.5f;

        [Header("Battery")]
        public float battery = 100f;
        public float drainPerSecond = 2f;
        public float sprintDrainMultiplier = 2.5f;

        [Header("Flicker - Low Battery")]
        [Tooltip("Battery % at which light starts flickering slightly")]
        public float lowBatteryThreshold = 30f;
        [Tooltip("Battery % at which flickering gets heavy")]
        public float criticalBatteryThreshold = 10f;

        [Header("Flicker - Enemy Proximity")]
        [Tooltip("Assign your enemy Transform here")]
        public Transform enemy;
        [Tooltip("Distance at which the enemy starts causing flicker")]
        public float enemyFlickerRange = 8f;

        // Internal
        private StarterAssetsInputs _input;
        private float _baseIntensity;
        private float _flickerTimer;
        private float _flickerInterval;
        private bool _isFlickering;

        private void Awake()
        {
            _input = GetComponentInParent<StarterAssetsInputs>();

            if (headlight == null)
                headlight = GetComponentInChildren<Light>();

            if (headlight == null)
                Debug.LogError("HeadlampController: No Light component found! Attach a Spotlight as a child of the camera.");

            _baseIntensity = maxIntensity;
            _flickerInterval = Random.Range(0.05f, 0.15f);
        }

        private void Update()
        {
            if (headlight == null) return;

            DrainBattery();
            HandleIntensity();
            HandleFlicker();
        }

        // ─── Battery ────────────────────────────────────────────────

        private void DrainBattery()
        {
            bool isSprinting = _input != null && _input.sprint;
            float drain = drainPerSecond * (isSprinting ? sprintDrainMultiplier : 1f);
            battery -= drain * Time.deltaTime;
            battery = Mathf.Clamp(battery, 0f, 100f);
        }

        /// <summary>Call this when the player picks up a battery.</summary>
        public void RechargeBattery(float amount)
        {
            battery = Mathf.Clamp(battery + amount, 0f, 100f);
        }

        // ─── Intensity ───────────────────────────────────────────────

        private void HandleIntensity()
        {
            if (battery <= 0f)
            {
                headlight.enabled = false;
                return;
            }

            headlight.enabled = true;

            // Scale intensity with battery level — starts dropping below 30%
            float batteryRatio = Mathf.Clamp01(battery / lowBatteryThreshold);
            _baseIntensity = Mathf.Lerp(minIntensity, maxIntensity, batteryRatio);
        }

        // ─── Flicker ─────────────────────────────────────────────────

        private void HandleFlicker()
        {
            if (battery <= 0f) return;

            bool nearEnemy = enemy != null &&
                             Vector3.Distance(transform.position, enemy.position) <= enemyFlickerRange;

            bool lowBattery   = battery <= lowBatteryThreshold;
            bool critBattery  = battery <= criticalBatteryThreshold;

            // Determine flicker intensity
            float flickerStrength = 0f;
            if (critBattery)   flickerStrength = 0.8f;
            else if (lowBattery) flickerStrength = 0.35f;
            if (nearEnemy)     flickerStrength = Mathf.Max(flickerStrength, 0.6f);

            if (flickerStrength <= 0f)
            {
                // No flicker — set clean intensity
                headlight.intensity = _baseIntensity;
                return;
            }

            // Tick flicker timer
            _flickerTimer -= Time.deltaTime;
            if (_flickerTimer <= 0f)
            {
                _isFlickering = !_isFlickering;
                _flickerTimer = Random.Range(0.04f, 0.2f / flickerStrength);
            }

            if (_isFlickering)
            {
                // Random dip in intensity
                float dip = Random.Range(0f, flickerStrength);
                headlight.intensity = Mathf.Max(0f, _baseIntensity - _baseIntensity * dip);
            }
            else
            {
                headlight.intensity = _baseIntensity;
            }
        }

        // ─── Debug ───────────────────────────────────────────────────

        private void OnDrawGizmosSelected()
        {
            if (enemy == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, enemyFlickerRange);
        }
    }
}   