using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerMovementInput
{
    public class PlayerMovementInput : MonoBehaviour
    {
        private InputAction _lookAction;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private InputAction _scrollWheelAction;
        private PlayerMovementInputStruct _playerMovementInputStruct;
        
        // temporary, for rocket jumping
        private InputAction _fireAction;

        
        void Start()
        {
            // initialize movement inputs
            _lookAction = InputSystem.actions.FindAction("look");
            _moveAction = InputSystem.actions.FindAction("move");
            _jumpAction = InputSystem.actions.FindAction("jump");
            _crouchAction = InputSystem.actions.FindAction("crouch");
            _scrollWheelAction = InputSystem.actions.FindAction("ScrollWheel");
            
            // temporary, for rocket jumping
            _fireAction = InputSystem.actions.FindAction("Attack");
            
            // Enable actions
            _lookAction?.Enable();
            _moveAction?.Enable();
            _jumpAction?.Enable();
            _crouchAction?.Enable();
        }
        
        private void Update() { UpdateInput(); }

        private void UpdateInput()
        {
            Vector2 lookValue = _lookAction.ReadValue<Vector2>();
            Vector2 moveValue = _moveAction.ReadValue<Vector2>();
            bool crouchPressed = _crouchAction.IsPressed();
            bool jumpPressed = _jumpAction.IsPressed();
            // Vector2 scrollWheelValue = _scrollWheelAction.ReadValue<Vector2>();

            _playerMovementInputStruct.DeltaPitch += lookValue.y;
            _playerMovementInputStruct.DeltaYaw += lookValue.x;
            _playerMovementInputStruct.ForwardMovement = moveValue.y;
            _playerMovementInputStruct.SideMovement = moveValue.x;
            
            // if (scrollWheelValue.y < 0f)
            //     _playerMovementInputComponent.Crouching = true;
            // else if (scrollWheelValue.y > 0f)
            //     _playerMovementInputComponent.UpMovement = 1f;
            
            _playerMovementInputStruct.UpMovement = jumpPressed ? 1f : 0f;
            _playerMovementInputStruct.Crouching = crouchPressed;
            
                        
            // temporary, for rocket jumping
            // _playerMovementInputComponent.Fired = _fireAction.IsPressed();
            
        }

        public PlayerMovementInputStruct GetPlayerMovementInputComponent()
        {
            var inputSnapshot = _playerMovementInputStruct;

            _playerMovementInputStruct.DeltaPitch = 0f;
            _playerMovementInputStruct.DeltaYaw = 0f;

            return inputSnapshot;
        }

    }
}