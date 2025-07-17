using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class ContinuousPlayerInputHandler : MonoBehaviour
{
    [Header("References")]
    public GridPlayerController playerController;

    [Header("Input Actions")]
    public InputActionReference moveInputAction;

    [Header("Continuous Movement Settings")]
    public float initialDelay = 0.5f;      // Tijd voordat herhaalde beweging start
    public float repeatRate = 0.15f;       // Tijd tussen herhaalde bewegingen
    public bool enableContinuousMovement = true;

    [Header("Mobile Settings")]
    public float mobileRepeatRate = 0.2f;  // Iets langzamer voor mobile

    private Vector2 currentMoveInput;
    private bool isInputActive = false;
    private Coroutine continuousMovementCoroutine;

    void OnEnable()
    {
        if (moveInputAction != null)
        {
            moveInputAction.action.Enable();
            moveInputAction.action.performed += OnMovePerformed;
            moveInputAction.action.canceled += OnMoveCanceled;
        }
    }

    void OnDisable()
    {
        if (moveInputAction != null)
        {
            moveInputAction.action.performed -= OnMovePerformed;
            moveInputAction.action.canceled -= OnMoveCanceled;
            moveInputAction.action.Disable();
        }

        StopContinuousMovement();
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        currentMoveInput = context.ReadValue<Vector2>();

        if (!isInputActive && enableContinuousMovement)
        {
            isInputActive = true;

            // Directe eerste beweging
            ProcessMovement();

            // Start continuous movement na initial delay
            if (continuousMovementCoroutine == null)
            {
                continuousMovementCoroutine = StartCoroutine(ContinuousMovement());
            }
        }
        else if (!enableContinuousMovement)
        {
            // Discrete movement (oude systeem)
            ProcessMovement();
        }
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        isInputActive = false;
        currentMoveInput = Vector2.zero;
        StopContinuousMovement();
    }

    private void ProcessMovement()
    {
        Vector2Int direction = GetGridDirection(currentMoveInput);

        if (direction != Vector2Int.zero && playerController != null)
        {
            playerController.MoveToDirection(direction);
        }
    }

    private IEnumerator ContinuousMovement()
    {
        // Wacht voor initial delay
        yield return new WaitForSeconds(initialDelay);

        // Bepaal repeat rate based op platform
        float currentRepeatRate = Application.isMobilePlatform ? mobileRepeatRate : repeatRate;

        while (isInputActive && enableContinuousMovement)
        {
            ProcessMovement();
            yield return new WaitForSeconds(currentRepeatRate);
        }

        continuousMovementCoroutine = null;
    }

    private void StopContinuousMovement()
    {
        if (continuousMovementCoroutine != null)
        {
            StopCoroutine(continuousMovementCoroutine);
            continuousMovementCoroutine = null;
        }
    }

    private Vector2Int GetGridDirection(Vector2 input)
    {
        float threshold = 0.5f;

        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
        {
            if (input.x > threshold) return Vector2Int.right;
            if (input.x < -threshold) return Vector2Int.left;
        }
        else
        {
            if (input.y > threshold) return Vector2Int.up;
            if (input.y < -threshold) return Vector2Int.down;
        }

        return Vector2Int.zero;
    }

    // Public methods voor UI toggles
    public void SetContinuousMovement(bool enabled)
    {
        enableContinuousMovement = enabled;
        if (!enabled)
        {
            StopContinuousMovement();
        }
    }

    public void SetRepeatRate(float rate)
    {
        repeatRate = Mathf.Clamp(rate, 0.05f, 1f);
    }
}
