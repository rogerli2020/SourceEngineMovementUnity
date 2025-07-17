using PlayerMovementInput;
using UnityEngine;
using System.Collections.Generic;

namespace PlayerMovement.Structs
{
    
    public struct PlayerMovementComponent
    {
        // positions
        public Vector3 Origin;
        public Vector3 Forward, Right, Up;
        
        // crouch status
        public Enums.CrouchType CrouchState;
        
        // velocities
        public Vector3 Velocity;
        
        // collisions
        public bool IsGrounded;
        public CollisionFlags CollisionFlags;
        public float SlopeLimit;
        public List<Vector3> CollisionNormalsBuffer;
        public List<float> CollisionAnglesBuffer;
        public List<Collider> CollisionCollidersBuffer;
        public bool OldIsGrounded;
        
        // movement base stats
        public MoveVars MoveStats;
        
        // commands
        public PlayerMovementInputComponent Cmd;
        
        // size
        public float Height;
        public float Radius;
        
        // DeltaTime
        public float DeltaTime;
        
        // rotations
        public float CurrentPitch;
        public float CurrentYaw;
        
        // move type
        public Enums.MoveType MoveType;
        
        // sliding and ladder
        public bool IsSliding;
        public Vector3 SlideSurfaceNormal;
        public Collider SlideSurfaceCollider;
        public bool IsOnLadder;

        // external velocity
        public Vector3 ExternalVelocity;
        
        // height to recover
        public float HeightToRecover;
    }
}