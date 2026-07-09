using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Top-Down Camera")]
    [SerializeField] private bool lockToTopDown = true;
    [SerializeField] private Vector3 fixedPosition = new Vector3(7.5f, 36f, -6.5f);
    [SerializeField] private Vector3 fixedEulerAngles = new Vector3(70f, 0f, 0f);
    [SerializeField, Min(1f)] private float orthographicSize = 7f;

    private Camera followedCamera;

    public Transform Target => null;

    public static CameraFollow FollowLocalPlayer(Transform player)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return null;
        }

        CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();
        }

        cameraFollow.ApplyTopDownView();
        return cameraFollow;
    }

    private void Awake()
    {
        followedCamera = GetComponent<Camera>();
        ApplyTopDownView();
    }

    private void LateUpdate()
    {
        if (lockToTopDown)
        {
            ApplyTopDownView();
        }
    }

    private void ApplyTopDownView()
    {
        transform.SetPositionAndRotation(
            fixedPosition,
            Quaternion.Euler(fixedEulerAngles));

        if (followedCamera == null)
        {
            followedCamera = GetComponent<Camera>();
        }

        if (followedCamera == null)
        {
            return;
        }

        followedCamera.orthographic = true;
        followedCamera.orthographicSize = orthographicSize;
    }

    public static Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        return direction.sqrMagnitude > 1f ? direction.normalized : direction;
    }
}
