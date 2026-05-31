using UnityEngine;
using System.Collections.Generic;

public class ARClothAreaTrigger : MonoBehaviour
{
    [SerializeField] private bool requireClothEquipped = true;
    [SerializeField] private Collider areaCollider;

    private readonly HashSet<ARCloth> clothsInside = new HashSet<ARCloth>();

    public bool IsClothInside
    {
        get
        {
            if (clothsInside.Count == 0)
            {
                return false;
            }

            foreach (ARCloth cloth in clothsInside)
            {
                if (cloth == null)
                {
                    continue;
                }

                if (!requireClothEquipped || cloth.IsEquipped)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Awake()
    {
        EnsureTriggerRigidbody();
    }

    private void Reset()
    {
        EnsureTriggerRigidbody();
    }

    public bool IsPositionInsideArea(Vector3 worldPosition)
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<Collider>();
        }

        if (areaCollider == null)
        {
            return false;
        }

        Vector3 closest = areaCollider.ClosestPoint(worldPosition);
        float sqrDistance = (closest - worldPosition).sqrMagnitude;
        return sqrDistance < 0.0001f;
    }

    private void OnTriggerEnter(Collider other)
    {
        ARCloth cloth = ResolveCloth(other);
        if (cloth == null)
        {
            return;
        }

        clothsInside.Add(cloth);
    }

    private void OnTriggerExit(Collider other)
    {
        ARCloth cloth = ResolveCloth(other);
        if (cloth == null)
        {
            return;
        }

        clothsInside.Remove(cloth);
    }

    private ARCloth ResolveCloth(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        ARCloth cloth = other.GetComponentInParent<ARCloth>();
        if (cloth == null)
        {
            return null;
        }

        return cloth;
    }

    private void EnsureTriggerRigidbody()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (areaCollider == null)
        {
            areaCollider = triggerCollider;
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
