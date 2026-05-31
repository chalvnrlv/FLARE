using UnityEngine;

public class ARWaterBucket : MonoBehaviour, IARDroppable
{
    [Header("Equip")]
    [SerializeField] private Vector3 equippedLocalPosition = new Vector3(-0.09f, -0.05f, 0.41f);
    [SerializeField] private Vector3 equippedLocalEuler = new Vector3(0f, -66.88f, 90f);

    [Header("Trigger Detection")]
    [SerializeField] private bool ensureBucketCollider = true;
    [SerializeField] private float fallbackColliderRadius = 0.15f;

    [Header("Pour")]
    [SerializeField] private Animator bucketAnimator;
    [SerializeField] private string pourTriggerName = "PourTrigger";
    [SerializeField] private float defaultPourDuration = 150f;

    private bool isEquipped;
    private bool isPouring;
    private Transform equipAnchor;
    private float lastDropTime = -999f;
    private float pourEndTime = -1f;

    public bool IsEquipped => isEquipped;
    public bool IsPouring => isPouring;
    public float LastDropTime => lastDropTime;
    public float DefaultPourDuration => defaultPourDuration;

    private void Awake()
    {
        ResolveReferences();
        EnsureColliderForTrigger();
    }

    private void Update()
    {
        if (isPouring && Time.time >= pourEndTime)
        {
            isPouring = false;
        }
    }

    private void Reset()
    {
        ResolveReferences();
        EnsureColliderForTrigger();
    }

    private void ResolveReferences()
    {
        if (bucketAnimator == null)
        {
            bucketAnimator = GetComponentInChildren<Animator>(true);
        }
    }

    private void EnsureColliderForTrigger()
    {
        if (!ensureBucketCollider)
        {
            return;
        }

        Collider existingCollider = GetComponentInChildren<Collider>(true);
        if (existingCollider != null)
        {
            return;
        }

        SphereCollider fallback = gameObject.AddComponent<SphereCollider>();
        fallback.radius = fallbackColliderRadius;
        fallback.isTrigger = false;
    }

    public void EquipTo(Transform anchor)
    {
        if (anchor == null)
        {
            return;
        }

        isEquipped = true;
        equipAnchor = anchor;

        transform.SetParent(equipAnchor, false);
        transform.localPosition = equippedLocalPosition;
        transform.localRotation = Quaternion.Euler(equippedLocalEuler);
    }

    public void DropTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        CancelPour();

        isEquipped = false;
        equipAnchor = null;
        lastDropTime = Time.time;

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        ARSingleEquipSlot.Release(gameObject);
    }

    public bool BeginPour(float duration)
    {
        if (!isEquipped || isPouring)
        {
            return false;
        }

        ResolveReferences();
        if (bucketAnimator != null && !string.IsNullOrEmpty(pourTriggerName))
        {
            bucketAnimator.SetTrigger(pourTriggerName);
        }

        float validDuration = duration > 0f ? duration : defaultPourDuration;
        pourEndTime = Time.time + validDuration;
        isPouring = true;
        return true;
    }

    public void CancelPour()
    {
        isPouring = false;
        pourEndTime = -1f;
    }

    public bool ConsumeForPour()
    {
        if (!isEquipped)
        {
            return false;
        }

        CancelPour();
        isEquipped = false;
        equipAnchor = null;
        ARSingleEquipSlot.Release(gameObject);
        gameObject.SetActive(false);
        return true;
    }
}
