using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ARDropEquippedObjectButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button dropButton;
    [SerializeField] private string dropButtonName = "DropButton";
    [SerializeField] private GameObject dropButtonContainer;

    [Header("Drop")]
    [SerializeField] private Transform playerCamera;
    // [SerializeField] private float dropDistance = 0.7f;
    // [SerializeField] private float dropVerticalOffset = -0.1f;

    [Header("Mount Drop")]
    [SerializeField] private string mountRootName = "wall_mount";
    [SerializeField] private string extinguisherMountName = "Apar_mount";
    [SerializeField] private string bucketMountName = "Bucket_mount";
    [SerializeField] private string bucketMountFallbackName = "Bucker_mount";
    [SerializeField] private string clothMountName = "Lap_mount";
    [SerializeField] private Transform extinguisherMountOffset;
    [SerializeField] private Transform bucketMountOffset;
    [SerializeField] private Transform clothMountOffset;
    [SerializeField] private bool applyExtinguisherMountedScale = false;
    [SerializeField] private Vector3 extinguisherMountedScale = Vector3.one;
    [SerializeField] private bool applyBucketMountedScale = false;
    [SerializeField] private Vector3 bucketMountedScale = Vector3.one;
    [SerializeField] private bool applyClothMountedScale = false;
    [SerializeField] private Vector3 clothMountedScale = Vector3.one;

    private CanvasGroup dropButtonCanvasGroup;

    private void Awake()
    {
        ResolveReferences();

        if (dropButton != null)
        {
            dropButton.onClick.AddListener(DropEquippedObject);
        }
    }

    private void OnDestroy()
    {
        if (dropButton != null)
        {
            dropButton.onClick.RemoveListener(DropEquippedObject);
        }
    }

    private void Update()
    {
        if (dropButton == null || playerCamera == null)
        {
            ResolveReferences();
        }

        if (dropButton != null)
        {
            bool hasEquippedObject = ARSingleEquipSlot.HasEquippedObject || GetFallbackEquippedObject() != null;
            SetDropButtonVisible(hasEquippedObject);
            dropButton.interactable = hasEquippedObject;
        }
    }

    private void ResolveReferences()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main != null ? Camera.main.transform : null;
        }

        if (dropButton == null)
        {
            GameObject dropButtonObject = FindSceneObjectByNameIncludingInactive(dropButtonName);
            if (dropButtonObject != null)
            {
                dropButton = dropButtonObject.GetComponent<Button>();
            }
        }

        if (dropButtonContainer == null && dropButton != null)
        {
            dropButtonContainer = dropButton.gameObject;
        }

        if (dropButtonCanvasGroup == null && dropButton != null)
        {
            dropButtonCanvasGroup = dropButton.gameObject.GetComponent<CanvasGroup>();
            if (dropButtonCanvasGroup == null)
            {
                dropButtonCanvasGroup = dropButton.gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public void DropEquippedObject()
    {
        GameObject equippedObject = ARSingleEquipSlot.EquippedObject;
        if (equippedObject == null)
        {
            equippedObject = GetFallbackEquippedObject();
            if (equippedObject == null)
            {
                return;
            }

            ARSingleEquipSlot.TryEquip(equippedObject);
        }

        if (equippedObject == null)
        {
            return;
        }

        if (!TryConsumeAndRespawnOnMount(equippedObject))
        {
            Debug.LogWarning("[ARDropEquippedObjectButton] Unable to respawn equipped object on mount.", this);
        }
    }

    private bool TryConsumeAndRespawnOnMount(GameObject equippedObject)
    {
        if (equippedObject == null)
        {
            return false;
        }

        if (equippedObject.GetComponentInChildren<ARWaterBucket>(true) != null)
        {
            if (!ConsumeEquipped(equippedObject))
            {
                return false;
            }

            ARBucketSpawner spawner = FindFirstObjectByType<ARBucketSpawner>();
            return spawner != null && spawner.RespawnOnMount(true);
        }

        if (equippedObject.GetComponentInChildren<ARCloth>(true) != null)
        {
            if (!ConsumeEquipped(equippedObject))
            {
                return false;
            }

            ARClothSpawner spawner = FindFirstObjectByType<ARClothSpawner>();
            return spawner != null && spawner.RespawnOnMount(true);
        }

        if (equippedObject.GetComponentInChildren<ARFireExtinguisher>(true) != null)
        {
            if (!ConsumeEquipped(equippedObject))
            {
                return false;
            }

            ARExtinguisherSpawner spawner = FindFirstObjectByType<ARExtinguisherSpawner>();
            return spawner != null && spawner.RespawnOnMount(true);
        }

        return false;
    }

    private bool ConsumeEquipped(GameObject equippedObject)
    {
        if (equippedObject == null)
        {
            return false;
        }

        ARSingleEquipSlot.Release(equippedObject);
        Destroy(equippedObject);
        return true;
    }

    private void SetDropButtonVisible(bool isVisible)
    {
        if (dropButton == null)
        {
            return;
        }

        // If this script lives on the same object as the button, avoid SetActive(false)
        // because it would disable this script too.
        if (dropButtonContainer != null && dropButtonContainer != gameObject)
        {
            dropButtonContainer.SetActive(isVisible);
            return;
        }

        dropButton.gameObject.SetActive(true);

        if (dropButtonCanvasGroup != null)
        {
            dropButtonCanvasGroup.alpha = isVisible ? 1f : 0f;
            dropButtonCanvasGroup.interactable = isVisible;
            dropButtonCanvasGroup.blocksRaycasts = isVisible;
        }
    }

    private GameObject GetFallbackEquippedObject()
    {
        ARFireExtinguisher extinguisher = FindFirstObjectByType<ARFireExtinguisher>();
        if (extinguisher != null && extinguisher.IsEquipped)
        {
            return extinguisher.gameObject;
        }

        ARWaterBucket bucket = FindFirstObjectByType<ARWaterBucket>();
        if (bucket != null && bucket.IsEquipped)
        {
            return bucket.gameObject;
        }

        return null;
    }

    private IARDroppable ResolveDroppable(GameObject equippedObject)
    {
        if (equippedObject == null)
        {
            return null;
        }

        IARDroppable onSelf = equippedObject.GetComponent<IARDroppable>();
        if (onSelf != null)
        {
            return onSelf;
        }

        IARDroppable onChildren = equippedObject.GetComponentInChildren<IARDroppable>(true);
        if (onChildren != null)
        {
            return onChildren;
        }

        return equippedObject.GetComponentInParent<IARDroppable>();
    }

    private bool TryReturnToMount(GameObject equippedObject)
    {
        if (equippedObject == null)
        {
            return false;
        }

        Transform mount = null;
        Transform offset = null;
        Vector3 targetScale = Vector3.one;

        bool isExtinguisher = equippedObject.GetComponentInChildren<ARFireExtinguisher>(true) != null;
        bool isBucket = !isExtinguisher && equippedObject.GetComponentInChildren<ARWaterBucket>(true) != null;
        bool isCloth = !isExtinguisher && !isBucket && equippedObject.GetComponentInChildren<ARCloth>(true) != null;

        if (isExtinguisher)
        {
            mount = ResolveMountTransform(extinguisherMountName, null);
            offset = extinguisherMountOffset;
            targetScale = extinguisherMountedScale;
        }
        else if (isBucket)
        {
            mount = ResolveMountTransform(bucketMountName, bucketMountFallbackName);
            offset = bucketMountOffset;
            targetScale = bucketMountedScale;
        }
        else if (isCloth)
        {
            mount = ResolveMountTransform(clothMountName, null);
            offset = clothMountOffset;
            targetScale = clothMountedScale;
        }
        else
        {
            return false;
        }

        if (mount == null)
        {
            return false;
        }

        Vector3 targetPosition = mount.position;
        Quaternion targetRotation = mount.rotation;
        if (offset != null)
        {
            targetPosition = mount.TransformPoint(offset.localPosition);
            targetRotation = mount.rotation * offset.localRotation;
        }

        IARDroppable droppable = ResolveDroppable(equippedObject);
        if (droppable != null)
        {
            droppable.DropTo(targetPosition, targetRotation);
        }
        else
        {
            equippedObject.transform.SetParent(null, true);
            equippedObject.transform.SetPositionAndRotation(targetPosition, targetRotation);
            ARSingleEquipSlot.Release(equippedObject);
        }

        equippedObject.transform.SetParent(mount, true);
        if (offset != null)
        {
            equippedObject.transform.localPosition = offset.localPosition;
            equippedObject.transform.localRotation = offset.localRotation;
        }
        else
        {
            equippedObject.transform.localPosition = Vector3.zero;
            equippedObject.transform.localRotation = Quaternion.identity;
        }

        if ((isExtinguisher && applyExtinguisherMountedScale)
            || (isBucket && applyBucketMountedScale)
            || (isCloth && applyClothMountedScale))
        {
            equippedObject.transform.localScale = targetScale;
        }
        return true;
    }

    private Transform ResolveMountTransform(string childName, string fallbackName)
    {
        GameObject root = FindSceneObjectByNameIncludingInactive(mountRootName);
        if (root == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(childName))
        {
            return root.transform;
        }

        GameObject child = SearchHierarchyForName(root, childName);
        if (child == null && !string.IsNullOrEmpty(fallbackName))
        {
            child = SearchHierarchyForName(root, fallbackName);
        }

        return child != null ? child.transform : root.transform;
    }

    private GameObject SearchHierarchyForName(GameObject obj, string targetName)
    {
        if (obj == null)
        {
            return null;
        }

        if (IsNameMatch(obj.name, targetName))
        {
            return obj;
        }

        foreach (Transform child in obj.transform)
        {
            GameObject found = SearchHierarchyForName(child.gameObject, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private bool IsNameMatch(string actualName, string targetName)
    {
        if (string.IsNullOrEmpty(actualName) || string.IsNullOrEmpty(targetName))
        {
            return false;
        }

        if (actualName == targetName)
        {
            return true;
        }

        if (actualName.StartsWith(targetName))
        {
            return true;
        }

        return actualName.Contains(targetName);
    }

    private GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Transform found = FindChildByNameRecursive(roots[r].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }
        }

        return null;
    }

    private Transform FindChildByNameRecursive(Transform root, string objectName)
    {
        if (root.name == objectName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildByNameRecursive(child, objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
