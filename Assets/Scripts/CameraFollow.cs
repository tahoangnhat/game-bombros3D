using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.75f, 0f);

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new Vector3(5f, 7f, -6f);
    [SerializeField] private float followSpeed = 10000f;
    [SerializeField] private float rotationSpeed = 10000f;
    [SerializeField] private bool rotateWithTarget = true;

    [Header("Orbit")]
    [SerializeField, Min(0f)] private float orbitSpeed = 120f;
    [SerializeField, Min(0.01f)] private float orbitSmoothTime = 0f;

    [Header("Zoom")]
    [SerializeField, Range(10f, 90f)] private float perspectiveFieldOfView = 25f;
    [SerializeField, Min(1f)] private float orthographicSize = 5f;
    [SerializeField, Min(0f)] private float zoomSpeed = 8f;

    private Camera followedCamera;
    private float orbitYaw;
    private float smoothedOrbitYaw;
    private float orbitSmoothVelocity;
    private bool orbitInitialized;

    public Transform Target => target;

    public static CameraFollow FollowLocalPlayer(Transform player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null || player == null)
        {
            return null;
        }

        CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
        }

        cameraFollow.SetTarget(player, true);
        return cameraFollow;
    }

    private void Awake()
    {
        followedCamera = GetComponent<Camera>();
    }

    public void SetTarget(Transform newTarget, bool snapImmediately = false)
    {
        target = newTarget;
        if (target != null && !orbitInitialized)
        {
            orbitYaw = rotateWithTarget ? target.eulerAngles.y : 0f;
            smoothedOrbitYaw = orbitYaw;
            orbitInitialized = true;
        }

        if (target == null || !snapImmediately)
        {
            return;
        }

        Vector3 focusPoint = target.position + targetOffset;
        transform.position = focusPoint + GetRotatedOffset();
        transform.rotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
        ApplyZoom(true);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateOrbitInput();

        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint + GetRotatedOffset();
        float positionBlend = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        float rotationBlend = 1f - Mathf.Exp(-rotationSpeed * Time.deltaTime);

        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);

        Quaternion desiredRotation = Quaternion.LookRotation(focusPoint - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);

        ApplyZoom(false);
    }

    private Vector3 GetRotatedOffset()
    {
        return Quaternion.Euler(0f, smoothedOrbitYaw, 0f) * offset;
    }

    private void UpdateOrbitInput()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        float direction = 0f;
        if (Keyboard.current.jKey.isPressed)
        {
            direction -= 1f;
        }
        if (Keyboard.current.kKey.isPressed)
        {
            direction += 1f;
        }

        orbitYaw += direction * orbitSpeed * Time.deltaTime;
        smoothedOrbitYaw = Mathf.SmoothDampAngle(
            smoothedOrbitYaw,
            orbitYaw,
            ref orbitSmoothVelocity,
            orbitSmoothTime,
            Mathf.Infinity,
            Time.deltaTime);
    }

    public static Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return new Vector3(input.x, 0f, input.y);
        }

        Vector3 forward = mainCamera.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = mainCamera.transform.right;
        right.y = 0f;
        right.Normalize();

        Vector3 direction = right * input.x + forward * input.y;
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }

    private void ApplyZoom(bool immediately)
    {
        if (followedCamera == null)
        {
            followedCamera = GetComponent<Camera>();
        }

        if (followedCamera == null)
        {
            return;
        }

        float blend = immediately ? 1f : 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        if (followedCamera.orthographic)
        {
            followedCamera.orthographicSize = Mathf.Lerp(
                followedCamera.orthographicSize,
                orthographicSize,
                blend);
        }
        else
        {
            followedCamera.fieldOfView = Mathf.Lerp(
                followedCamera.fieldOfView,
                perspectiveFieldOfView,
                blend);
        }
    }
}
