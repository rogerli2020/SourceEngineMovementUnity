using UnityEngine;
using PlayerMovementInput;
using System.Collections.Generic;
using System.Diagnostics;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEditor.Rendering;
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
        
        // IsOwnerHost
        private readonly SyncVar<bool> _isHost = new(false);

        // replication data
        public struct ReplicateData : IReplicateData
        {
            private uint _tick;
            public readonly Vector3 Forward, Right, Up;
            public readonly float ForwardMovement, SideMovement, UpMovement;
            public readonly bool Crouching;
            public ReplicateData(Vector3 forward, Vector3 right, Vector3 up,
                float forwardMovement, float sideMovement, float upMovement, bool crouching) : this()
            {
                _tick = 0u;
                this.Forward = forward;
                this.Right = right;
                this.Up = up;
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

            public ReconcileData(Vector3 origin, Vector3 velocity, float height) : this()
            {
                _tick = 0;
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
            
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
            
            // check if this is host
            CheckOwnedByHostAndIsHost();
        }
        
        public override void OnStopNetwork()
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }
        
        private void TimeManager_OnTick()
        {
            if (IsOwner && !_isHost.Value)
            {
                ReplicateData data = CreateReplicateData();
                RunInputs(data); 
                ServerRpc_Rotate(_camera.transform.rotation, transform.rotation);
            }
            else if (_isHost.Value)
            {
                // do not predict, if is host.
                CaptureMovementState(CreateReplicateData());
                ProcessMovementState();
                ApplyMovementState();
            }
            else
            {
                RunInputs(default(ReplicateData));
            }
        }
        
        private void TimeManager_OnPostTick()
        {
            if (!IsServerStarted) return;
            CreateReconcile();
        }
        
        public void CreateReconcile()
        {
            ReconcileData rd = new ReconcileData(
                    transform.position,
                    _pmComponent.Velocity,
                    _characterController.height
                );
            
            ReconcileState(rd);
        }
        
        [Reconcile]
        private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            // _characterController.enabled = false;
            // transform.position = data.Origin;
            // _pmComponent.Velocity = data.Velocity;
            // _characterController.height = data.Height;
            // _characterController.enabled = true;
            // _characterController.Move(data.Origin - transform.position);
            _pmComponent.Velocity = data.Velocity;
        }

        private ReplicateData CreateReplicateData()
        {
            PlayerMovementInputComponent curInput = _playerMovementInputEntity.GetPlayerMovementInputComponent();
            ReplicateData md = new ReplicateData(
                    transform.forward,
                    transform.right,
                    transform.up,
                    curInput.ForwardMovement,
                    curInput.SideMovement,
                    curInput.UpMovement,
                    curInput.Crouching
                );
            
            return md;
        }
        
        [Replicate]
        private void RunInputs(
            ReplicateData data, 
            ReplicateState state = ReplicateState.Invalid, 
            Channel channel = Channel.Unreliable)
        {
            CaptureMovementState(data);
            ProcessMovementState(); 
            ApplyMovementState();
        }
        
        private void CaptureMovementState(ReplicateData replicateData)
        {
            // update pmComponent
            _pmComponent.curTick = TimeManager.Tick;
            _pmComponent.Forward = replicateData.Forward;
            _pmComponent.Right = replicateData.Right;
            _pmComponent.Up = replicateData.Up;
            _pmComponent.Origin = transform.position;
            _pmComponent.OldIsGrounded = _pmComponent.IsGrounded;
            _pmComponent.IsGrounded = _characterController.isGrounded;
            _pmComponent.CollisionFlags = _characterController.collisionFlags;
            _pmComponent.SlopeLimit = _characterController.slopeLimit;
            _pmComponent.DeltaTime = Time.fixedDeltaTime;
            _pmComponent.Cmd.ForwardMovement = replicateData.ForwardMovement;
            _pmComponent.Cmd.SideMovement = replicateData.SideMovement;
            _pmComponent.Cmd.UpMovement = replicateData.UpMovement;
            _pmComponent.Cmd.Crouching = replicateData.Crouching;
            _pmComponent.ExternalVelocity = _externalVelocity;
        }

        private void ProcessMovementState()
        {
            PlayerMovementSystemUtil.UpdateVelocity(ref _pmComponent);
            PlayerMovementSystemUtil.UpdateCrouch(ref _pmComponent);
        }

        [ServerRpc]
        void ServerRpc_Rotate(Quaternion cameraRotation, Quaternion transformRotation)
        {
            _characterController.enabled = false;
            _camera.transform.rotation = cameraRotation;
            transform.rotation = transformRotation;
            _characterController.enabled = true;
        }

        private void ApplyMovementState()
        {
            // 1. check if is on ladder. (this is a pretty bad approach, but it does works like a ladder)
            _characterController.slopeLimit = (_pmComponent.IsOnLadder) ? 91f : 45f;
            
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
            
            // 4. clear old collision buffers and let Move() populate new ones for the new frame.
            // calculate and apply displacement.
            _pmComponent.IsOnLadder = false;
            _pmComponent.CollisionNormalsBuffer.Clear();
            _pmComponent.CollisionAnglesBuffer.Clear();
            _pmComponent.CollisionCollidersBuffer.Clear();
            Vector3 move = _pmComponent.Velocity * _pmComponent.DeltaTime;
            _characterController.enabled = true;
            _characterController.Move(move);
        }
        
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // add to buffers
            _pmComponent.CollisionNormalsBuffer.Add(hit.normal);
            _pmComponent.CollisionAnglesBuffer.Add(Vector3.Angle(Vector3.up, hit.normal));
            _pmComponent.CollisionCollidersBuffer.Add(hit.collider);
            
            // if collided with a Ladder, set IsOnLadder to true
            if (hit.gameObject.TryGetComponent<Ladder>(out var _))
                _pmComponent.IsOnLadder = true;
        }

        public void Update()
        {
            if (!IsOwner) return;
            // handle rotations client side every frame
            _pmComponent.Cmd = _playerMovementInputEntity.GetPlayerMovementInputComponent();    // pull every frame
            PlayerMovementSystemUtil.UpdateRotation(ref _pmComponent, cameraSensitivity);       // update every frame
            _characterController.enabled = false;
            _camera.transform.localEulerAngles = new Vector3(_pmComponent.CurrentPitch, 0f, 0f);
            transform.localEulerAngles = new Vector3(0f, _pmComponent.CurrentYaw, 0f);
            _characterController.enabled = true;
        }

        private bool CheckOwnedByHostAndIsHost()
        {
            if (!IsServerStarted) return false;
            if (NetworkManager.ClientManager.Connection.ClientId == 0 && OwnerId == 0)
            {
                _isHost.Value = true;
                return true;
            }

            _isHost.Value = false;
            return false;
        }
        
        public void SetExternalVelocity(Vector3 externalVelocity) 
            => _externalVelocity = externalVelocity;
        
    }
}
