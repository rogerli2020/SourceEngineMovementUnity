using UnityEngine;
using PlayerMovementInput;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using Debug = UnityEngine.Debug;

namespace PlayerMovement
{
    public class PredictedPlayerMovementEntity : NetworkBehaviour
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
        private Vector3 _externalVelocity = Vector3.zero;
        private float _accumulatedDeltaPitch;
        private float _accumulatedDeltaYaw;
        
        // replication data
        public struct ReplicateData : IReplicateData
        {
            private uint _tick;
            public readonly Vector3 Forward, Right, Up;
            public readonly Quaternion Rotation;
            public readonly float ForwardMovement, SideMovement, UpMovement;
            public bool Crouching;
            public ReplicateData(Vector3 forward, Vector3 right, Vector3 up, Quaternion rotation,
                float forwardMovement, float sideMovement, float upMovement, bool crouching) : this()
            {
                this.Forward = forward;
                this.Right = right;
                this.Up = up;
                this.Rotation = rotation;
                this.ForwardMovement = forwardMovement;
                this.SideMovement = sideMovement;
                this.UpMovement = upMovement;
                this.Crouching = crouching;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        
        // reconciliation data
        public struct ReconcileData : IReconcileData
        {
            private uint _tick;
            public readonly Vector3 Origin;
            public readonly Vector3 Velocity;
            public readonly float Height;

            public ReconcileData(Vector3 origin, Vector3 velocity, float height, 
                Quaternion playerRotation, Quaternion cameraRotation) : this()
            {
                Origin = origin;
                Velocity = velocity;
                Height = height;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public override void OnStartNetwork()
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
            _pmComponent.CollisionCollidersBuffer = new List<Collider>();
            _pmComponent.SlidingColliderCheckBuffer = new Collider[16];
            
            // initialize controller data
            _characterController.height = _pmComponent.Height;
            _characterController.radius = _pmComponent.Radius;
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                _camera.enabled = false;
                _playerMovementInputEntity.enabled = false;
            }
            
            // for predictions
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }
        
        public override void OnStopNetwork()
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }
        
        private void TimeManager_OnTick()
        {
            RunInputs(CreateReplicateData());
        }
        
        private void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }
        
        public void CreateReconcile()
        {
            ReconcileData rd = new ReconcileData(
                    transform.position,
                    _pmComponent.Velocity,
                    _characterController.height,
                    transform.rotation,
                    _camera.transform.rotation
                );
            
            ReconcileState(rd);
        }
        
        [Reconcile]
        private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            _characterController.enabled = false;
            transform.position = data.Origin;
            _pmComponent.Velocity = data.Velocity;
            _characterController.height = data.Height;
            _characterController.enabled = true;
        }

        private ReplicateData CreateReplicateData()
        {
            if (!_playerMovementInputEntity) return default;
            _playerMovementInputComponent = _playerMovementInputEntity.GetPlayerMovementInputComponent();
            ReplicateData md = new ReplicateData(
                _pmComponent.Forward,
                _pmComponent.Right,
                _pmComponent.Up,
                transform.rotation,
                _playerMovementInputComponent.ForwardMovement,
                _playerMovementInputComponent.SideMovement,
                _playerMovementInputComponent.UpMovement,
                _playerMovementInputComponent.Crouching
                );
            
            return md;
        }
        
        [Replicate]
        private void RunInputs(
            ReplicateData data, 
            FishNet.Object.Prediction.ReplicateState state = FishNet.Object.Prediction.ReplicateState.Invalid, 
            Channel channel = Channel.Unreliable)
        {
            _characterController.enabled = false;
            transform.forward = data.Forward;
            transform.right = data.Right;
            transform.up = data.Up;
            transform.rotation = data.Rotation;
            _characterController.enabled = true;
            _playerMovementInputComponent.ForwardMovement = data.ForwardMovement;
            _playerMovementInputComponent.SideMovement = data.SideMovement;
            _playerMovementInputComponent.UpMovement = data.UpMovement;
            _playerMovementInputComponent.Crouching = data.Crouching;
            
            CaptureMovementState();
            ProcessMovementState();
            ApplyMovementState();
        }
        
        private void CaptureMovementState()
        {
            // update pmComponent
            _pmComponent.Origin = transform.position;
            _pmComponent.Forward = transform.forward;
            _pmComponent.Right = transform.right;
            _pmComponent.Up = transform.up;
            _pmComponent.OldIsGrounded = _pmComponent.IsGrounded;
            _pmComponent.IsGrounded = _characterController.isGrounded;
            _pmComponent.CollisionFlags = _characterController.collisionFlags;
            _pmComponent.SlopeLimit = _characterController.slopeLimit;
            _pmComponent.DeltaTime = (float)TimeManager.TickDelta;
            _pmComponent.Cmd = _playerMovementInputComponent;
            _pmComponent.ExternalVelocity = _externalVelocity;
        }

        private void ProcessMovementState()
        {
            PlayerMovementSystemUtil.UpdateRotation(ref _pmComponent, cameraSensitivity);
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
                _characterController.enabled = true;
                _characterController.Move(Vector3.up * heightToRecoverThisFrame / 2f);
                _pmComponent.HeightToRecover -= heightToRecoverThisFrame;
            }
            else if ( Mathf.Abs(_characterController.height - _pmComponent.Height) > 0.0001f )
                _characterController.height = _pmComponent.Height;
            
            // 3. update rotations
            if (IsOwner)
            {
                _camera.transform.localEulerAngles = new Vector3(_pmComponent.CurrentPitch, 0f, 0f);
                transform.localEulerAngles = new Vector3(0f, _pmComponent.CurrentYaw, 0f);
            }
            
            // 4. clear old collision buffers and let Move() populate new ones for the new frame.
            // calculate and apply displacement.
            _pmComponent.IsOnLadder = false;
            _pmComponent.CollisionNormalsBuffer.Clear();
            _pmComponent.CollisionAnglesBuffer.Clear();
            _pmComponent.CollisionCollidersBuffer.Clear();
            Vector3 move = _pmComponent.Velocity * _pmComponent.DeltaTime;
            _characterController.enabled = true;
            _characterController.Move(move);
            
            // if (IsServerInitialized) Debug.Log($"Server: {transform.rotation}");
            // if (IsOwner) Debug.Log($"Owner: {transform.rotation}");
        }
        
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // add to buffers
            _pmComponent.CollisionNormalsBuffer.Add(hit.normal);
            _pmComponent.CollisionAnglesBuffer.Add(Vector3.Angle(Vector3.up, hit.normal));
            _pmComponent.CollisionCollidersBuffer.Add(hit.collider);
            
            // if collided with a Ladder, set IsOnLadder to true
            if (hit.gameObject.TryGetComponent<Ladder>(out var ladder))
            {
                _pmComponent.IsOnLadder = true;
            }
        }

        public void SetExternalVelocity(Vector3 externalVelocity)
        {
            _externalVelocity = externalVelocity;
        }
    }
}
