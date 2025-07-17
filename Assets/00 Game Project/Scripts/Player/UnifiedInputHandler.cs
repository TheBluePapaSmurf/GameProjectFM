using UnityEngine;
using UnityEngine.InputSystem;

public class UnifiedInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridPlayerController playerController;
    public Joystick movementJoystick;

    [Header("Input Actions")]
    public InputActionReference moveInputAction;

    [Header("Movement Settings")]
    public float moveInterval = 0.15f;
    public bool enableContinuousMovement = true;

    [Header("Input Thresholds")]
    public float keyboardThreshold = 0.5f;
    public float joystickDeadzone = 0.2f;
    public float joystickThreshold = 0.5f;

    private float lastMoveTime;

    void OnEnable()
    {
        if (moveInputAction != null)
        {
            moveInputAction.action.Enable();
        }
    }

    void OnDisable()
    {
        if (moveInputAction != null)
        {
            moveInputAction.action.Disable();
        }
    }

    void Update()
    {
        if (!enableContinuousMovement || playerController == null) return;

        Vector2 totalInput = Vector2.zero;

        // Keyboard/WASD input
        if (moveInputAction != null)
        {
            Vector2 keyboardInput = moveInputAction.action.ReadValue<Vector2>();
            if (keyboardInput.magnitude > keyboardThreshold)
            {
                totalInput = keyboardInput;
            }
        }

        // Joystick input (overschrijft keyboard als actief)
        if (movementJoystick != null)
        {
            Vector2 joystickInput = new Vector2(movementJoystick.Horizontal, movementJoystick.Vertical);
            if (joystickInput.magnitude > joystickDeadzone)
            {
                totalInput = joystickInput;
            }
        }

        // Process movement
        if (totalInput.magnitude > 0 && Time.time >= lastMoveTime + moveInterval)
        {
            Vector2Int direction = GetGridDirection(totalInput);

            if (direction != Vector2Int.zero)
            {
                playerController.MoveToDirection(direction);
                lastMoveTime = Time.time;
            }
        }
    }

    private Vector2Int GetGridDirection(Vector2 input)
    {
        float threshold = input.magnitude > joystickDeadzone ? joystickThreshold : keyboardThreshold;

        if (input.magnitude < threshold)
            return Vector2Int.zero;

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            if (input.x > 0) return Vector2Int.right;
            if (input.x < 0) return Vector2Int.left;
        }
        else
        {
            if (input.y > 0) return Vector2Int.up;
            if (input.y < 0) return Vector2Int.down;
        }

        return Vector2Int.zero;
    }
}
