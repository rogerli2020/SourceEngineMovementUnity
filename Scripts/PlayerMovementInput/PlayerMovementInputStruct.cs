namespace PlayerMovementInput
{
    public struct PlayerMovementInputStruct
    {
        public float DeltaYaw;
        public float DeltaPitch;
        public float CurrentYaw;
        public float CurrentPitch;
        public float ForwardMovement;
        public float SideMovement;
        public float UpMovement;
        public bool Crouching;
        
        // temporary, for rocket jumping
        // public bool Fired;
    }
}