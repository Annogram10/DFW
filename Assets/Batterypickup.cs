using UnityEngine;

namespace StarterAssets
{
    public class BatteryPickup : MonoBehaviour
    {
        [Header("Pickup Settings")]
        [Tooltip("How much battery this pickup restores (out of 100)")]
        public float rechargeAmount = 30f;

        [Tooltip("Must the player be crouching to pick this up?")]
        public bool requireCrouch = true;

        [Header("Visual Feedback")]
        [Tooltip("How fast the battery bobs up and down")]
        public float bobSpeed = 2f;
        [Tooltip("How high it bobs")]
        public float bobHeight = 0.2f;
        [Tooltip("How fast it spins")]
        public float spinSpeed = 90f;

        private Vector3 _startPosition;
        private HeadlampController _headlamp;

        private void Start()
        {
            _startPosition = transform.position;
            _headlamp = FindObjectOfType<HeadlampController>();
        }

        private void Update()
        {
            // Bob up and down
            float newY = _startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // Spin
            transform.Rotate(Vector3.up * spinSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Check it's the player
            if (!other.CompareTag("Player")) return;

            // Check crouch requirement
            if (requireCrouch)
            {
                StarterAssetsInputs input = other.GetComponentInChildren<StarterAssetsInputs>();
                if (input == null)
                    input = other.GetComponentInParent<StarterAssetsInputs>();
                if (input == null)
                    input = FindObjectOfType<StarterAssetsInputs>();

                // Only pick up if crouching
                FirstPersonController fpc = other.GetComponentInChildren<FirstPersonController>();
                if (fpc == null)
                    fpc = other.GetComponentInParent<FirstPersonController>();

                if (fpc != null && !fpc.IsCrouching)
                    return;
            }

            Collect();
        }

        private void Collect()
        {
            if (_headlamp != null)
                _headlamp.RechargeBattery(rechargeAmount);

            Debug.Log("Battery collected! +" + rechargeAmount + "%");

            // Destroy the pickup
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }
}