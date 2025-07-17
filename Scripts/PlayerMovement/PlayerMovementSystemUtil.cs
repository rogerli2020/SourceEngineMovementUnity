using UnityEngine;
using PlayerMovement.Enums;
using Unity.VisualScripting;

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
            CheckSliding(ref pm);   // check if you're surfing

            if (pm.IsSliding) pm.IsGrounded = false;    // if you're surfing, follow air logic.

            HandleGravity(ref pm);
            
            // apply external velocity.
            pm.Velocity += pm.ExternalVelocity;
            HandleJump(ref pm);
            
            HandleFriction(ref pm);
            
            HandleHorizontalMovement(ref pm);
            ClampHorizontalSpeed(ref pm);
            
            HandleSlide(ref pm);
        }
        
        private static void CheckSliding(ref Structs.PlayerMovementComponent pm)
        {
            // can't be sliding or bouncing off shit if grabbed onto a ladder.
            if (pm.IsOnLadder)
            {
                // cancel sliding
                pm.IsSliding = false;
                pm.SlideSurfaceNormal.x = 99f;
                return;
            }
            
            // if you're currently sliding, check if you're still sliding this frame.
            if (pm.IsSliding)
            {
                if (pm.CollisionFlags != CollisionFlags.None)
                {
                    pm.IsSliding = false;
                    for (int i = 0; i < pm.CollisionNormalsBuffer.Count; i++)
                        if (Vector3.Distance(pm.CollisionNormalsBuffer[i], pm.SlideSurfaceNormal) < 0.01f)
                        {
                            pm.IsSliding = true;
                        }
                }
                // to handle weird edge case where Character Controller somehow does not detect the sliding surface.
                else
                {
                    float skinWidth = 0.1f;
                    float expandedRadius = pm.Radius + skinWidth;
                    float halfHeight = Mathf.Max(0f, (pm.Height * 0.5f) - pm.Radius);
                    Vector3 up = Vector3.up;
                    Vector3 point1 = pm.Origin + up * halfHeight;
                    Vector3 point2 = pm.Origin - up * halfHeight;
                    Collider[] overlaps = Physics.OverlapCapsule(point1, point2, expandedRadius);
                
                    pm.IsSliding = false;
                    foreach (var collider in overlaps)
                    {
                        if (collider == pm.SlideSurfaceCollider)
                            pm.IsSliding = true;
                    }
                }
            }
            
            // if not sliding, "reset" the SlideSurfaceNormal to something impossible.
            if (!pm.IsSliding)
            {
                pm.SlideSurfaceNormal.x = 99f;
                pm.SlideSurfaceCollider = null;
            }
            
            // for all collided surfaces, regardless of whether you're sliding or not
            for (int i = 0; i < pm.CollisionNormalsBuffer.Count; i++)
            {
                float       hitAngle    = pm.CollisionAnglesBuffer[i];
                Vector3     hitNormal   = pm.CollisionNormalsBuffer[i];
                Collider    hitCollider = pm.CollisionCollidersBuffer[i];

                // absorb velocity upon collision with walls
                if (hitAngle >= 87.5f)
                    pm.Velocity = Vector3.ProjectOnPlane(pm.Velocity, hitNormal);
                
                // if not sliding this frame, check if you're sliding, maybe on a new surface?
                if (hitAngle > pm.SlopeLimit && hitAngle < 87.5f && !pm.IsSliding)
                {
                    // if grounded previous frame?
                    if (pm.OldIsGrounded)
                        continue;   // probably just a step offset...
                    
                    // if not grounded previous frame...
                    pm.IsSliding = true;
                    pm.SlideSurfaceNormal = hitNormal;
                    pm.SlideSurfaceCollider = hitCollider;
                    return;
                }
                
                // if colliding with a plane at high speed AND not sliding, bounce off the surface
                // at a fun angle
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
            => pm.Velocity.y -= pm.MoveStats.gravity * pm.DeltaTime;

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