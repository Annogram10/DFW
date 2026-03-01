using UnityEngine;
using TMPro;   // ✅ changed from UnityEngine.UI

namespace StarterAssets
{
    public class GemCounter : MonoBehaviour
    {
        [Header("UI")]
        public TMP_Text gemText;   // ✅ changed from Text to TMP_Text

        [Header("Settings")]
        public string prefix = "Gems: ";

        private int _gemCount = 0;

        private void Start()
        {
            UpdateUI();
        }

        public void AddGems(int amount)
        {
            _gemCount += amount;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (gemText != null)
                gemText.text = prefix + _gemCount;
        }
    }
}