using UnityEngine;
using UnityEngine.InputSystem;

public class JoystickToInputSystem : MonoBehaviour
{
    [Header("Joystick Reference")]
    public Joystick movementJoystick;

    [Header("Player Reference")]
    public GridPlayerController playerController;

    [Header("Settings")]
    public float inputThreshold = 0.5f;
    public float inputCooldown = 0.3f;

    private float lastInputTime;
    private Vector2 lastInput;

    void Update()
    {
        if (movementJoystick != null && playerController != null)
        {
            Vector2 joystickInput = new Vector2(movementJoystick.Horizontal, movementJoystick.Vertical);

            // Check of input sterk genoeg is en niet te snel herhaalt
            if (joystickInput.magnitude > inputThreshold &&
                Time.time - lastInputTime > inputCooldown &&
                HasInputChanged(joystickInput))
            {
                Vector2Int direction = GetGridDirection(joystickInput);

                if (direction != Vector2Int.zero)
                {
                    playerController.MoveToDirection(direction);
                    lastInputTime = Time.time;
                    lastInput = joystickInput;
                }
            }
        }
    }

    private bool HasInputChanged(Vector2 currentInput)
    {
        Vector2Int currentDirection = GetGridDirection(currentInput);
        Vector2Int lastDirection = GetGridDirection(lastInput);
        return currentDirection != lastDirection;
    }

    private Vector2Int GetGridDirection(Vector2 input)
    {
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            if (input.x > inputThreshold) return Vector2Int.right;
            if (input.x < -inputThreshold) return Vector2Int.left;
        }
        else
        {
            if (input.y > inputThreshold) return Vector2Int.up;
            if (input.y < -inputThreshold) return Vector2Int.down;
        }

        return Vector2Int.zero;
    }
}
