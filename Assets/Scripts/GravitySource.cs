using System.Collections;
using UnityEngine;

public class GravitySource : MonoBehaviour
{
    public enum Type
    {
        Spherical,
        Planar
    };

    [Header("Settings")]
    [SerializeField] private float gravityStrength = 25f;
    [SerializeField] private Type type = Type.Spherical;
    [SerializeField] private bool inverted = false;
    [SerializeField] private int priority = 0;

    private Collider zoneCollider;

    public int Priority => priority;
    public Vector3 Center => transform.position;
    public Collider ZoneCollider=> zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();

        if (!zoneCollider.isTrigger) zoneCollider.isTrigger = true;
    }

    public Vector3 GetGravity(Vector3 position)
    {
        Vector3 gravityDirection = Vector3.down;

        if (type == Type.Spherical)
        {
            gravityDirection = (transform.position - position).normalized;
        }
        else if (type == Type.Planar)
        {
            gravityDirection = -transform.up.normalized;
        }

        if (inverted)
            gravityDirection = -gravityDirection;

        return gravityDirection * gravityStrength;
    }

    public Vector3 GetUpAxis(Vector3 position)
    {
        Vector3 gravity = GetGravity(position);
        return -gravity.normalized;
    }

    public float GetPriorityDistance(Vector3 position)
    {
        return Vector3.Distance(position, transform.position);
    }
}