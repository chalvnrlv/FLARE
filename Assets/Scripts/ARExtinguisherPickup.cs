using UnityEngine;

public class ARExtinguisherPickup : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Transform equipAnchor;
    [SerializeField] private ARFireExtinguisher extinguisher;
    [SerializeField] private float equipDistance = 0.8f;
    [SerializeField] private float pickupCooldownAfterDrop = 0.8f;
    [SerializeField] private string handAnchorName = "HandAnchor";

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main != null ? Camera.main.transform : null;
        }

        if (extinguisher == null)
        {
            extinguisher = GetComponent<ARFireExtinguisher>();
        }

        if (equipAnchor == null && playerCamera != null)
        {
            Transform anchorFromCamera = playerCamera.Find(handAnchorName);
            if (anchorFromCamera != null)
            {
                equipAnchor = anchorFromCamera;
            }
        }

        if (equipAnchor == null)
        {
            GameObject anchorObject = GameObject.Find(handAnchorName);
            if (anchorObject != null)
            {
                equipAnchor = anchorObject.transform;
            }
        }
    }

    private void Update()
    {
        if (playerCamera == null || equipAnchor == null || extinguisher == null)
        {
            ResolveReferences();
        }

        if (playerCamera == null || equipAnchor == null || extinguisher == null)
        {
            return;
        }

        if (extinguisher.IsEquipped)
        {
            return;
        }

        if (Time.time - extinguisher.LastDropTime < pickupCooldownAfterDrop)
        {
            return;
        }

        float distance = Vector3.Distance(playerCamera.position, transform.position);
        if (distance > equipDistance)
        {
            return;
        }

        if (!ARSingleEquipSlot.TryEquip(extinguisher.gameObject))
        {
            return;
        }

        extinguisher.EquipTo(equipAnchor);
    }
}
