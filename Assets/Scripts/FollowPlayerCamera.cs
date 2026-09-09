using UnityEngine;

public sealed class FollowPlayerCamera : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -13f);
    private Transform target;

    private void Start()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            target = player.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = target.position + offset;
        transform.LookAt(target.position + Vector3.up * 0.5f);
    }
}
