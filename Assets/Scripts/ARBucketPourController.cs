using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ARBucketPourController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARWaterBucket bucket;
    [SerializeField] private ARBurningAreaTrigger burningArea;
    [SerializeField] private FireHealthTarget fireTarget;

    [Header("UI")]
    [SerializeField] private GameObject bucketPourButtonObject;
    [SerializeField] private Button bucketPourButton;
    [SerializeField] private string bucketPourButtonName = "BucketPourButton";
    [SerializeField] private string bucketPourButtonFallbackName = "BucketPourWater";

    [Header("Animated Pour")]
    [SerializeField] private GameObject animatedBucketPrefab;
    [SerializeField] private Transform pourPoint;
    [SerializeField] private string burningObjectName = "TrashCan_Burning";
    [SerializeField] private string pourPointName = "PourPoint";
    [SerializeField] private string animatedPourTriggerName = "PourTrigger";

    [Header("Fire Drain")]
    [SerializeField] private float fireStartHealth = 50f;
    [SerializeField] private float drainDuration = 150f;

    [Header("LPG Fail State")]
    [SerializeField] private string lpgRootName = "Burning_LPG";
    [SerializeField] private string waterFailMessage = "Api melebar karena anda menggunakan alat pemadam bermedia air!";

    [Header("PC Electric Fail State")]
    [SerializeField] private string pcShockMessage = "Penggunaan pemadam jenis cair membuat anda tersetrum!";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float debugLogInterval = 0.4f;

    private float nextDebugLogTime;
    private bool isPourSequenceRunning;
    private Coroutine drainRoutine;
    private GameObject activeAnimatedBucketInstance;

    private void Awake()
    {
        ResolveReferences();
        BindButton();
        SetPourButtonVisible(false);
    }

    private void OnDestroy()
    {
        if (bucketPourButton != null)
        {
            bucketPourButton.onClick.RemoveListener(OnBucketPourButtonClicked);
        }

        CleanupAnimatedBucketInstance();
    }

    private void Update()
    {
        if (bucket == null || burningArea == null || fireTarget == null || bucketPourButtonObject == null || bucketPourButton == null)
        {
            ResolveReferences();
            BindButton();
        }

        ARWaterBucket equippedBucket = ResolveEquippedBucket();
        bool isEquipped = equippedBucket != null && equippedBucket.IsEquipped;

        bool isInsideZone = false;
        bool isInsideTrigger = false;
        bool isInsideBounds = false;
        if (burningArea != null && equippedBucket != null)
        {
            isInsideTrigger = burningArea.IsBucketInside;
            isInsideBounds = burningArea.IsPositionInsideArea(equippedBucket.transform.position);
            isInsideZone = isInsideTrigger || isInsideBounds;
        }

        bool shouldShowButton = isEquipped && isInsideZone && !isPourSequenceRunning;
        SetPourButtonVisible(shouldShowButton);

        if (bucketPourButton != null)
        {
            bucketPourButton.interactable = shouldShowButton;
        }

        if (enableDebugLogs && isEquipped && Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + Mathf.Max(0.05f, debugLogInterval);
            string bucketName = equippedBucket != null ? equippedBucket.name : "null";
            Debug.Log("[ARBucketPourController] bucket=" + bucketName +
                      " equipped=" + isEquipped +
                      " insideTrigger=" + isInsideTrigger +
                      " insideBounds=" + isInsideBounds +
                      " showButton=" + shouldShowButton,
                      this);
        }
    }

    private void ResolveReferences()
    {
        bucket = ResolveEquippedBucket();

        if (burningArea == null)
        {
            burningArea = FindFirstObjectByType<ARBurningAreaTrigger>();
        }

        if (fireTarget == null)
        {
            fireTarget = FindAnyFireTarget();
        }

        if (pourPoint == null)
        {
            pourPoint = ResolvePourPoint();
        }

        if (bucketPourButton == null)
        {
            if (bucketPourButtonObject == null)
            {
                bucketPourButtonObject = FindSceneObjectByNameIncludingInactive(bucketPourButtonName);
            }

            if (bucketPourButtonObject == null)
            {
                bucketPourButtonObject = FindSceneObjectByNameIncludingInactive(bucketPourButtonFallbackName);
            }

            if (bucketPourButtonObject != null)
            {
                bucketPourButton = bucketPourButtonObject.GetComponent<Button>();
            }
        }
    }

    private void BindButton()
    {
        if (bucketPourButton == null)
        {
            return;
        }

        bucketPourButton.onClick.RemoveListener(OnBucketPourButtonClicked);
        bucketPourButton.onClick.AddListener(OnBucketPourButtonClicked);
    }

    private void OnBucketPourButtonClicked()
    {
        if (isPourSequenceRunning)
        {
            return;
        }

        ARWaterBucket equippedBucket = ResolveEquippedBucket();
        bool canPour = equippedBucket != null && equippedBucket.IsEquipped && burningArea != null && (burningArea.IsBucketInside || burningArea.IsPositionInsideArea(equippedBucket.transform.position));
        if (!canPour)
        {
            return;
        }

        ARPcElectricalController pcController = FindFirstObjectByType<ARPcElectricalController>();
        if (pcController != null && pcController.IsPcFireTarget())
        {
            if (!pcController.IsPowerOff)
            {
                pcController.ShowMustTurnOffMessage();
                return;
            }

            TriggerPcShockFail();
            return;
        }

        if (!equippedBucket.ConsumeForPour())
        {
            return;
        }

        if (IsLpgFireTarget())
        {
            TriggerFailState();
            return;
        }

        Transform resolvedPourPoint = pourPoint != null ? pourPoint : ResolvePourPoint();
        if (resolvedPourPoint != null)
        {
            pourPoint = resolvedPourPoint;
        }

        if (animatedBucketPrefab != null && resolvedPourPoint != null)
        {
            CleanupAnimatedBucketInstance();
            GameObject animatedInstance = Instantiate(animatedBucketPrefab, resolvedPourPoint.position, resolvedPourPoint.rotation, resolvedPourPoint);
            PrepareAnimatedBucketInstance(animatedInstance);
            activeAnimatedBucketInstance = animatedInstance;
        }
        else if (animatedBucketPrefab != null && fireTarget != null)
        {
            Transform fallbackParent = ResolveFallbackParent();
            Vector3 spawnPosition = fallbackParent != null ? fallbackParent.position : fireTarget.transform.position;
            Quaternion spawnRotation = fallbackParent != null ? fallbackParent.rotation : Quaternion.identity;

            if (enableDebugLogs)
            {
                Debug.LogWarning("[ARBucketPourController] PourPoint not found. Spawning animated bucket with fallback parent.", this);
            }

            CleanupAnimatedBucketInstance();
            GameObject animatedInstance = fallbackParent != null
                ? Instantiate(animatedBucketPrefab, spawnPosition, spawnRotation, fallbackParent)
                : Instantiate(animatedBucketPrefab, spawnPosition, spawnRotation);
            PrepareAnimatedBucketInstance(animatedInstance);
            activeAnimatedBucketInstance = animatedInstance;
        }

        float duration = Mathf.Max(0.1f, drainDuration);
        float dps = Mathf.Max(0.01f, fireStartHealth / duration);

        isPourSequenceRunning = true;
        if (drainRoutine != null)
        {
            StopCoroutine(drainRoutine);
        }

        drainRoutine = StartCoroutine(DrainFireRoutine(dps, duration));
    }

    private bool IsLpgFireTarget()
    {
        if (fireTarget == null)
        {
            return false;
        }

        Transform current = fireTarget.transform;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.name) && current.name.ToLowerInvariant().Contains(lpgRootName.ToLowerInvariant()))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void TriggerFailState()
    {
        ARSpawnCountdownTimer timer = FindFirstObjectByType<ARSpawnCountdownTimer>();
        if (timer != null)
        {
            timer.TriggerFailWithMessage(waterFailMessage);
        }
    }

    private void TriggerPcShockFail()
    {
        ARSpawnCountdownTimer timer = FindFirstObjectByType<ARSpawnCountdownTimer>();
        if (timer != null)
        {
            timer.TriggerFailWithMessage(pcShockMessage);
        }
    }

    private IEnumerator DrainFireRoutine(float dps, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (fireTarget == null)
            {
                fireTarget = FindAnyFireTarget();
                if (fireTarget == null)
                {
                    break;
                }
            }

            if (fireTarget.IsExtinguished)
            {
                break;
            }

            float delta = Time.deltaTime;
            fireTarget.ApplyDamageFromHitbox(dps, delta);
            elapsed += delta;
            yield return null;
        }

        if (fireTarget != null)
        {
            fireTarget.ExtinguishNow();
        }

        CleanupAnimatedBucketInstance();

        isPourSequenceRunning = false;
        drainRoutine = null;
    }

    private ARWaterBucket ResolveEquippedBucket()
    {
        GameObject equippedObject = ARSingleEquipSlot.EquippedObject;
        if (equippedObject != null)
        {
            ARWaterBucket equippedBucket = equippedObject.GetComponent<ARWaterBucket>();
            if (equippedBucket != null)
            {
                return equippedBucket;
            }

            equippedBucket = equippedObject.GetComponentInChildren<ARWaterBucket>(true);
            if (equippedBucket != null)
            {
                return equippedBucket;
            }
        }

        if (bucket != null && bucket.IsEquipped)
        {
            return bucket;
        }

        return FindFirstObjectByType<ARWaterBucket>();
    }

    private void SetPourButtonVisible(bool isVisible)
    {
        if (bucketPourButtonObject != null)
        {
            bucketPourButtonObject.SetActive(isVisible);
        }
    }

    private Transform ResolvePourPoint()
    {
        GameObject burningObject = FindSceneObjectByNameIncludingInactive(burningObjectName);
        if (burningObject != null)
        {
            Transform root = burningObject.transform;
            if (NameEquals(root.name, pourPointName))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindChildByNameRecursive(child, pourPointName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        if (burningArea != null)
        {
            Transform foundInArea = FindChildByNameRecursive(burningArea.transform, pourPointName);
            if (foundInArea != null)
            {
                return foundInArea;
            }
        }

        GameObject globalPourPoint = FindSceneObjectByNameIncludingInactive(pourPointName);
        if (globalPourPoint != null)
        {
            return globalPourPoint.transform;
        }

        return null;
    }

    private Transform ResolveFallbackParent()
    {
        if (burningArea != null)
        {
            return burningArea.transform;
        }

        if (fireTarget != null)
        {
            return fireTarget.transform.parent;
        }

        return null;
    }

    private void PrepareAnimatedBucketInstance(GameObject animatedInstance)
    {
        if (animatedInstance == null)
        {
            return;
        }

        Animator animatedAnimator = animatedInstance.GetComponentInChildren<Animator>(true);
        if (animatedAnimator != null && !string.IsNullOrEmpty(animatedPourTriggerName))
        {
            animatedAnimator.SetTrigger(animatedPourTriggerName);
        }

        ARBucketPickup bucketPickup = animatedInstance.GetComponentInChildren<ARBucketPickup>(true);
        if (bucketPickup != null)
        {
            bucketPickup.enabled = false;
        }

        ARExtinguisherPickup extinguisherPickup = animatedInstance.GetComponentInChildren<ARExtinguisherPickup>(true);
        if (extinguisherPickup != null)
        {
            extinguisherPickup.enabled = false;
        }

        ARWaterBucket waterBucket = animatedInstance.GetComponentInChildren<ARWaterBucket>(true);
        if (waterBucket != null)
        {
            waterBucket.enabled = false;
        }

        ARFireExtinguisher fireExtinguisher = animatedInstance.GetComponentInChildren<ARFireExtinguisher>(true);
        if (fireExtinguisher != null)
        {
            fireExtinguisher.enabled = false;
        }
    }

    private void CleanupAnimatedBucketInstance()
    {
        if (activeAnimatedBucketInstance == null)
        {
            return;
        }

        Destroy(activeAnimatedBucketInstance);
        activeAnimatedBucketInstance = null;
    }

    private GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
        {
            return activeObject;
        }

        int sceneCount = SceneManager.sceneCount;
        for (int sceneIndex = 0; sceneIndex < sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindChildByNameRecursive(roots[i].transform, objectName);
                if (found != null)
                {
                    return found.gameObject;
                }
            }
        }

        return null;
    }

    private FireHealthTarget FindAnyFireTarget()
    {
        FireHealthTarget[] targets = FindObjectsByType<FireHealthTarget>(FindObjectsSortMode.None);
        if (targets == null || targets.Length == 0)
        {
            return null;
        }

        return targets[0];
    }

    private Transform FindChildByNameRecursive(Transform root, string objectName)
    {
        if (NameEquals(root.name, objectName))
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

    private static bool NameEquals(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
