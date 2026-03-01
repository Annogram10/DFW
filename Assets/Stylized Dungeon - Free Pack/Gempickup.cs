using UnityEngine;

namespace StarterAssets
{
    public class GemPickup : MonoBehaviour
    {
        [Header("Settings")]
        public int gemValue = 1;
        public float bobSpeed = 2f;
        public float bobHeight = 0.15f;
        public float spinSpeed = 120f;

        [Header("Visual")]
        public Light gemGlow;
        public float glowIntensity = 1.5f;

        private Vector3 _startPosition;

        private void Start()
        {
            _startPosition = transform.position;

            // Auto-find or create a point light for glow effect
            if (gemGlow == null)
            {
                gemGlow = GetComponentInChildren<Light>();
            }
        }

        private void Update()
        {
            // Bob
            float newY = _startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Spin
            transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            // Add to counter
            GemCounter counter = FindObjectOfType<GemCounter>();
            if (counter != null)
                counter.AddGems(gemValue);

            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }
}