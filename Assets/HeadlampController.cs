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
        public float lowBatteryThreshold = 30f;
        public float criticalBatteryThreshold = 10f;

        [Header("Flicker - Enemy Proximity")]
        public Transform enemy;
        public float enemyFlickerRange = 8f;

        // Internal
        private StarterAssetsInputs _input;
        private float _baseIntensity;
        private float _flickerTimer;
        private bool _isFlickering;
        private bool _lightOn = true;

        private void Awake()
        {
            _input = FindObjectOfType<StarterAssetsInputs>();

            if (_input == null)
                Debug.LogError("HeadlampController: No StarterAssetsInputs found in scene!");

            if (headlight == null)
                headlight = GetComponentInChildren<Light>();

            if (headlight == null)
                Debug.LogError("HeadlampController: No Light component found!");

            _baseIntensity = maxIntensity;
        }

        private void Update()
        {
            if (headlight == null) return;

            // Debug — remove once working
            if (Input.GetKeyDown(KeyCode.F))
                Debug.Log("F key detected via legacy input");

            if (_input != null && _input.flashlight)
            {
                Debug.Log("Flashlight toggle triggered!");
                _lightOn = !_lightOn;
                _input.flashlight = false;
            }

            if (!_lightOn)
            {
                headlight.enabled = false;
                return;
            }

            DrainBattery();
            HandleIntensity();
            HandleFlicker();
        }

        // --- Battery ---

        private void DrainBattery()
        {
            bool isSprinting = _input != null && _input.sprint;
            float drain = drainPerSecond * (isSprinting ? sprintDrainMultiplier : 1f);
            battery -= drain * Time.deltaTime;
            battery = Mathf.Clamp(battery, 0f, 100f);
        }

        public void RechargeBattery(float amount)
        {
            battery = Mathf.Clamp(battery + amount, 0f, 100f);
        }

        // --- Intensity ---

        private void HandleIntensity()
        {
            if (battery <= 0f)
            {
                headlight.enabled = false;
                return;
            }

            headlight.enabled = true;

            float batteryRatio = Mathf.Clamp01(battery / lowBatteryThreshold);
            _baseIntensity = Mathf.Lerp(minIntensity, maxIntensity, batteryRatio);
        }

        // --- Flicker ---

        private void HandleFlicker()
        {
            if (battery <= 0f) return;

            bool nearEnemy   = enemy != null &&
                               Vector3.Distance(transform.position, enemy.position) <= enemyFlickerRange;
            bool lowBattery  = battery <= lowBatteryThreshold;
            bool critBattery = battery <= criticalBatteryThreshold;

            float flickerStrength = 0f;
            if (critBattery)     flickerStrength = 0.8f;
            else if (lowBattery) flickerStrength = 0.35f;
            if (nearEnemy)       flickerStrength = Mathf.Max(flickerStrength, 0.6f);

            if (flickerStrength <= 0f)
            {
                headlight.intensity = _baseIntensity;
                return;
            }

            _flickerTimer -= Time.deltaTime;
            if (_flickerTimer <= 0f)
            {
                _isFlickering = !_isFlickering;
                _flickerTimer = Random.Range(0.04f, 0.2f / flickerStrength);
            }

            headlight.intensity = _isFlickering
                ? Mathf.Max(0f, _baseIntensity - _baseIntensity * Random.Range(0f, flickerStrength))
                : _baseIntensity;
        }

        // --- Gizmos ---

        private void OnDrawGizmosSelected()
        {
            if (enemy == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, enemyFlickerRange);
        }
    }
}