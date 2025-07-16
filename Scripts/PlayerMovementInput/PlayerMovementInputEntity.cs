using UnityEngine;
using UnityEngine.InputSystem;

namespace PlayerMovementInput
{
    public class PlayerMovementInputEntity : MonoBehaviour
    {
        private InputAction _lookAction;
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _crouchAction;
        private InputAction _scrollWheelAction;
        private PlayerMovementInputComponent _playerMovementInputComponent;
        
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

            _playerMovementInputComponent.DeltaPitch += lookValue.y;
            _playerMovementInputComponent.DeltaYaw += lookValue.x;
            _playerMovementInputComponent.ForwardMovement = moveValue.y;
            _playerMovementInputComponent.SideMovement = moveValue.x;
            
            // if (scrollWheelValue.y < 0f)
            //     _playerMovementInputComponent.Crouching = true;
            // else if (scrollWheelValue.y > 0f)
            //     _playerMovementInputComponent.UpMovement = 1f;
            
            _playerMovementInputComponent.UpMovement = jumpPressed ? 1f : 0f;
            _playerMovementInputComponent.Crouching = crouchPressed;
            
                        
            // temporary, for rocket jumping
            // _playerMovementInputComponent.Fired = _fireAction.IsPressed();
            
        }

        public PlayerMovementInputComponent GetPlayerMovementInputComponent()
        {
            var inputSnapshot = _playerMovementInputComponent;

            _playerMovementInputComponent.DeltaPitch = 0f;
            _playerMovementInputComponent.DeltaYaw = 0f;

            return inputSnapshot;
        }

    }
}