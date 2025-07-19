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
            public readonly float DeltaYaw;
            public readonly float DeltaPitch;
            public readonly float ForwardMovement;
            public readonly float SideMovement;
            public readonly float UpMovement;
            public readonly bool Crouching;

            public ReplicateData(float deltaYaw, float deltaPitch, 
                float forwardMovement, float sideMovement, float upMovement, bool crouching) : this()
            {
                DeltaYaw = deltaYaw;
                DeltaPitch = deltaPitch;
                ForwardMovement = forwardMovement;
                SideMovement = sideMovement;
                UpMovement = upMovement;
                Crouching = crouching;
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
            public readonly Vector3 PlayerRotation;
            public readonly Vector3 CameraRotation;

            public ReconcileData(Vector3 origin, Vector3 velocity, float height, 
                Vector3 playerRotation, Vector3 cameraRotation) : this()
            {
                Origin = origin;
                Velocity = velocity;
                Height = height;
                PlayerRotation = playerRotation;
                CameraRotation = cameraRotation;
            }

            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        private void InitializationShared()
        {
            // assign important references
            _camera = gameObject.GetComponentInChildren<Camera>();
            _playerMovementInputEntity = GetComponent<PlayerMovementInputEntity>();
            _characterController = GetComponent<CharacterController>();
            _characterController.enabled = true;
            
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
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            InitializationShared();
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            InitializationShared();

            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                _camera.enabled = false;
                // _playerMovementInputEntity.enabled = false;
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
        
        public override void CreateReconcile()
        {
            Debug.Log(transform.localEulerAngles);
            Debug.Log(_camera.transform.localEulerAngles);
            ReconcileData rd = new ReconcileData(
                    transform.position,
                    _pmComponent.Velocity,
                    _characterController.height,
                    transform.localEulerAngles,
                    _camera.transform.localEulerAngles
                );
            
            ReconcileState(rd);
        }
        
        [Reconcile]
        private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            _characterController.Move(data.Origin);
            _pmComponent.Velocity = data.Velocity;
            _characterController.height = data.Height;
            transform.localEulerAngles = data.PlayerRotation;
            _camera.transform.localEulerAngles = data.CameraRotation;
        }

        private ReplicateData CreateReplicateData()
        {
            if (!base.IsOwner) return default;
            _playerMovementInputComponent = _playerMovementInputEntity.GetPlayerMovementInputComponent();
            ReplicateData md = new ReplicateData(
                _playerMovementInputComponent.DeltaYaw * cameraSensitivity,
                _playerMovementInputComponent.DeltaPitch * cameraSensitivity,
                _playerMovementInputComponent.ForwardMovement,
                _playerMovementInputComponent.SideMovement,
                _playerMovementInputComponent.UpMovement,
                _playerMovementInputComponent.Crouching
                );
            
            return md;
        }
        
        [Replicate]
        private void RunInputs(ReplicateData data, ReplicateState state = ReplicateState.Invalid, 
            Channel channel = Channel.Unreliable)
        {
            _playerMovementInputComponent.DeltaYaw = data.DeltaYaw;
            _playerMovementInputComponent.DeltaPitch = data.DeltaPitch;
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
            _pmComponent.DeltaTime = (float)base.TimeManager.TickDelta;
            _pmComponent.Cmd = _playerMovementInputComponent;
            _pmComponent.ExternalVelocity = _externalVelocity;
            
            // zero out external velocity
            _externalVelocity = Vector3.zero;
        }

        private void ProcessMovementState()
        {
            PlayerMovementSystemUtil.UpdateRotation(ref _pmComponent);
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
            _camera.transform.localEulerAngles = new Vector3(_pmComponent.CurrentPitch, 0f, 0f);
            transform.localEulerAngles = new Vector3(0f, _pmComponent.CurrentYaw, 0f);
            
            // 4. clear old collision buffers and let Move() populate new ones for the new frame.
            // calculate and apply displacement.
            _pmComponent.IsOnLadder = false;
            _pmComponent.CollisionNormalsBuffer.Clear();
            _pmComponent.CollisionAnglesBuffer.Clear();
            _pmComponent.CollisionCollidersBuffer.Clear();
            Vector3 move = _pmComponent.Velocity * _pmComponent.DeltaTime;
            _characterController.Move(move);
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
