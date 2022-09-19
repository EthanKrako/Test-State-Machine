using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerStateMachine : MonoBehaviour
{
    // declare reference variables
    PlayerInput _playerInput;
    CharacterController _characterController;
    Animator _animator;
    Camera _camera;
    Transform _rotationPoint;

    //variables to store optimized setter/getter parameters IDs (Performance optimization)
    int _isWalkingHash;
    int _isRunningHash;
    int _isJumpingHash;
    int _jumpCountHash;

    // variables to store player input values
    Vector2 _currentMovementInput;
    Vector3 _currentMovement;
    Vector3 _currentRunMovement;
    Vector3 _appliedMovement;
    bool _isMovementPressed;
    bool _isRunPressed;

    // constants
    float _rotationFactorPerFrame = 15.0f;
    float _runMultiplier = 3.0f;

    // gravity variables
    float _gravity = -9.8f;
    float _maxFallingSpeed = -20.0f;

    // jumping variables
    bool _isJumpPressed = false;
    float _initialJumpVelocity;
    float _maxJumpHeight = 2.0f;
    float _maxJumpTime = 0.75f;
    bool _isJumping = false;
    bool _requireNewJumpPress = false;
    int _jumpCount = 0;
    Dictionary<int, float> _initialJumpVelocities = new Dictionary<int, float>();
    Dictionary<int, float> _jumpGravities = new Dictionary<int, float>();
    Coroutine _currentJumpResetRoutine = null;

    // State variable
    PlayerBaseState _currentState;
    PlayerStateFactory _states;

    // getters and setters
    public PlayerBaseState CurrentState { get { return _currentState; } set { _currentState = value;}}
    public Animator Animator { get { return _animator; }}
    public CharacterController CharacterController { get { return _characterController; }}
    public Coroutine CurrentJumpResetRoutine { get { return _currentJumpResetRoutine; } set { _currentJumpResetRoutine = value; }}
    public Dictionary<int, float> InitialJumpVelocities { get { return _initialJumpVelocities; }}
    public Dictionary<int, float> JumpGravities { get { return _jumpGravities; }}
    public int JumpCount { get { return _jumpCount; } set { _jumpCount = value; }}
    public int IsJumpingHash { get { return _isJumpingHash; }}
    public int JumpCountHash { get { return _jumpCountHash; }}
    public int IsWalkingHash { get { return _isWalkingHash; }}
    public int IsRunningHash { get { return _isRunningHash; }}
    public bool IsMovementPressed { get { return _isMovementPressed; }}
    public bool IsRunPressed { get { return _isRunPressed; }}
    public bool RequireNewJumpPress { get { return _requireNewJumpPress; } set { _requireNewJumpPress = value; }}
    public bool IsJumping { set { _isJumping = value; }}
    public bool IsJumpPressed { get { return _isJumpPressed; }}
    public float Gravity { get { return _gravity; }}
    public float CurrentMovementY { get { return _currentMovement.y; } set { _currentMovement.y = value; }}
    public float AppliedMovementY { get { return _appliedMovement.y; } set { _appliedMovement.y = value; }}
    public float AppliedMovementX { get { return _appliedMovement.x; } set { _appliedMovement.x = value; }}
    public float AppliedMovementZ { get { return _appliedMovement.z; } set { _appliedMovement.z = value; }}
    public float RunMultiplier { get { return _runMultiplier; }}
    public Vector2 CurrentMovementInput { get { return _currentMovementInput; }}

    void Awake() {
        // Initially set reference variables
        _playerInput = new PlayerInput();
        _characterController = GetComponent<CharacterController>();
        _animator = GetComponent<Animator>();
        _camera = Camera.main;

        // setup states
        _states = new PlayerStateFactory(this);
        _currentState = _states.Grounded();
        _currentState.EnterState();

        // set the parameter hash references
        _isWalkingHash = Animator.StringToHash("isWalking");
        _isRunningHash = Animator.StringToHash("isRunning");
        _isJumpingHash = Animator.StringToHash("isJumping");
        _jumpCountHash = Animator.StringToHash("jumpCount");

        // Get the current inputs
        _playerInput.CharacterControls.Move.started += onMovementInput;
        _playerInput.CharacterControls.Move.canceled += onMovementInput;
        _playerInput.CharacterControls.Move.performed += onMovementInput;
        _playerInput.CharacterControls.Run.started += onRun;
        _playerInput.CharacterControls.Run.canceled += onRun;
        _playerInput.CharacterControls.Jump.started += onJump;
        _playerInput.CharacterControls.Jump.canceled += onJump;

        setupJumpVariables();
    }

    void setupJumpVariables() {
        float timeToApex = _maxJumpTime / 2;
        _gravity = (-2 * _maxJumpHeight) / Mathf.Pow(timeToApex, 2);
        _initialJumpVelocity = (2 * _maxJumpHeight) / timeToApex;
        float secondJumpGravity = (-2 * (_maxJumpHeight + 2)) / Mathf.Pow((timeToApex * 1.25f), 2);
        float secondJumpInitialVelocity = (2 * (_maxJumpHeight + 2)) / (timeToApex * 1.25f);
        float thirdJumpGravity = (-2 * (_maxJumpHeight + 4)) / Mathf.Pow((timeToApex * 1.5f), 2);
        float thirdJumpInitialVelocity = (2 * (_maxJumpHeight + 4)) / (timeToApex * 1.5f);

        _initialJumpVelocities.Add(1, _initialJumpVelocity);
        _initialJumpVelocities.Add(2, secondJumpInitialVelocity);
        _initialJumpVelocities.Add(3, thirdJumpInitialVelocity);

        _jumpGravities.Add(0, _gravity);
        _jumpGravities.Add(1, _gravity);
        _jumpGravities.Add(2, secondJumpGravity);
        _jumpGravities.Add(3, thirdJumpGravity);
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _rotationPoint = _camera.transform;
        _rotationPoint.rotation = new Quaternion(0, _rotationPoint.rotation.y, 0, _rotationPoint.rotation.w);
        handleRotation(_rotationPoint);
        _characterController.Move(_rotationPoint.TransformDirection(_appliedMovement * Time.deltaTime));
        _currentState.UpdateStates();
    }

    // Handle Rotation with fix camera
    void handleRotation() {
        Vector3 positionToLookAt;
        positionToLookAt.x = _currentMovementInput.x;
        positionToLookAt.y = 0.0f;
        positionToLookAt.z = _currentMovementInput.y;

        Quaternion currentRotation = transform.rotation;

        if (_isMovementPressed) {
            Quaternion targetRotation = Quaternion.LookRotation(positionToLookAt);
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, _rotationFactorPerFrame * Time.deltaTime);
        }
    }

    // Handle rotation base on the camera rotation
    void handleRotation(Transform rotationPoint) {
        Vector3 positionToLookAt;
        positionToLookAt.x = _currentMovementInput.x;
        positionToLookAt.y = 0.0f;
        positionToLookAt.z = _currentMovementInput.y;

        Quaternion currentRotation = transform.rotation;

        if (_isMovementPressed) {
            Quaternion targetRotation = Quaternion.LookRotation(positionToLookAt) * rotationPoint.rotation;
            transform.rotation = Quaternion.Slerp(currentRotation, targetRotation, _rotationFactorPerFrame * Time.deltaTime);
        }
    }

    void onMovementInput (InputAction.CallbackContext context) {
        _currentMovementInput = context.ReadValue<Vector2>();
        _isMovementPressed = _currentMovementInput.x != 0 || _currentMovementInput.y != 0;
    }

    void onJump(InputAction.CallbackContext context) {
        _isJumpPressed = context.ReadValueAsButton();
        _requireNewJumpPress = false;
    }

    void onRun(InputAction.CallbackContext context) {
        _isRunPressed = context.ReadValueAsButton();
    }

    private void OnEnable() 
    {
        _playerInput.CharacterControls.Enable();
    }

    private void OnDisable() {
        _playerInput.CharacterControls.Disable();
    }
}
