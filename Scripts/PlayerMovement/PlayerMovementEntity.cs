using UnityEngine;
using PlayerMovementInput;
using System.Collections.Generic;

namespace PlayerMovement
{
    public class PlayerMovementEntity : MonoBehaviour
    {
        // movement states
        private Structs.PlayerMovementComponent _pmComponent;
        
        // important references
        private Camera _camera;
        private PlayerMovementInputEntity _playerMovementInputEntity;
        private CharacterController _characterController;
        private PlayerMovementInputComponent _playerMovementInputComponent;
        
        // variables
        public float cameraSensitivity = 0.1f;
        
        // private variables just for keeping track of stuff
        private float _currentPitch = 0f;
        private float _currentYaw = 0f;
        private Vector3 _collisionHitNormal;
        private float _collisionHitAngle;
        
        private void Start()
        {
            // assign important references
            _camera = gameObject.GetComponentInChildren<Camera>();
            _playerMovementInputEntity = GetComponent<PlayerMovementInputEntity>();
            _characterController = GetComponent<CharacterController>();
            
            // initialize state data
            _pmComponent = new Structs.PlayerMovementComponent();
            _pmComponent.MoveStats = MoveVars.Instance;
            _pmComponent.MoveType = Enums.MoveType.Default;
            _pmComponent.Height = 1.8f;
            _pmComponent.Radius = 0.45f;
            _pmComponent.CollisionNormalsBuffer = new List<Vector3>();
            _pmComponent.CollisionAnglesBuffer = new List<float>();
            
            // initialize controller data
            _characterController.height = _pmComponent.Height;
            _characterController.radius = _pmComponent.Radius;
            
            // cursors
            Cursor.lockState = CursorLockMode.Locked;  // Lock cursor to the center of the screen
            Cursor.visible = false;                    // Hide the cursor
        }


        private void Update()
        {
            CaptureMovementState();
            ProcessMovementState();
            ApplyMovementState();
        }
        
        private void CaptureMovementState()
        {
            // update input data
            _playerMovementInputComponent = _playerMovementInputEntity.GetPlayerMovementInputComponent();
            float pitchDelta = _playerMovementInputComponent.DeltaPitch * cameraSensitivity;
            _currentPitch = Mathf.Clamp(_currentPitch - pitchDelta, -90f, 90f);
            _currentYaw = 
                transform.eulerAngles.y + _playerMovementInputComponent.DeltaYaw * cameraSensitivity;
            
            // update input cmd
            _playerMovementInputComponent.CurrentPitch = _currentPitch;
            _playerMovementInputComponent.CurrentYaw = _currentYaw;
            
            // update pmComponent
            _pmComponent.Origin = transform.position;
            _pmComponent.Forward = transform.forward;
            _pmComponent.Right = transform.right;
            _pmComponent.Up = transform.up;
            _pmComponent.OldIsGrounded = _pmComponent.IsGrounded;
            _pmComponent.IsGrounded = _characterController.isGrounded;
            _pmComponent.CollisionFlags = _characterController.collisionFlags;
            _pmComponent.SlopeLimit = _characterController.slopeLimit;
            _pmComponent.DeltaTime = Time.deltaTime;
            _pmComponent.CurrentPitch = _currentPitch;
            _pmComponent.CurrentYaw = _currentYaw;
            _pmComponent.Cmd = _playerMovementInputComponent;
        }

        private void ProcessMovementState()
        {
            PlayerMovementSystemUtil.UpdateVelocity(ref _pmComponent);
            PlayerMovementSystemUtil.UpdateCrouch(ref _pmComponent);
        }

        private void ApplyMovementState()
        {
            // 1. check if is on ladder. (this is a pretty bad approach, but it does works like a ladder)
            if (_pmComponent.IsOnLadder)
                _characterController.slopeLimit = 91f;
            else
                _characterController.slopeLimit = 45f;
            
            // 2. update crouch state and height nonlinearly and gradually
            if (_pmComponent.HeightToRecover > 0f)
            {
                float heightToRecoverThisFrame = 
                    (_pmComponent.HeightToRecover <= 0.01f) 
                        ? _pmComponent.HeightToRecover 
                        : (_pmComponent.HeightToRecover / 2f) * (_pmComponent.DeltaTime / .05f);
                _characterController.height += heightToRecoverThisFrame;
                _characterController.Move(Vector3.up * heightToRecoverThisFrame / 2f);
                _pmComponent.HeightToRecover -= heightToRecoverThisFrame;
            }
            else if ( Mathf.Abs(_characterController.height - _pmComponent.Height) > 0.0001f )
                _characterController.height = _pmComponent.Height;
            
            // 3. update rotations
            if (_camera)
                 _camera.transform.localEulerAngles = new Vector3(_pmComponent.CurrentPitch, 0f, 0f);
            transform.localEulerAngles = new Vector3(0f, _pmComponent.CurrentYaw, 0f);
            
            // 4. clear old collision buffers and let Move() populate new ones for the new frame.
            // calculate and apply displacement.
            _pmComponent.IsOnLadder = false;
            _pmComponent.CollisionNormalsBuffer.Clear();
            _pmComponent.CollisionAnglesBuffer.Clear();
            Vector3 move = _pmComponent.Velocity * _pmComponent.DeltaTime;
            _characterController.Move(move);
        }
        
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // collect collision information to be processed by util functions
            _collisionHitNormal = hit.normal;
            _collisionHitAngle = Vector3.Angle(Vector3.up, _collisionHitNormal);
            
            // add to buffers
            _pmComponent.CollisionNormalsBuffer.Add(_collisionHitNormal);
            _pmComponent.CollisionAnglesBuffer.Add(_collisionHitAngle);
            
            if (hit.gameObject.TryGetComponent<Ladder>(out var ladder))
            {
                _pmComponent.IsOnLadder = true;
            }
        }
        
         // void HandleJetpack()
         // {
         //     if (!_playerMovementInputComponent.Fired) return;
         //     
         //     Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
         //     Ray ray = Camera.main.ScreenPointToRay(screenCenter);
         //
         //     if (Physics.Raycast(ray, out RaycastHit hit))
         //     {
         //         float maxDistance = 10f;
         //         float maxVelocity = 5f;
         //         Vector3 hitPoint = hit.point;
         //         Vector3 playerPosition = transform.position;
         //
         //         // Direction *away* from explosion
         //         Vector3 direction = (playerPosition - hitPoint);
         //         float distance = Mathf.Max(direction.magnitude, 1.5f);
         //
         //         if (distance > maxDistance) return;
         //
         //         // Logarithmic impact strength
         //         float impactStrength = Mathf.Clamp(maxVelocity / Mathf.Log(distance), 0f, maxVelocity);
         //
         //         // Final impact velocity
         //         Vector3 rocketImpactVelocity = direction.normalized * impactStrength;
         //
         //         // Apply to player's velocity
         //         _pmComponent.ExternalVelocity += rocketImpactVelocity;
         //     }
         // }
    }
}
