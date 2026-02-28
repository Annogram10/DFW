using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class FirstPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 4.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 6.0f;
		[Tooltip("Crouch speed of the character in m/s")]
		public float CrouchSpeed = 2.0f;
		[Tooltip("Rotation speed of the character")]
		public float RotationSpeed = 1.0f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.1f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.5f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Crouch")]
		[Tooltip("Height of the CharacterController when standing")]
		public float StandingHeight = 2.0f;
		[Tooltip("Height of the CharacterController when crouching")]
		public float CrouchingHeight = 1.0f;
		[Tooltip("How quickly the player transitions between standing and crouching")]
		public float CrouchTransitionSpeed = 10.0f;
		[Tooltip("Overhead check radius to prevent uncrouching inside geometry")]
		public float OverheadCheckRadius = 0.3f;
		[Tooltip("Layers to check overhead when trying to uncrouch")]
		public LayerMask OverheadLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 90.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -90.0f;

		// cinemachine
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// crouch
		private bool _isCrouching = false;
		private float _targetHeight;
		private Vector3 _targetCameraPosition;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

#if ENABLE_INPUT_SYSTEM
		private PlayerInput _playerInput;
#endif
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool IsCurrentDeviceMouse
		{
			get
			{
				#if ENABLE_INPUT_SYSTEM
				return _playerInput.currentControlScheme == "KeyboardMouse";
				#else
				return false;
				#endif
			}
		}

		private void Awake()
		{
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();
#if ENABLE_INPUT_SYSTEM
			_playerInput = GetComponent<PlayerInput>();
#else
			Debug.LogError("Starter Assets package is missing dependencies. Please use Tools/Starter Assets/Reinstall Dependencies to fix it");
#endif
			// initialise controller height and camera position
			_controller.height = StandingHeight;
			_targetHeight = StandingHeight;
			_targetCameraPosition = CinemachineCameraTarget.transform.localPosition;

			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			JumpAndGravity();
			GroundedCheck();
			Crouch();
			Move();
		}

		private void LateUpdate()
		{
			CameraRotation();
		}

		private void GroundedCheck()
		{
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			Grounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);
		}

		private void Crouch()
		{
			// Toggle crouch on button press (only while grounded)
			if (_input.crouch && Grounded)
			{
				if (!_isCrouching)
				{
					_isCrouching = true;
				}
				else if (CanStandUp())
				{
					_isCrouching = false;
				}

				// Reset the input so it acts as a toggle rather than holding
				_input.crouch = false;
			}

			// If crouching but left the ground (e.g. jumped), force standing if there's room
			if (!Grounded && _isCrouching && CanStandUp())
			{
				_isCrouching = false;
			}

			_targetHeight = _isCrouching ? CrouchingHeight : StandingHeight;

			// Smoothly interpolate the CharacterController height
			if (!Mathf.Approximately(_controller.height, _targetHeight))
			{
				_controller.height = Mathf.Lerp(_controller.height, _targetHeight, Time.deltaTime * CrouchTransitionSpeed);

				// Keep the controller's center aligned so the bottom stays grounded
				_controller.center = new Vector3(0, _controller.height / 2f, 0);

				// Move the camera target proportionally with the height change
				float cameraHeightRatio = _controller.height / StandingHeight;
				_targetCameraPosition.y = CinemachineCameraTarget.transform.localPosition.y;
				CinemachineCameraTarget.transform.localPosition = new Vector3(
					CinemachineCameraTarget.transform.localPosition.x,
					Mathf.Lerp(CinemachineCameraTarget.transform.localPosition.y,
						(CrouchingHeight + (StandingHeight - CrouchingHeight) * (1 - (1 - cameraHeightRatio))) * 0.85f,
						Time.deltaTime * CrouchTransitionSpeed),
					CinemachineCameraTarget.transform.localPosition.z
				);
			}
		}

		private bool CanStandUp()
		{
			// Cast a sphere above the player to check for overhead obstructions
			Vector3 overheadPosition = transform.position + Vector3.up * StandingHeight;
			return !Physics.CheckSphere(overheadPosition, OverheadCheckRadius, OverheadLayers, QueryTriggerInteraction.Ignore);
		}

		private void CameraRotation()
		{
			if (_input.look.sqrMagnitude >= _threshold)
			{
				float deltaTimeMultiplier = IsCurrentDeviceMouse ? 1.0f : Time.deltaTime;

				_cinemachineTargetPitch += _input.look.y * RotationSpeed * deltaTimeMultiplier;
				_rotationVelocity = _input.look.x * RotationSpeed * deltaTimeMultiplier;

				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);

				CinemachineCameraTarget.transform.localRotation = Quaternion.Euler(_cinemachineTargetPitch, 0.0f, 0.0f);

				transform.Rotate(Vector3.up * _rotationVelocity);
			}
		}

		private void Move()
		{
			// Use crouch speed if crouching, otherwise normal/sprint speed
			float targetSpeed = _isCrouching ? CrouchSpeed : (_input.sprint ? SprintSpeed : MoveSpeed);

			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}

			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			if (_input.move != Vector2.zero)
			{
				inputDirection = transform.right * _input.move.x + transform.forward * _input.move.y;
			}

			_controller.Move(inputDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				_fallTimeoutDelta = FallTimeout;

				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Prevent jumping while crouching
				if (_input.jump && _jumpTimeoutDelta <= 0.0f && !_isCrouching)
				{
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);
				}

				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				_jumpTimeoutDelta = JumpTimeout;

				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}

				_input.jump = false;
			}

			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;

			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);

			// Draw overhead check gizmo
			Gizmos.color = new Color(0.0f, 0.0f, 1.0f, 0.35f);
			Gizmos.DrawSphere(transform.position + Vector3.up * StandingHeight, OverheadCheckRadius);
		}
	}
}