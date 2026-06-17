using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;

public class ARTapToPlaceEvacuation : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARSpawnCountdownTimer timer;

    [Header("Prefabs")]
    [SerializeField] private GameObject fogPrefab;
    [SerializeField] private GameObject evacuationPathPrefab;

    [Header("UI")]
    [SerializeField] private string textDialogueName = "TextDialogue";
    [SerializeField] private Text textDialogue;
    [SerializeField] private string textContainerName = "TextContainer";
    [SerializeField] private GameObject textContainer;
    [SerializeField] private string scanMessage = "Gerakan ponsel anda di sekitar bidang datar untuk memulai simulasi!";
    [SerializeField] private string tapMessage = "Sentuh area yang terdeteksi untuk memulai simulasi!";
    [SerializeField] private string startMessage = "Ikuti jalur untuk segera Evakuasi!";
    [SerializeField] private float startMessageDuration = 2f;
    [SerializeField] private string fogWarningMessage = "Asap berbahaya, segera merunduk!";

    [Header("Overlay")]
    [SerializeField] private string overlayImageName = "HealthDamagerOverlay";
    [SerializeField] private Image overlayImage;
    [SerializeField] private float overlayPulseSpeed = 2f;
    [SerializeField] private float overlayMaxAlpha = 1f;

    [Header("Dial Pad")]
    [SerializeField] private DialPadController dialPad;
    [SerializeField] private string dialPadName = "DialPad";
    [SerializeField] private string correctDialNumber = "113";
    [SerializeField] private string callInstructionMessage = "Masukkan 113 untuk pemadam kebakaran";
    [SerializeField] private string callFailMessage = "Telepon pemadam kebakaran sekarang!";
    [SerializeField] private float callTimeoutSeconds = 5f;
    [SerializeField] private float wrongDialClearDelay = 0.6f;

    [Header("Audio Sirene")]
    [Tooltip("AudioSource untuk sirene. Jika kosong, dibuat atau ditemukan otomatis.")]
    [SerializeField] private AudioSource sireneAudioSource;
    [Tooltip("AudioClip sirene yang diputar looping saat nomor damkar benar dimasukkan.")]
    [SerializeField] private AudioClip sireneClip;

    [Header("Placement")]
    [SerializeField] private int requiredPlaneCount = 3;
    // [SerializeField] private float fogHeightFromPlane = 0.75f;
    // [SerializeField] private float fallbackPlaneYOffset = 0.02f;
    // [SerializeField] private float fogFixedY = 1.658079f;

    [Header("Fog Damage")]
    [SerializeField] private float insideFogFailSeconds = 5f;

    [Header("Evacuation Point")]
    [SerializeField] private string evacuationPointChildName = "EvacuationPoint";

    private bool hasPlaced;
    private GameObject spawnedFog;
    private GameObject spawnedPath;
    private Collider fogCollider;
    private Collider evacuationCollider;
    private float insideFogSeconds;
    private Coroutine startMessageRoutine;
    private bool waitingForCall;
    private float callTimer;
    private bool dialPadHooked;
    private bool callInstructionShown;
    private Coroutine dialErrorRoutine;

    private void Awake()
    {
        ResolveReferences();
        ResolveSireneAudioSource();
        PrepareTimer();
        SetOverlayVisible(false);
        UpdateDialogueMessage();
        SetPlaneVisualization(true);
    }

    private void OnEnable()
    {
        EnsureDialPadHooked();
    }

    private void OnDisable()
    {
        if (dialPad != null)
        {
            dialPad.CallPressed -= HandleDialCall;
        }

        dialPadHooked = false;
        StopSirene();
    }

    private void Update()
    {
        ResolveReferences();

        if (!dialPadHooked)
        {
            EnsureDialPadHooked();
        }

        if (!hasPlaced)
        {
            UpdateDialogueMessage();
            if (!CanTapToPlace())
            {
                return;
            }

            if (TryGetTapPosition(out Vector2 screenPosition))
            {
                TryPlaceFromTap(screenPosition);
            }

            return;
        }

        if (timer != null && timer.IsFinished)
        {
            SetOverlayVisible(false);
            return;
        }

        if (waitingForCall)
        {
            UpdateCallTimer();
            return;
        }

        UpdateFogDamage();
        CheckEvacuationReached();
    }

    private void ResolveReferences()
    {
        if (raycastManager == null)
        {
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
        }

        if (planeManager == null)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }

        if (timer == null)
        {
            timer = FindFirstObjectByType<ARSpawnCountdownTimer>();
        }

        if (textDialogue == null && !string.IsNullOrEmpty(textDialogueName))
        {
            GameObject textObject = FindSceneObjectByNameIncludingInactive(textDialogueName);
            if (textObject != null)
            {
                textDialogue = textObject.GetComponent<Text>();
            }
        }

        if (textContainer == null && !string.IsNullOrEmpty(textContainerName))
        {
            textContainer = FindSceneObjectByNameIncludingInactive(textContainerName);
        }

        if (overlayImage == null && !string.IsNullOrEmpty(overlayImageName))
        {
            GameObject overlayObject = FindSceneObjectByNameIncludingInactive(overlayImageName);
            if (overlayObject != null)
            {
                overlayImage = overlayObject.GetComponent<Image>();
            }
        }

        if (dialPad == null && !string.IsNullOrEmpty(dialPadName))
        {
            GameObject dialPadObject = FindSceneObjectByNameIncludingInactive(dialPadName);
            if (dialPadObject != null)
            {
                dialPad = dialPadObject.GetComponent<DialPadController>();
            }
        }
    }

    private void EnsureDialPadHooked()
    {
        if (dialPad == null || dialPadHooked)
        {
            return;
        }

        dialPad.CallPressed += HandleDialCall;
        dialPadHooked = true;
    }

    private void PrepareTimer()
    {
        if (timer == null)
        {
            return;
        }

        timer.SetUseSpawnedObjectsGate(false);
        timer.SetRequireManualStart(true);
    }

    private bool CanTapToPlace()
    {
        if (raycastManager == null || planeManager == null)
        {
            return false;
        }

        return planeManager.trackables.count >= requiredPlaneCount;
    }

    private void UpdateDialogueMessage()
    {
        if (textDialogue == null || planeManager == null)
        {
            return;
        }

        string nextMessage = planeManager.trackables.count >= requiredPlaneCount ? tapMessage : scanMessage;
        textDialogue.text = nextMessage;
        SetDialogueVisible(true);
    }

    private bool TryGetTapPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        return false;
    }

    private void TryPlaceFromTap(Vector2 screenPosition)
    {
        if (raycastManager == null)
        {
            return;
        }

        var hits = new List<ARRaycastHit>();
        if (!raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            return;
        }

        Pose hitPose = hits[0].pose;
        Vector3 basePosition = hitPose.position;

        if (fogPrefab != null)
        {
            Vector3 fogPosition = arCamera != null ? arCamera.transform.position : basePosition;
            spawnedFog = Instantiate(fogPrefab, fogPosition, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(spawnedFog, gameObject.scene);
            fogCollider = spawnedFog.GetComponentInChildren<Collider>(true);
        }

        if (evacuationPathPrefab != null)
        {
            Vector3 pathPosition = basePosition;
            if (arCamera != null)
            {
                pathPosition = new Vector3(arCamera.transform.position.x, basePosition.y, arCamera.transform.position.z);
            }

            Vector3 lookDirection = arCamera != null ? arCamera.transform.position - pathPosition : Vector3.forward;
            lookDirection.y = 0f;
            if (lookDirection.sqrMagnitude < 0.0001f)
            {
                lookDirection = Vector3.forward;
            }

            Quaternion pathRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            spawnedPath = Instantiate(evacuationPathPrefab, pathPosition, pathRotation);
            SceneManager.MoveGameObjectToScene(spawnedPath, gameObject.scene);
            evacuationCollider = ResolveEvacuationCollider(spawnedPath);
        }

        hasPlaced = true;
        ShowStartMessage();
        SetPlaneVisualization(false);

        if (timer != null)
        {
            timer.StartCountdownNow();
        }
    }

    private void SetPlaneVisualization(bool isVisible)
    {
        if (planeManager == null)
        {
            return;
        }

        planeManager.enabled = isVisible;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane != null)
            {
                plane.gameObject.SetActive(isVisible);
            }
        }
    }

    private void UpdateFogDamage()
    {
        if (fogCollider == null || arCamera == null)
        {
            SetOverlayVisible(false);
            insideFogSeconds = 0f;
            return;
        }

        bool isInsideFog = IsPointInsideCollider(arCamera.transform.position, fogCollider);
        if (isInsideFog)
        {
            insideFogSeconds += Time.deltaTime;
            ShowFogWarning();
            PulseOverlay();

            if (insideFogSeconds >= insideFogFailSeconds)
            {
                TriggerFogFail();
            }
        }
        else
        {
            insideFogSeconds = 0f;
            SetOverlayVisible(false);
        }
    }

    private void ShowFogWarning()
    {
        if (textDialogue == null)
        {
            return;
        }

        textDialogue.text = fogWarningMessage;
        SetDialogueVisible(true);
    }

    private void TriggerFogFail()
    {
        if (timer == null)
        {
            return;
        }

        timer.TriggerFailWithMessage(fogWarningMessage);
    }

    private void PulseOverlay()
    {
        if (overlayImage == null)
        {
            return;
        }

        float pulse = (Mathf.Sin(Time.time * overlayPulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Clamp01(pulse * overlayMaxAlpha);
        SetOverlayVisible(true);
        SetOverlayAlpha(alpha);
    }

    private void SetOverlayVisible(bool isVisible)
    {
        if (overlayImage == null)
        {
            return;
        }

        overlayImage.gameObject.SetActive(isVisible);
        if (!isVisible)
        {
            SetOverlayAlpha(0f);
        }
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlayImage == null)
        {
            return;
        }

        Color color = overlayImage.color;
        color.a = Mathf.Clamp01(alpha);
        overlayImage.color = color;
    }

    private void CheckEvacuationReached()
    {
        if (evacuationCollider == null || arCamera == null)
        {
            return;
        }

        if (IsPointInsideCollider(arCamera.transform.position, evacuationCollider))
        {
            Debug.Log("[ARTapToPlaceEvacuation] Evacuation point reached.", this);
            StartCallFlow();
        }
    }

    private void StartCallFlow()
    {
        if (waitingForCall)
        {
            return;
        }

        waitingForCall = true;
        callTimer = 0f;
        callInstructionShown = false;

        if (startMessageRoutine != null)
        {
            StopCoroutine(startMessageRoutine);
            startMessageRoutine = null;
        }

        if (dialPad != null)
        {
            dialPad.ResetInput();
            dialPad.ResetInputColor();
            dialPad.SetVisible(true);
        }

        ShowMessage(callFailMessage);
    }

    private void UpdateCallTimer()
    {
        callTimer += Time.deltaTime;
        if (callTimer < callTimeoutSeconds || callInstructionShown)
        {
            return;
        }

        callInstructionShown = true;
        ShowMessage(callInstructionMessage);
    }

    private void HandleDialCall(string dialedNumber)
    {
        if (!waitingForCall)
        {
            return;
        }

        if (string.Equals(dialedNumber, correctDialNumber, StringComparison.Ordinal))
        {
            CompleteCallSuccess();
        }
        else
        {
            StartDialErrorFeedback();
        }
    }

    private void StartDialErrorFeedback()
    {
        if (dialPad == null)
        {
            callInstructionShown = true;
            ShowMessage(callInstructionMessage);
            return;
        }

        dialPad.ShowErrorColor();
        if (dialErrorRoutine != null)
        {
            StopCoroutine(dialErrorRoutine);
        }

        dialErrorRoutine = StartCoroutine(ClearDialAfterDelay());
    }

    private IEnumerator ClearDialAfterDelay()
    {
        float waitSeconds = Mathf.Max(0f, wrongDialClearDelay);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        if (dialPad != null)
        {
            dialPad.ResetInput();
            dialPad.ResetInputColor();
        }

        callInstructionShown = true;
        ShowMessage(callInstructionMessage);
        dialErrorRoutine = null;
    }

    private void CompleteCallSuccess()
    {
        waitingForCall = false;
        SetDialPadVisible(false);
        PlaySirene();
        if (timer != null)
        {
            timer.TriggerWinWithMessage("Kerja Bagus !");
        }
    }

    private void SetDialPadVisible(bool isVisible)
    {
        if (dialPad != null)
        {
            dialPad.SetVisible(isVisible);
        }
    }

    private bool IsPointInsideCollider(Vector3 worldPosition, Collider collider)
    {
        if (collider == null)
        {
            return false;
        }

        Vector3 closest = collider.ClosestPoint(worldPosition);
        return (closest - worldPosition).sqrMagnitude < 0.0001f;
    }

    // ── Audio Sirene ───────────────────────────────────────────────

    private void ResolveSireneAudioSource()
    {
        if (sireneAudioSource != null)
        {
            return;
        }

        sireneAudioSource = GetComponent<AudioSource>();
        if (sireneAudioSource == null)
        {
            sireneAudioSource = gameObject.AddComponent<AudioSource>();
        }

        sireneAudioSource.playOnAwake = false;
        sireneAudioSource.loop = true;
    }

    private void PlaySirene()
    {
        if (sireneAudioSource == null || sireneClip == null)
        {
            return;
        }

        sireneAudioSource.clip = sireneClip;
        sireneAudioSource.loop = true;
        sireneAudioSource.Play();
    }

    private void StopSirene()
    {
        if (sireneAudioSource == null || !sireneAudioSource.isPlaying)
        {
            return;
        }

        sireneAudioSource.Stop();
    }

    private Collider ResolveEvacuationCollider(GameObject root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (!string.IsNullOrEmpty(allChildren[i].name) && allChildren[i].name.Contains(evacuationPointChildName))
            {
                Collider childCollider = allChildren[i].GetComponent<Collider>();
                if (childCollider != null)
                {
                    return childCollider;
                }
            }
        }

        return root.GetComponentInChildren<SphereCollider>(true);
    }

    private void ShowStartMessage()
    {
        if (textDialogue == null)
        {
            return;
        }

        if (startMessageRoutine != null)
        {
            StopCoroutine(startMessageRoutine);
        }

        textDialogue.text = startMessage;
        SetDialogueVisible(true);
        startMessageRoutine = StartCoroutine(HideStartMessageAfterDelay());
    }

    private void ShowMessage(string message)
    {
        if (textDialogue == null)
        {
            return;
        }

        textDialogue.text = message;
        SetDialogueVisible(true);
    }

    private IEnumerator HideStartMessageAfterDelay()
    {
        float waitSeconds = Mathf.Max(0f, startMessageDuration);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        if (textDialogue != null)
        {
            SetDialogueVisible(false);
        }

        startMessageRoutine = null;
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
                GameObject found = SearchHierarchyForName(roots[r], objectName);
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
        if (obj == null)
        {
            return null;
        }

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

    private void SetDialogueVisible(bool isVisible)
    {
        if (textContainer != null)
        {
            textContainer.SetActive(isVisible);
            if (textDialogue != null)
            {
                textDialogue.gameObject.SetActive(isVisible);
            }
            return;
        }

        if (textDialogue != null)
        {
            textDialogue.gameObject.SetActive(isVisible);
        }
    }
}
