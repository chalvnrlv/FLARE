using UnityEngine;

public class ARCloth : MonoBehaviour, IARDroppable
{
    [Header("Equip")]
    [SerializeField] private Vector3 equippedLocalPosition = new Vector3(-0.09f, -0.05f, 0.41f);
    [SerializeField] private Vector3 equippedLocalEuler = new Vector3(0f, -66.88f, 90f);

    [Header("Trigger Detection")]
    [SerializeField] private bool ensureClothCollider = false;
    [SerializeField] private float fallbackColliderRadius = 0.15f;

    private bool isEquipped;
    private bool isUsed;
    private Transform equipAnchor;
    private float lastDropTime = -999f;

    public bool IsEquipped => isEquipped;
    public bool IsUsed => isUsed;
    public float LastDropTime => lastDropTime;

    private void Awake()
    {
        EnsureColliderForTrigger();
    }

    private void Reset()
    {
        EnsureColliderForTrigger();
    }

    private void EnsureColliderForTrigger()
    {
        if (!ensureClothCollider)
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
        isUsed = false;
        equipAnchor = anchor;

        transform.SetParent(equipAnchor, false);
        transform.localPosition = equippedLocalPosition;
        transform.localRotation = Quaternion.Euler(equippedLocalEuler);
    }

    public void DropTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        isEquipped = false;
        equipAnchor = null;
        lastDropTime = Time.time;

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        ARSingleEquipSlot.Release(gameObject);
    }

    public bool ConsumeForActivation()
    {
        if (!isEquipped || isUsed)
        {
            return false;
        }

        isUsed = true;
        isEquipped = false;
        equipAnchor = null;
        ARSingleEquipSlot.Release(gameObject);
        gameObject.SetActive(false);
        return true;
    }
}
