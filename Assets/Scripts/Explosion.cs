using UnityEngine;

public class Explosion : MonoBehaviour
{
    public float lifeTime = 0.35f;

    void Start()
    {
        Destroy(gameObject, lifeTime);
    }
}
