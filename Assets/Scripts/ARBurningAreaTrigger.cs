using UnityEngine;
using System.Collections.Generic;

public class ARBurningAreaTrigger : MonoBehaviour
{
    [SerializeField] private ARWaterBucket trackedBucket;
    [SerializeField] private bool requireBucketEquipped = false;
    [SerializeField] private Collider areaCollider;

    private readonly HashSet<ARWaterBucket> bucketsInside = new HashSet<ARWaterBucket>();

    public bool IsBucketInside
    {
        get
        {
            if (bucketsInside.Count == 0)
            {
                return false;
            }

            foreach (ARWaterBucket bucket in bucketsInside)
            {
                if (bucket == null)
                {
                    continue;
                }

                if (!requireBucketEquipped || bucket.IsEquipped)
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
        ARWaterBucket bucket = ResolveBucket(other);
        if (bucket == null)
        {
            return;
        }

        bucketsInside.Add(bucket);
    }

    private void OnTriggerExit(Collider other)
    {
        ARWaterBucket bucket = ResolveBucket(other);
        if (bucket == null)
        {
            return;
        }

        bucketsInside.Remove(bucket);
    }

    private ARWaterBucket ResolveBucket(Collider other)
    {
        if (other == null)
        {
            return null;
        }

        ARWaterBucket bucket = other.GetComponentInParent<ARWaterBucket>();
        if (bucket == null)
        {
            return null;
        }

        if (trackedBucket != null && bucket != trackedBucket)
        {
            return null;
        }

        return bucket;
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
