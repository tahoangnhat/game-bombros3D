using UnityEngine;

public static class PlayerMovementUtility
{
    private static readonly Vector3[] FootprintOffsets =
    {
        Vector3.zero,
        new Vector3(0.22f, 0f, 0f),
        new Vector3(-0.22f, 0f, 0f),
        new Vector3(0f, 0f, 0.22f),
        new Vector3(0f, 0f, -0.22f)
    };

    public static bool TryMove(Transform transform, Collider bodyCollider, Vector3 moveStep)
    {
        if (moveStep.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        Vector3 target = transform.position + moveStep;
        if (CanOccupyPosition(transform, bodyCollider, target))
        {
            transform.position = target;
            return true;
        }

        Vector3 slideX = transform.position + new Vector3(moveStep.x, 0f, 0f);
        if (slideX != transform.position && CanOccupyPosition(transform, bodyCollider, slideX))
        {
            transform.position = slideX;
            return true;
        }

        Vector3 slideZ = transform.position + new Vector3(0f, 0f, moveStep.z);
        if (slideZ != transform.position && CanOccupyPosition(transform, bodyCollider, slideZ))
        {
            transform.position = slideZ;
            return true;
        }

        return false;
    }

    public static bool CanOccupyPosition(Transform transform, Collider bodyCollider, Vector3 position)
    {
        if (!PassesGridCollision(position))
        {
            return false;
        }

        return !HitsBlockingCollider(transform, bodyCollider, position);
    }

    private static bool PassesGridCollision(Vector3 position)
    {
        for (int i = 0; i < FootprintOffsets.Length; i++)
        {
            Vector3 sample = position + FootprintOffsets[i];
            GridUtility.TryWorldToCell(sample, out int cellX, out int cellZ);
            if (GridUtility.IsCellBlockingForMovement(cellX, cellZ))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HitsBlockingCollider(Transform transform, Collider bodyCollider, Vector3 targetPosition)
    {
        Vector3 delta = targetPosition - transform.position;
        if (delta.sqrMagnitude <= 0.000001f)
        {
            return false;
        }

        if (bodyCollider is CapsuleCollider capsule)
        {
            GetCapsulePoints(transform, capsule, targetPosition, out Vector3 pointA, out Vector3 pointB, out float radius);
            if (Physics.CapsuleCast(
                    pointA,
                    pointB,
                    radius,
                    delta.normalized,
                    out RaycastHit hit,
                    delta.magnitude,
                    ~0,
                    QueryTriggerInteraction.Ignore) && IsBlockingCollider(transform, hit.collider))
            {
                return true;
            }
        }

        float checkRadius = 0.28f;
        Collider[] overlaps = Physics.OverlapSphere(targetPosition, checkRadius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (IsBlockingCollider(transform, overlaps[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void GetCapsulePoints(
        Transform transform,
        CapsuleCollider capsule,
        Vector3 targetPosition,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius)
    {
        Vector3 worldCenter = targetPosition + transform.TransformVector(capsule.center);
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        radius = capsule.radius * scale;
        float height = Mathf.Max(capsule.height * transform.lossyScale.y, radius * 2f);
        float halfHeight = height * 0.5f - radius;

        pointA = worldCenter + Vector3.up * halfHeight;
        pointB = worldCenter - Vector3.up * halfHeight;
    }

    private static bool IsBlockingCollider(Transform self, Collider collider)
    {
        if (collider == null || collider.transform == self || collider.transform.IsChildOf(self))
        {
            return false;
        }

        if (collider.GetComponentInParent<OnlinePlayerController>() != null
            || collider.GetComponentInParent<PlayerController>() != null)
        {
            return false;
        }

        if (collider.GetComponentInParent<OnlineBomb>() != null
            || collider.GetComponentInParent<Bomb>() != null)
        {
            return false;
        }

        if (collider.GetComponentInParent<OnlineExplosion>() != null
            || collider.GetComponentInParent<Explosion>() != null)
        {
            return false;
        }

        return true;
    }
}
