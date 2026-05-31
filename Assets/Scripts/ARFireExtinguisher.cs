using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class ARFireExtinguisher : MonoBehaviour, IARDroppable
{
    [Header("Runtime References")]
    [SerializeField] private ParticleSystem sprayParticle;
    [SerializeField] private Transform nozzleTransform;
    [SerializeField] private Transform sprayOrigin;
    [SerializeField] private Transform equipAnchor;
    [SerializeField] private string sprayOriginName = "SprayPoint";

    [Header("Equip Pose")]
    [SerializeField] private Vector3 equippedLocalPosition = new Vector3(-0.09f, -0.05f, 0.41f);
    [SerializeField] private Vector3 equippedLocalEuler = new Vector3(0f, -66.88f, 90f);

    [Header("UI")]
    [SerializeField] private GameObject extinguisherUiRoot;
    [SerializeField] private Button pullPinButton;
    [SerializeField] private ARHoldButton fireHoldButton;
    [SerializeField] private Button typeLeftButton;
    [SerializeField] private Button typeRightButton;
    [SerializeField] private GameObject progressUi;
    [SerializeField] private Image capacityFillImage;
    [SerializeField] private Text capacityText;
    [SerializeField] private Text extinguisherTypeText;
    [SerializeField] private string uiRootName = "ExtinguisherUIRoot";
    [SerializeField] private string pullPinButtonName = "PullPinButton";
    [SerializeField] private string fireButtonName = "FireButton";
    [SerializeField] private string typeLeftButtonName = "TypeLeftButton";
    [SerializeField] private string typeRightButtonName = "TypeRightButton";
    [SerializeField] private string extinguisherTypeTextName = "ExtinguisherTypeText";
    [SerializeField] private string progressUiName = "ProgressUI";
    [SerializeField] private string capacityRingName = "CapacityRing";
    [SerializeField] private string capacityTextName = "CapacityText";

    [Header("Capacity")]
    [SerializeField] private float maxCapacity = 100f;
    [SerializeField] private float depletionPerSecond = 20f;

    [Header("Audio")]
    [SerializeField] private AudioSource sprayAudioSource;
    [SerializeField] private AudioClip sprayLoopClip;
    [SerializeField] private float sprayLoopVolume = 1f;

    [Header("Spray Collision")]
    [SerializeField] private bool enableSprayCollision = true;
    [SerializeField] private LayerMask sprayCollisionLayers = ~0;
    [SerializeField] private string preferredSprayCollisionLayerName = "FireHitbox";
    [SerializeField] private bool forceUsePreferredSprayLayer = true;

    [Header("Spray Hitbox Damage")]
    [SerializeField] private bool enableHitboxDamage = true;
    [SerializeField] private LayerMask fireHitboxLayers = ~0;
    [SerializeField] private string fireHitboxLayerName = "FireHitbox";
    [SerializeField] private float sprayHitRadius = 0.2f;
    [SerializeField] private float sprayHitDistance = 2.5f;

    [Header("Extinguisher Type")]
    [SerializeField] private Renderer labelRenderer;
    [SerializeField] private Material[] labelMaterials;
    [SerializeField] private string[] labelTypeTexts = new string[]
    {
        "AIR",
        "CO2",
        "FOAM",
        "WET CHEMICAL",
        "DRY POWDER"
    };
    [SerializeField] private int currentTypeIndex;

    [Header("LPG Fail State")]
    [SerializeField] private string lpgRootName = "Burning_LPG";
    [SerializeField] private string waterFailMessage = "Api melebar karena anda menggunakan alat pemadam bermedia air!";

    private float currentCapacity;
    private bool isEquipped;
    private bool isPinPulled;
    private bool wantsToSpray;
    private bool listenersBound;
    private float lastDropTime = -999f;
    private bool sprayLocalPoseCached;
    private Vector3 sprayLocalPosition;
    private Quaternion sprayLocalRotation;
    private Vector3 sprayLocalScale;
    private readonly RaycastHit[] sprayHitBuffer = new RaycastHit[24];
    private readonly HashSet<FireHealthTarget> damagedTargets = new HashSet<FireHealthTarget>();

    public bool IsEquipped => isEquipped;
    public float LastDropTime => lastDropTime;
    public float DepletionPerSecond => depletionPerSecond;
    public bool IsActivelySpraying => isEquipped && isPinPulled && wantsToSpray && currentCapacity > 0f;
    public Vector3 SprayOriginPosition => sprayParticle != null ? sprayParticle.transform.position : transform.position;
    public Vector3 SprayForward => sprayParticle != null ? sprayParticle.transform.forward : transform.forward;

    private void Awake()
    {
        currentCapacity = maxCapacity;
        ResolveSprayCollisionLayerMask();
        ResolveFireHitboxLayerMask();
        ResolveSprayReferences();
        ResolveSprayAudio();
        ResolveUiReferences();
        BindUiListeners();
        ResolveLabelRenderer();
        ApplyTypeIndex(currentTypeIndex);

        // Always start with UI hidden at play-mode start.
        HideAllExtinguisherUi();

        if (sprayParticle != null)
        {
            sprayParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ConfigureSprayParticle();
        }

        RefreshUi();
    }

    private void OnDestroy()
    {
        UnbindUiListeners();
    }

    private void OnDisable()
    {
        wantsToSpray = false;
        StopSprayEffect();
        StopSprayAudio();
        HideAllExtinguisherUi();
    }

    private void Update()
    {
        if (sprayParticle == null || sprayOrigin == null)
        {
            ResolveSprayReferences();
            ConfigureSprayParticle();
        }

        PreserveSprayLocalPose();

        bool resolvedAnyUi = false;

        if ((extinguisherUiRoot == null || pullPinButton == null || fireHoldButton == null || progressUi == null || capacityFillImage == null || capacityText == null))
        {
            ResolveUiReferences();
            BindUiListeners();
            resolvedAnyUi = true;
        }

        if (resolvedAnyUi)
        {
            RefreshUi();
        }

        if (!isEquipped || !isPinPulled || !wantsToSpray)
        {
            StopSprayEffect();
            return;
        }

        if (currentCapacity <= 0f)
        {
            StopSprayEffect();
            return;
        }

        currentCapacity = Mathf.Max(0f, currentCapacity - depletionPerSecond * Time.deltaTime);
        ApplyHitboxDamage(Time.deltaTime);
        RefreshUi();

        if (!sprayParticle.isPlaying)
        {
            sprayParticle.Play(true);
        }
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

        ResolveSprayReferences();
        PreserveSprayLocalPose();
        ResolveLabelRenderer();
        ApplyTypeIndex(currentTypeIndex);

        RefreshUi();
    }

    public void DropTo(Vector3 worldPosition, Quaternion worldRotation)
    {
        StopSpray();

        isEquipped = false;
        isPinPulled = false;
        equipAnchor = null;
        lastDropTime = Time.time;

        transform.SetParent(null, true);
        transform.SetPositionAndRotation(worldPosition, worldRotation);

        ARSingleEquipSlot.Release(gameObject);
        HideAllExtinguisherUi();
        RefreshUi();
    }

    private void ResolveSprayReferences()
    {
        if (sprayParticle == null)
        {
            sprayParticle = GetComponentInChildren<ParticleSystem>(true);
        }

        if (sprayParticle != null && sprayOrigin == null)
        {
            // Default to the particle object's own transform so prefab placement is preserved exactly.
            sprayOrigin = sprayParticle.transform;
        }

        if (sprayOrigin == null && nozzleTransform != null)
        {
            sprayOrigin = nozzleTransform;
        }

        if (sprayOrigin == null)
        {
            Transform direct = transform.Find(sprayOriginName);
            if (direct != null)
            {
                sprayOrigin = direct;
            }
            else
            {
                Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < allTransforms.Length; i++)
                {
                    if (allTransforms[i].name == sprayOriginName)
                    {
                        sprayOrigin = allTransforms[i];
                        break;
                    }
                }
            }
        }

        if (sprayParticle == null || sprayOrigin == null)
        {
            return;
        }

        CacheSprayLocalPose();
    }

    private void ResolveLabelRenderer()
    {
        if (labelRenderer != null)
        {
            return;
        }

        labelRenderer = GetComponentInChildren<Renderer>(true);
    }

    private void SelectNextType()
    {
        ApplyTypeIndex(currentTypeIndex + 1);
    }

    private void SelectPreviousType()
    {
        // Debug.Log($"[ARFireExtinguisher] SelectPreviousType called. Current index: {currentTypeIndex}");
        ApplyTypeIndex(currentTypeIndex - 1);
    }

    private void ApplyTypeIndex(int newIndex)
    {
        int typeCount = labelTypeTexts != null ? labelTypeTexts.Length : 0;
        if (typeCount <= 0)
        {
            return;
        }

        int normalized = newIndex % typeCount;
        if (normalized < 0)
        {
            normalized += typeCount;
        }

        currentTypeIndex = normalized;

        if (extinguisherTypeText != null)
        {
            extinguisherTypeText.text = labelTypeTexts[currentTypeIndex];
        }

        if (labelRenderer != null && labelMaterials != null && currentTypeIndex < labelMaterials.Length)
        {
            Material selected = labelMaterials[currentTypeIndex];
            if (selected != null)
            {
                labelRenderer.material = selected;
            }
        }
    }

    private void CacheSprayLocalPose()
    {
        if (sprayParticle == null)
        {
            return;
        }

        sprayLocalPosition = sprayParticle.transform.localPosition;
        sprayLocalRotation = sprayParticle.transform.localRotation;
        sprayLocalScale = sprayParticle.transform.localScale;
        sprayLocalPoseCached = true;
    }

    private void PreserveSprayLocalPose()
    {
        if (!sprayLocalPoseCached || sprayParticle == null)
        {
            return;
        }

        if (sprayParticle.transform.localPosition != sprayLocalPosition)
        {
            sprayParticle.transform.localPosition = sprayLocalPosition;
        }

        if (sprayParticle.transform.localRotation != sprayLocalRotation)
        {
            sprayParticle.transform.localRotation = sprayLocalRotation;
        }

        if (sprayParticle.transform.localScale != sprayLocalScale)
        {
            sprayParticle.transform.localScale = sprayLocalScale;
        }
    }

    public void PullPin()
    {
        if (!isEquipped || isPinPulled)
        {
            return;
        }

        isPinPulled = true;
        RefreshUi();
    }

    public void StartSpray()
    {
        wantsToSpray = true;
        StartSprayAudio();
    }

    public void StopSpray()
    {
        wantsToSpray = false;
        StopSprayEffect();
        StopSprayAudio();
    }

    private void ResolveSprayAudio()
    {
        if (sprayAudioSource == null)
        {
            sprayAudioSource = GetComponent<AudioSource>();
        }

        if (sprayAudioSource == null)
        {
            sprayAudioSource = GetComponentInChildren<AudioSource>(true);
        }

        if (sprayAudioSource == null)
        {
            return;
        }

        if (sprayLoopClip != null)
        {
            sprayAudioSource.clip = sprayLoopClip;
        }

        sprayAudioSource.loop = true;
        sprayAudioSource.playOnAwake = false;
        sprayAudioSource.volume = Mathf.Max(0f, sprayLoopVolume);
    }

    private void StartSprayAudio()
    {
        if (sprayAudioSource == null || sprayAudioSource.clip == null)
        {
            return;
        }

        if (!sprayAudioSource.isPlaying)
        {
            sprayAudioSource.volume = Mathf.Max(0f, sprayLoopVolume);
            sprayAudioSource.Play();
        }
    }

    private void StopSprayAudio()
    {
        if (sprayAudioSource == null)
        {
            return;
        }

        if (sprayAudioSource.isPlaying)
        {
            sprayAudioSource.Stop();
        }
    }

    private void StopSprayEffect()
    {
        if (sprayParticle != null && sprayParticle.isPlaying)
        {
            sprayParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ConfigureSprayParticle()
    {
        if (sprayParticle == null)
        {
            return;
        }

        ResolveSprayCollisionLayerMask();

        // Hitbox damage does not need particle collision and disabling it prevents bounce/deflection artifacts.
        if (enableHitboxDamage)
        {
            enableSprayCollision = false;
        }

        var main = sprayParticle.main;
        main.playOnAwake = false;
        main.prewarm = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var collisionModule = sprayParticle.collision;
        collisionModule.enabled = enableSprayCollision;
        collisionModule.type = ParticleSystemCollisionType.World;
        collisionModule.mode = ParticleSystemCollisionMode.Collision3D;
        collisionModule.collidesWith = sprayCollisionLayers;
        collisionModule.bounceMultiplier = 0f;
        collisionModule.dampenMultiplier = 0f;
        collisionModule.lifetimeLossMultiplier = 1f;
        collisionModule.minKillSpeed = 0f;
        collisionModule.maxKillSpeed = float.MaxValue;
        collisionModule.sendCollisionMessages = enableSprayCollision;
    }

    private void ApplyHitboxDamage(float deltaTime)
    {
        if (!enableHitboxDamage || deltaTime <= 0f)
        {
            return;
        }

        ResolveFireHitboxLayerMask();

        Vector3 origin = SprayOriginPosition;
        Vector3 direction = SprayForward.normalized;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = transform.forward;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            Mathf.Max(0.01f, sprayHitRadius),
            direction,
            sprayHitBuffer,
            Mathf.Max(0.1f, sprayHitDistance),
            fireHitboxLayers,
            QueryTriggerInteraction.Collide);

        if (hitCount <= 0)
        {
            return;
        }

        damagedTargets.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = sprayHitBuffer[i].collider;
            if (hitCollider == null)
            {
                continue;
            }

            FireHealthTarget target = hitCollider.GetComponentInParent<FireHealthTarget>();
            if (target == null || damagedTargets.Contains(target))
            {
                continue;
            }

            ARPcElectricalController pcController = FindFirstObjectByType<ARPcElectricalController>();
            if (pcController != null && pcController.IsPcFireTarget())
            {
                if (!pcController.IsPowerOff)
                {
                    pcController.ShowMustTurnOffMessage();
                    return;
                }

                if (IsLiquidType(currentTypeIndex))
                {
                    TriggerPcShockFail();
                    StopSpray();
                    return;
                }
            }

            if (currentTypeIndex == 0 && IsLpgFireTarget(target))
            {
                TriggerFailState();
                StopSpray();
                return;
            }

            target.ApplyDamageFromHitbox(depletionPerSecond, deltaTime);
            damagedTargets.Add(target);
        }
    }

    private bool IsLpgFireTarget(FireHealthTarget target)
    {
        if (target == null)
        {
            return false;
        }

        string lpgLower = lpgRootName.ToLowerInvariant();
        Transform current = target.transform;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.name) && current.name.ToLowerInvariant().Contains(lpgLower))
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
            timer.TriggerFailWithMessage("Penggunaan pemadam jenis cair membuat anda tersetrum!");
        }
    }

    private bool IsLiquidType(int typeIndex)
    {
        return typeIndex == 0 || typeIndex == 2 || typeIndex == 3;
    }

    private void ResolveSprayCollisionLayerMask()
    {
        if (string.IsNullOrEmpty(preferredSprayCollisionLayerName))
        {
            return;
        }

        int preferredLayer = LayerMask.NameToLayer(preferredSprayCollisionLayerName);
        if (preferredLayer < 0)
        {
            return;
        }

        if (!forceUsePreferredSprayLayer && sprayCollisionLayers.value != -1)
        {
            return;
        }

        sprayCollisionLayers = 1 << preferredLayer;
    }

    private void ResolveFireHitboxLayerMask()
    {
        if (fireHitboxLayers.value != -1)
        {
            return;
        }

        if (string.IsNullOrEmpty(fireHitboxLayerName))
        {
            return;
        }

        int hitboxLayer = LayerMask.NameToLayer(fireHitboxLayerName);
        if (hitboxLayer < 0)
        {
            return;
        }

        fireHitboxLayers = 1 << hitboxLayer;
    }

    private void ResolveUiReferences()
    {
        if (extinguisherUiRoot == null)
        {
            GameObject rootObject = FindSceneObjectByNameIncludingInactive(uiRootName);
            if (rootObject != null)
            {
                extinguisherUiRoot = rootObject;
            }
        }

        if (pullPinButton == null)
        {
            GameObject pullPinObject = FindSceneObjectByNameIncludingInactive(pullPinButtonName);
            if (pullPinObject != null)
            {
                pullPinButton = pullPinObject.GetComponent<Button>();
            }
        }

        if (fireHoldButton == null)
        {
            GameObject fireButtonObject = FindSceneObjectByNameIncludingInactive(fireButtonName);
            if (fireButtonObject != null)
            {
                fireHoldButton = fireButtonObject.GetComponent<ARHoldButton>();
            }
        }

        if (typeLeftButton == null)
        {
            GameObject leftButtonObject = FindSceneObjectByNameIncludingInactive(typeLeftButtonName);
            if (leftButtonObject != null)
            {
                typeLeftButton = leftButtonObject.GetComponent<Button>();
                Debug.Log($"[ARFireExtinguisher] Found left button: {leftButtonObject.name}, Button component: {typeLeftButton != null}");
            }
            else
            {
                Debug.LogWarning($"[ARFireExtinguisher] Could not find left button object with name: {typeLeftButtonName}");
            }
        }

        if (typeRightButton == null)
        {
            GameObject rightButtonObject = FindSceneObjectByNameIncludingInactive(typeRightButtonName);
            if (rightButtonObject != null)
            {
                typeRightButton = rightButtonObject.GetComponent<Button>();
            }
        }

        if (capacityFillImage == null)
        {
            GameObject ringObject = FindSceneObjectByNameIncludingInactive(capacityRingName);
            if (ringObject != null)
            {
                capacityFillImage = ringObject.GetComponent<Image>();
            }
        }

        if (progressUi == null)
        {
            GameObject progressObject = FindSceneObjectByNameIncludingInactive(progressUiName);
            if (progressObject != null)
            {
                progressUi = progressObject;
            }
        }

        if (capacityText == null)
        {
            GameObject textObject = FindSceneObjectByNameIncludingInactive(capacityTextName);
            if (textObject != null)
            {
                capacityText = textObject.GetComponent<Text>();
            }
        }

        if (extinguisherTypeText == null)
        {
            GameObject typeTextObject = FindSceneObjectByNameIncludingInactive(extinguisherTypeTextName);
            if (typeTextObject != null)
            {
                extinguisherTypeText = typeTextObject.GetComponent<Text>();
            }
        }
    }

    private GameObject FindSceneObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        // Fast path for active objects.
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

    private void BindUiListeners()
    {
        if (listenersBound)
        {
            return;
        }

        if (pullPinButton != null)
        {
            pullPinButton.onClick.AddListener(PullPin);
        }

        if (fireHoldButton != null)
        {
            fireHoldButton.AddHoldStartListener(StartSpray);
            fireHoldButton.AddHoldEndListener(StopSpray);
        }

        if (typeLeftButton != null)
        {
            typeLeftButton.onClick.AddListener(SelectPreviousType);
            Debug.Log($"[ARFireExtinguisher] Left button listener bound successfully");
        }
        else
        {
            Debug.LogWarning("[ARFireExtinguisher] typeLeftButton is null, cannot bind listener");
        }

        if (typeRightButton != null)
        {
            typeRightButton.onClick.AddListener(SelectNextType);
            Debug.Log($"[ARFireExtinguisher] Right button listener bound successfully");
        }
        else
        {
            Debug.LogWarning("[ARFireExtinguisher] typeRightButton is null, cannot bind listener");
        }

        listenersBound = pullPinButton != null || fireHoldButton != null || typeLeftButton != null || typeRightButton != null;
    }

    private void UnbindUiListeners()
    {
        if (pullPinButton != null)
        {
            pullPinButton.onClick.RemoveListener(PullPin);
        }

        if (fireHoldButton != null)
        {
            fireHoldButton.RemoveHoldStartListener(StartSpray);
            fireHoldButton.RemoveHoldEndListener(StopSpray);
        }

        if (typeLeftButton != null)
        {
            typeLeftButton.onClick.RemoveListener(SelectPreviousType);
        }

        if (typeRightButton != null)
        {
            typeRightButton.onClick.RemoveListener(SelectNextType);
        }

        listenersBound = false;
    }

    private void RefreshUi()
    {
        if (extinguisherUiRoot != null)
        {
            extinguisherUiRoot.SetActive(isEquipped);
        }

        if (pullPinButton != null)
        {
            pullPinButton.gameObject.SetActive(isEquipped && !isPinPulled);
        }

        if (typeLeftButton != null)
        {
            typeLeftButton.gameObject.SetActive(isEquipped && !isPinPulled);
        }

        if (typeRightButton != null)
        {
            typeRightButton.gameObject.SetActive(isEquipped && !isPinPulled);
        }

        if (extinguisherTypeText != null)
        {
            extinguisherTypeText.gameObject.SetActive(isEquipped && !isPinPulled);
        }

        if (fireHoldButton != null)
        {
            fireHoldButton.gameObject.SetActive(isEquipped && isPinPulled && currentCapacity > 0f);
        }

        if (progressUi != null)
        {
            progressUi.SetActive(isEquipped && isPinPulled && currentCapacity > 0f);
        }

        if (capacityFillImage != null)
        {
            float normalized = maxCapacity <= 0f ? 0f : currentCapacity / maxCapacity;
            capacityFillImage.fillAmount = normalized;
        }

        if (capacityText != null)
        {
            int percentage = maxCapacity <= 0f ? 0 : Mathf.RoundToInt((currentCapacity / maxCapacity) * 100f);
            capacityText.text = percentage + "%";
        }

        ApplyTypeIndex(currentTypeIndex);
    }

    private void HideAllExtinguisherUi()
    {
        if (extinguisherUiRoot != null)
        {
            extinguisherUiRoot.SetActive(false);
        }

        if (pullPinButton != null)
        {
            pullPinButton.gameObject.SetActive(false);
        }

        if (typeLeftButton != null)
        {
            typeLeftButton.gameObject.SetActive(false);
        }

        if (typeRightButton != null)
        {
            typeRightButton.gameObject.SetActive(false);
        }

        if (fireHoldButton != null)
        {
            fireHoldButton.gameObject.SetActive(false);
        }

        if (progressUi != null)
        {
            progressUi.SetActive(false);
        }
    }
}
