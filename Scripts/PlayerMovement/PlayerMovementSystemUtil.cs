using UnityEngine;
using PlayerMovement.Enums;
using Vector3 = UnityEngine.Vector3;


namespace PlayerMovement
{
    public static class PlayerMovementSystemUtil
    {
        // public const float CrouchJumpHeight = 0.45f;
        
        /// <summary>
        /// Main driver function for updating velocity and position of the player. Called at each frame/tick.
        /// </summary>
        public static void UpdateVelocity(ref Structs.PlayerMovementComponent pm)
        {
            CheckMovementType(ref pm);
            
            // if sliding, follow airborne logic.
            if (pm.IsSliding) pm.IsGrounded = false;

            // apply external velocity.
            pm.Velocity += pm.ExternalVelocity;
            pm.ExternalVelocity = Vector3.zero;
            
            HandleGravity(ref pm);
            HandleJump(ref pm);
            
            HandleFriction(ref pm);
            
            HandleHorizontalMovement(ref pm);
            ClampHorizontalSpeed(ref pm);
            
            HandleSlide(ref pm);
        }
        
        private static void CheckMovementType(ref Structs.PlayerMovementComponent pm)
        {
            if (pm.IsOnLadder)
            {
                // can't be sliding or bouncing off shit if grabbed onto a ladder.
                pm.IsSliding = false;
                return;
            }
            
            if (pm.IsSliding)
            {
                pm.IsSliding = false;
                for (int i = 0; i < pm.CollisionNormalsBuffer.Count; i++)
                {
                    Vector3 hitNormal = pm.CollisionNormalsBuffer[i];
                    if (Vector3.Distance(hitNormal, pm.CollisionNormalsBuffer[i]) < 0.05f)
                        pm.IsSliding = true;
                }
            }

            for (int i = 0; i < pm.CollisionNormalsBuffer.Count; i++)
            {
                float hitAngle = pm.CollisionAnglesBuffer[i];
                Vector3 hitNormal = pm.CollisionNormalsBuffer[i];

                // if NOT sliding, then...
                // absorb velocity upon collision with walls
                if (hitAngle >= 87.5f)
                {
                    float yVelocity = pm.Velocity.y;
                    pm.Velocity.y = 0f;
                    pm.Velocity -= Vector3.Project(pm.Velocity, hitNormal);
                    pm.Velocity.y = yVelocity;
                }
                
                if (hitAngle > pm.SlopeLimit && hitAngle < 87.5f)
                {
                    
                    if (!pm.IsSliding && pm.OldIsGrounded)
                    {
                        // probably a step offset.
                        pm.IsSliding = false;
                        continue;
                    }
                    pm.IsSliding = true;
                    pm.SlideSurfaceNormal = hitNormal;
                    return;
                }
                
                pm.IsSliding = false;
                
                // if colliding with a plane at high speed
                if (!pm.IsSliding && pm.Velocity.magnitude > 25f && hitAngle >= 5f)
                {
                    float velocityLossFactor = 0.4f;
                    Vector3 reflected = Vector3.Reflect(pm.Velocity.normalized, hitNormal);
                    Vector3 tangent = Vector3.ProjectOnPlane(reflected, hitNormal).normalized;
                    Vector3 shallowedDirection = Vector3.RotateTowards(hitNormal, 
                        tangent, Mathf.Deg2Rad * (90f-hitAngle), 0f);
                    float speedAbsorbed = Vector3.Project(pm.Velocity, hitNormal).magnitude 
                                          * velocityLossFactor;
                    pm.Velocity = shallowedDirection * (pm.Velocity.magnitude - speedAbsorbed);
                }
            }
        }
        
        private static void HandleSlide(ref Structs.PlayerMovementComponent pm)
        {
            if (!pm.IsSliding) return;
            pm.Velocity = Vector3.ProjectOnPlane(pm.Velocity, pm.SlideSurfaceNormal);
            pm.ExternalVelocity = Vector3.ProjectOnPlane(pm.ExternalVelocity, pm.SlideSurfaceNormal);
        }

        private static void HandleFriction(ref Structs.PlayerMovementComponent pm)
        {
            // only apply friction when walking on ground
            if (!pm.IsGrounded) return;

            Vector3 horizontalVelocity = pm.Velocity;
            float horizontalSpeed = Vector3.Magnitude(horizontalVelocity);  // y should be 0 if grounded.
            
            // if not moving, you got nothing to do, return.
            if (horizontalSpeed <= 0f) return;
            
            float dropInSpeed = Mathf.Max(pm.MoveStats.stopSpeed, horizontalSpeed)
                         * pm.MoveStats.friction * pm.DeltaTime;;
            
            // apply the drop in velocity.
            pm.Velocity = Vector3.Normalize(pm.Velocity) * Mathf.Max(0f, horizontalSpeed - dropInSpeed);
            SanitizeVelocity(ref pm);
        }

        /// <summary>
        /// quake engine classic.
        /// </summary>
        private static void HandleHorizontalMovement(ref Structs.PlayerMovementComponent pm)
        {
            Vector3 wishVelocity;
            Vector3 wishDirection;
            float forwardMove, sideMove;
            float wishSpeed;
            float acceleration;
            float addSpeed, currentSpeed, accelSpeed;
            float moveSpeed;
            
            moveSpeed = pm.MoveStats.moveSpeed;
            if (pm.CrouchState != CrouchType.Standing)
                moveSpeed /= 2f;
            
            forwardMove = pm.Cmd.ForwardMovement * moveSpeed;
            sideMove = pm.Cmd.SideMovement * moveSpeed;
            wishVelocity = pm.Forward * forwardMove + pm.Right * sideMove;
            wishDirection = wishVelocity.normalized;
            wishSpeed = wishVelocity.magnitude;
            if (pm.IsSliding)
                acceleration = pm.MoveStats.slideAccel;
            else if (pm.IsGrounded)
                acceleration = pm.MoveStats.groundAccel;
            else
                acceleration = pm.MoveStats.airAccel;

            // clamp wishspeed
            wishSpeed = Mathf.Clamp(wishSpeed, 0f, pm.MoveStats.maxSpeed);

            // calculate speed to add
            currentSpeed = Vector3.Dot(pm.Velocity, wishDirection);
            addSpeed = wishSpeed - currentSpeed;

            // return if no speed to add
            if (addSpeed <= 0f) return;

            accelSpeed = acceleration * pm.DeltaTime * wishSpeed * pm.MoveStats.friction;
            accelSpeed = Mathf.Min(accelSpeed, addSpeed);

            pm.Velocity += accelSpeed * wishDirection;
            SanitizeVelocity(ref pm);
        }

        private static void HandleJump(ref Structs.PlayerMovementComponent pm)
        {
            if (!pm.IsGrounded) return;             // if not grounded, don't jump.
            if (pm.Cmd.UpMovement <= 0f) return;    // if didn't ask to jump, don't jump.
            
            pm.Velocity.y = pm.MoveStats.jumpVelocity * pm.Cmd.UpMovement;
        }

        private static void ClampHorizontalSpeed(ref Structs.PlayerMovementComponent pm)
        {
            if (pm.IsSliding) return;
            
            Vector3 clampedVelocity = pm.Velocity;
            clampedVelocity.y = 0f;

            if (clampedVelocity.magnitude <= pm.MoveStats.maxSpeed) return;
            
            // scale speed down
            clampedVelocity *= (pm.MoveStats.maxSpeed / clampedVelocity.magnitude);
            clampedVelocity.y = pm.Velocity.y;
            
            pm.Velocity = clampedVelocity;
        }

        private static void HandleGravity(ref Structs.PlayerMovementComponent pm) 
            => pm.Velocity.y -= ( (!pm.IsSliding) ? pm.MoveStats.gravity : pm.MoveStats.gravity)
                                * pm.DeltaTime;

        /// <summary>
        /// Main driver function for updating crouch state. Called at each frame/tick.
        /// </summary>
        public static void UpdateCrouch(ref Structs.PlayerMovementComponent pm)
        {
            
            switch (pm.CrouchState)
            {
                case (CrouchType.Standing):
                    // if you're standing but want to start crouching now...
                    if (pm.Cmd.Crouching)
                    {
                        pm.Height /= 2f;
                        pm.HeightToRecover = 0f;
                        pm.CrouchState = CrouchType.Crouching;
                    }
                    break;
                
                case (CrouchType.Crouching):
                    // if you are already crouching, but you are still asking to crouch, do nothing
                    if (pm.Cmd.Crouching) break;
                    
                    // otherwise you wish to stand.
                    pm.CrouchState = CrouchType.WishStanding;
                    goto case CrouchType.WishStanding;
                    
                case CrouchType.WishStanding:
                    // if player now wants to crouch, return to crouching
                    if (pm.Cmd.Crouching)
                    {
                        pm.CrouchState = CrouchType.Crouching;
                        break;
                    }

                    float crouchHeight = pm.Height;
                    float standHeight = crouchHeight * 2f;
                    float radius = pm.Radius;

                    Vector3 bottom = pm.Origin + Vector3.up * (radius); // capsule bottom
                    Vector3 top = bottom + Vector3.up * (standHeight - 2 * radius); // capsule top
                    
                    int ignoreLayer = 6; // ThisPlayer
                    int mask = ~ (1 << ignoreLayer); // everything except layer 6
                    if (!Physics.CheckCapsule(bottom, top + Vector3.up, radius, mask))
                    {
                        pm.Height = standHeight;
                        pm.HeightToRecover = standHeight - crouchHeight;
                        pm.CrouchState = CrouchType.Standing;
                    }
                    
                    break;
            }
            
            
        }
        
        
        private static void SanitizeVelocity(ref Structs.PlayerMovementComponent pm)
        {
            // zero NaN values
            if (float.IsNaN(pm.Velocity.x)) pm.Velocity.x = 0f;
            if (float.IsNaN(pm.Velocity.y)) pm.Velocity.y = 0f;
            if (float.IsNaN(pm.Velocity.z)) pm.Velocity.z = 0f;
            if (float.IsNaN(pm.Origin.x)) pm.Origin.x = 0f;
            if (float.IsNaN(pm.Origin.y)) pm.Origin.y = 0f;
            if (float.IsNaN(pm.Origin.z)) pm.Origin.z = 0f;
            
            // bound velocity
            if (pm.Velocity.x > pm.MoveStats.maxVelocity)
                pm.Velocity.x = pm.MoveStats.maxVelocity;
            if (pm.Velocity.y > pm.MoveStats.maxVelocity)
                pm.Velocity.y = pm.MoveStats.maxVelocity;
            if (pm.Velocity.z > pm.MoveStats.maxVelocity)
                pm.Velocity.z = pm.MoveStats.maxVelocity;
        }
        
    }
}