using UnityEngine;
using TMPro;

namespace StarterAssets
{
    public class GemCounter : MonoBehaviour
    {
        [Header("UI")]
        public TMP_Text gemText;

        [Header("Settings")]
        public string prefix = "Gems: ";

        [SerializeField] private int _gemCount = 0;

        private void Awake()
        {
            // Auto-grab if you forget to assign it
            if (gemText == null)
                gemText = GetComponentInChildren<TMP_Text>();
            
            UpdateUI();
        }

        public void AddGems(int amount = 1)
        {
            _gemCount += amount;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (gemText != null)
                gemText.text = prefix + _gemCount;
            else
                Debug.LogWarning("GemCounter: gemText not assigned and not found.");
        }
    }
}