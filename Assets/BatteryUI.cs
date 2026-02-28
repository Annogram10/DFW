using UnityEngine;
using UnityEngine.UI;

namespace StarterAssets
{
    /// <summary>
    /// Attach this to a Canvas UI object.
    /// Reads battery level from HeadlampController and updates a UI bar + color.
    /// </summary>
    public class BatteryUI : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The Image component used as the battery fill bar (Image Type: Filled)")]
        public Image fillBar;

        [Tooltip("Optional text label showing battery % (can leave empty)")]
        public Text batteryLabel;

        [Header("Colors")]
        public Color fullColor    = new Color(0.2f, 1f, 0.4f);   // green
        public Color mediumColor  = new Color(1f, 0.85f, 0.1f);  // yellow
        public Color lowColor     = new Color(1f, 0.3f, 0.1f);   // red

        [Header("Thresholds")]
        public float mediumThreshold = 50f;
        public float lowThreshold    = 20f;

        [Header("Pulse when Critical")]
        public bool pulseWhenLow = true;
        public float pulseSpeed  = 3f;

        private HeadlampController _headlamp;

        private void Start()
        {
            _headlamp = FindObjectOfType<HeadlampController>();

            if (_headlamp == null)
                Debug.LogError("BatteryUI: No HeadlampController found in scene!");

            if (fillBar == null)
                Debug.LogError("BatteryUI: No fill bar Image assigned!");
        }

        private void Update()
        {
            if (_headlamp == null || fillBar == null) return;

            float pct = _headlamp.battery / 100f;

            // Fill amount
            fillBar.fillAmount = pct;

            // Color
            Color targetColor;
            if (_headlamp.battery <= lowThreshold)
                targetColor = lowColor;
            else if (_headlamp.battery <= mediumThreshold)
                targetColor = Color.Lerp(lowColor, mediumColor,
                    (_headlamp.battery - lowThreshold) / (mediumThreshold - lowThreshold));
            else
                targetColor = Color.Lerp(mediumColor, fullColor,
                    (_headlamp.battery - mediumThreshold) / (100f - mediumThreshold));

            // Pulse alpha when critically low
            if (pulseWhenLow && _headlamp.battery <= lowThreshold)
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * pulseSpeed));
                targetColor.a = Mathf.Lerp(0.4f, 1f, alpha);
            }
            else
            {
                targetColor.a = 1f;
            }

            fillBar.color = targetColor;

            // Optional label
            if (batteryLabel != null)
                batteryLabel.text = Mathf.CeilToInt(_headlamp.battery) + "%";
        }
    }
}