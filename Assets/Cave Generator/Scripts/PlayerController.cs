using UnityEngine;

namespace CaveGeneration
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed;
        [SerializeField] private float moveAcceleration;
        [SerializeField] private float turnSpeedY;
        [SerializeField] private float turnSpeedX;
        [SerializeField] private float jumpSpeed;
        [SerializeField] private float playerGravity;

        public Transform cameraTransform;
        public Transform cameraHolder;


        CharacterController characterController;

        float playerRotY;
        float cameraRotX;

        float currentMoveSpeed;

        private Vector3 vel;
        private Vector3 movementVel;

        [HideInInspector]
        public Vector3 cameraForwardNoY;
        [HideInInspector]
        public Vector3 cameraRightNoY;

        void Start()
        {
            characterController = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void FixedUpdate()
        {

        }

        void Update()
        {
            //process movement input
            cameraForwardNoY = cameraTransform.forward;
            cameraForwardNoY.y = 0;
            cameraForwardNoY.Normalize();

            cameraRightNoY = cameraTransform.right;
            cameraRightNoY.y = 0;
            cameraRightNoY.Normalize();

            vel = Vector3.zero;

            if (Input.GetKey(KeyCode.D))
            {
                vel.x += moveSpeed;
            }

            if (Input.GetKey(KeyCode.A))
            {
                vel.x -= moveSpeed;
            }

            if (Input.GetKey(KeyCode.W))
            {
                vel.z += moveSpeed;
            }

            if (Input.GetKey(KeyCode.S))
            {
                vel.z -= moveSpeed;
            }

            playerRotY += Input.mousePositionDelta.x * Time.deltaTime * turnSpeedY;
            cameraRotX += Input.mousePositionDelta.y * Time.deltaTime * turnSpeedX;

            cameraRotX = Mathf.Clamp(cameraRotX, -85f, 85f);

            cameraTransform.localRotation = Quaternion.Euler(-cameraRotX, 0, 0);
            cameraHolder.localRotation = Quaternion.Euler(0, playerRotY, 0);

            //apply player movement

            if (Mathf.Approximately(vel.x, 0f) && Mathf.Approximately(vel.z, 0f))
            {
                currentMoveSpeed = Mathf.Lerp(currentMoveSpeed, 0f, moveAcceleration * Time.deltaTime);

                if (currentMoveSpeed < 0.1f)
                {
                    currentMoveSpeed = 0f;
                }


                float yVel = movementVel.y;
                movementVel.y = 0;

                movementVel = movementVel.normalized * currentMoveSpeed;
                movementVel.y = yVel;
            }
            else
            {
                currentMoveSpeed = Mathf.Lerp(currentMoveSpeed, moveSpeed, moveAcceleration * Time.deltaTime);

                float yVel = movementVel.y;
                movementVel.y = 0;

                movementVel = ((cameraForwardNoY * vel.z) + (cameraRightNoY * vel.x)).normalized * currentMoveSpeed;
                movementVel.y = yVel;
            }

            if (characterController.isGrounded)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    movementVel.y = jumpSpeed;
                }
                else
                {
                    movementVel.y = -0.25f;
                }
            }
            else
            {
                movementVel.y += playerGravity * Time.deltaTime;
            }

            characterController.Move(movementVel);
        }
    }
}