using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ARClothController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARClothAreaTrigger clothTrigger;
    [SerializeField] private Button activateClothButton;
    [SerializeField] private GameObject activateClothButtonObject;
    [SerializeField] private string activateClothButtonName = "ClothButton";
    [SerializeField] private string activateClothButtonFallbackName = "ActivateClothButton";

    [Header("Cloth Detection")]
    [SerializeField] private string clothChildName = "Cloth_";
    [SerializeField] private LayerMask fireHitboxLayers = ~0;
    [SerializeField] private string fireHitboxLayerName = "FireHitbox";

    [Header("Fade Transition")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Damage")]
    [SerializeField] private float damagePerSecond = 20f;
    [SerializeField] private float damageApplicationDuration = 5f;

    [Header("Area Checks")]
    [SerializeField] private bool useTriggerCheck = false;
    [SerializeField] private bool useBoundsCheck = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float debugLogInterval = 0.4f;

    private ARCloth equippedCloth;
    private bool isClothActivationRunning;
    private bool listenersBound;
    private float nextDebugLogTime;

    private void Awake()
    {
        ResolveReferences();
        BindListeners();
        ResolveFireHitboxLayerMask();
        SetClothButtonVisible(false);
    }

    private void OnDestroy()
    {
        UnbindListeners();
    }

    private void Update()
    {
        if (clothTrigger == null || activateClothButtonObject == null || activateClothButton == null)
        {
            ResolveReferences();
            BindListeners();
        }

        UpdateClothEquipState();
        UpdateActivateButtonVisibility();
    }

    private void ResolveReferences()
    {
        if (clothTrigger == null)
        {
            clothTrigger = GetComponentInParent<ARClothAreaTrigger>();
            if (clothTrigger == null)
            {
                clothTrigger = FindFirstObjectByType<ARClothAreaTrigger>();
            }
        }

        if (activateClothButtonObject == null)
        {
            GameObject buttonObj = FindSceneObjectByNameIncludingInactive(activateClothButtonName);
            if (buttonObj == null && !string.IsNullOrEmpty(activateClothButtonFallbackName))
            {
                buttonObj = FindSceneObjectByNameIncludingInactive(activateClothButtonFallbackName);
            }
            if (buttonObj != null)
            {
                activateClothButtonObject = buttonObj;
                activateClothButton = buttonObj.GetComponent<Button>();
                Debug.Log($"[ARClothController] Found cloth button: {buttonObj.name}");
            }
            else
            {
                Debug.LogWarning($"[ARClothController] Could not find cloth button with name: {activateClothButtonName}");
            }
        }
        else if (activateClothButton == null)
        {
            activateClothButton = activateClothButtonObject.GetComponent<Button>();
        }
    }

    private void BindListeners()
    {
        if (listenersBound)
        {
            return;
        }

        if (activateClothButton != null)
        {
            activateClothButton.onClick.RemoveListener(OnActivateClothButtonClicked);
            activateClothButton.onClick.AddListener(OnActivateClothButtonClicked);
            Debug.Log("[ARClothController] Cloth button listener bound");
        }

        listenersBound = true;
    }

    private void UnbindListeners()
    {
        if (activateClothButton != null)
        {
            activateClothButton.onClick.RemoveListener(OnActivateClothButtonClicked);
        }

        listenersBound = false;
    }

    private void UpdateClothEquipState()
    {
        ARCloth newEquipped = null;
        GameObject equippedObject = ARSingleEquipSlot.EquippedObject;
        if (equippedObject != null)
        {
            newEquipped = equippedObject.GetComponentInChildren<ARCloth>(true);
        }

        if (newEquipped != equippedCloth)
        {
            equippedCloth = newEquipped;
        }
    }

    private void UpdateActivateButtonVisibility()
    {
        bool isInsideTrigger = false;
        bool isInsideBounds = false;
        bool isInsideZone = false;

        if (clothTrigger != null && equippedCloth != null && !IsTriggerAttachedToCloth())
        {
            if (useTriggerCheck)
            {
                isInsideTrigger = clothTrigger.IsClothInside;
            }

            if (useBoundsCheck)
            {
                isInsideBounds = clothTrigger.IsPositionInsideArea(equippedCloth.transform.position);
            }

            isInsideZone = isInsideTrigger || isInsideBounds;
        }

        bool shouldShowButton = equippedCloth != null
            && equippedCloth.IsEquipped
            && isInsideZone
            && !isClothActivationRunning;

        SetClothButtonVisible(shouldShowButton);

        if (activateClothButton != null)
        {
            activateClothButton.interactable = shouldShowButton;
        }

        if (enableDebugLogs && equippedCloth != null && equippedCloth.IsEquipped && Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + Mathf.Max(0.05f, debugLogInterval);
            string clothName = equippedCloth != null ? equippedCloth.name : "null";
            Debug.Log("[ARClothController] cloth=" + clothName +
                      " equipped=" + (equippedCloth != null && equippedCloth.IsEquipped) +
                      " insideTrigger=" + isInsideTrigger +
                      " insideBounds=" + isInsideBounds +
                      " showButton=" + shouldShowButton,
                      this);
        }
    }

    private void SetClothButtonVisible(bool isVisible)
    {
        if (activateClothButtonObject != null)
        {
            activateClothButtonObject.SetActive(isVisible);
        }
    }

    private void OnActivateClothButtonClicked()
    {
        if (equippedCloth == null || !equippedCloth.IsEquipped || isClothActivationRunning)
        {
            return;
        }

        if (!IsClothInsideZone())
        {
            return;
        }

        StartCoroutine(ActivateClothRoutine());
    }

    private IEnumerator ActivateClothRoutine()
    {
        isClothActivationRunning = true;

        ARPcElectricalController pcController = FindFirstObjectByType<ARPcElectricalController>();
        if (pcController != null && pcController.IsPcFireTarget() && !pcController.IsPowerOff)
        {
            pcController.ShowMustTurnOffMessage();
            isClothActivationRunning = false;
            yield break;
        }

        if (equippedCloth.ConsumeForActivation())
        {
            equippedCloth = null;
        }

        FireHealthTarget[] allFires = FindObjectsByType<FireHealthTarget>(FindObjectsSortMode.None);
        System.Collections.Generic.List<(CanvasGroup canvasGroup, FireHealthTarget target)> clothsToActivate =
            new System.Collections.Generic.List<(CanvasGroup, FireHealthTarget)>();

        foreach (FireHealthTarget fire in allFires)
        {
            if (fire == null)
            {
                continue;
            }

            Transform clothChild = FindClothChild(fire.transform);
            if (clothChild != null)
            {
                CanvasGroup canvasGroup = clothChild.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = clothChild.gameObject.AddComponent<CanvasGroup>();
                }

                clothsToActivate.Add((canvasGroup, fire));
                Debug.Log($"[ARClothController] Found cloth on: {fire.gameObject.name}");
            }
        }

        if (clothsToActivate.Count == 0)
        {
            Debug.LogWarning("[ARClothController] No cloth children found to activate");
            isClothActivationRunning = false;
            yield break;
        }

        foreach (var (canvasGroup, fire) in clothsToActivate)
        {
            canvasGroup.gameObject.SetActive(true);
            StartCoroutine(FadeInCloth(canvasGroup));
        }

        float elapsedTime = 0f;
        while (elapsedTime < damageApplicationDuration)
        {
            float deltaTime = Time.deltaTime;
            float dps = damagePerSecond;

            foreach (var (_, fire) in clothsToActivate)
            {
                if (fire != null)
                {
                    fire.ApplyDamageFromHitbox(dps, deltaTime);
                }
            }

            elapsedTime += deltaTime;
            yield return null;
        }

        isClothActivationRunning = false;
        Debug.Log("[ARClothController] Cloth activation complete");
    }

    private IEnumerator FadeInCloth(CanvasGroup canvasGroup)
    {
        if (canvasGroup == null)
        {
            yield break;
        }

        canvasGroup.alpha = 0f;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / fadeDuration);
            float curveValue = fadeCurve.Evaluate(t);
            canvasGroup.alpha = curveValue;
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private bool IsClothInsideZone()
    {
        if (equippedCloth == null || clothTrigger == null)
        {
            return false;
        }

        if (IsTriggerAttachedToCloth())
        {
            return false;
        }

        bool isInsideTrigger = useTriggerCheck && clothTrigger.IsClothInside;
        bool isInsideBounds = useBoundsCheck && clothTrigger.IsPositionInsideArea(equippedCloth.transform.position);
        return isInsideTrigger || isInsideBounds;
    }

    private bool IsTriggerAttachedToCloth()
    {
        return clothTrigger != null && equippedCloth != null && clothTrigger.transform.IsChildOf(equippedCloth.transform);
    }

    private Transform FindClothChild(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        Transform root = parent;
        while (root.parent != null)
        {
            root = root.parent;
        }

        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (allChildren[i].name.Contains(clothChildName))
            {
                return allChildren[i];
            }
        }

        return null;
    }

    private void ResolveFireHitboxLayerMask()
    {
        if (string.IsNullOrEmpty(fireHitboxLayerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(fireHitboxLayerName);
        if (layer >= 0)
        {
            fireHitboxLayers = 1 << layer;
        }
    }

    private GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Scene[] loadedScenes = new Scene[SceneManager.sceneCount];
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            loadedScenes[i] = SceneManager.GetSceneAt(i);
        }

        foreach (Scene scene in loadedScenes)
        {
            if (!scene.isLoaded)
            {
                continue;
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject root in rootObjects)
            {
                GameObject found = SearchHierarchyForName(root, objectName);
                if (found != null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private GameObject SearchHierarchyForName(GameObject obj, string targetName)
    {
        if (obj.name == targetName)
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
}
