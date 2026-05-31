using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

public class ARSpawnCountdownTimer : MonoBehaviour
{
    private static bool pendingRestartReset;

    [Header("Dependencies")]
    [SerializeField] private ARPlaceCube fireSourcePlacer;
    [SerializeField] private ARExtinguisherSpawner extinguisherSpawner;
    [SerializeField] private ARBucketSpawner bucketSpawner;
    [SerializeField] private FireHealthTarget fireTarget;

    [Header("UI")]
    [SerializeField] private GameObject timerRoot;
    [SerializeField] private Text timerText;
    [Header("End Dialog")]
    [SerializeField] private GameObject endDialogRoot;
    [SerializeField] private Text endStateText; // "EndState" object
    [SerializeField] private Text finishTimeText; // "FinishTime" object
    [SerializeField] private Button nextRetButton; // "NextRetButton"
    [SerializeField] private string winText = "API DIPADAMKAN";
    [SerializeField] private string loseText = "GAGAL MEMADAMKAN";
    [SerializeField] private string evacuationWinText = "BERHASIL EVAKUASI";
    [SerializeField] private string evacuationLoseText = "GAGAL EVAKUASI";
    [SerializeField] private string evacuationLevelName = "Simulasi_Ev";

    [Header("Fail Message")]
    [SerializeField] private string failMessageTextName = "TextDialogue";
    [SerializeField] private Text failMessageText;
    [SerializeField] private string failMessageContainerName = "TextContainer";
    [SerializeField] private GameObject failMessageContainer;
    [SerializeField] private float endDialogDelaySeconds = 2f;
    [SerializeField] private string successMessage = "Kerja Bagus !";
    [SerializeField] private float winMessageDelaySeconds = 2f;
    [SerializeField] private float winTipDelaySeconds = 2f;
    [SerializeField] private string tipSimulasiA = "Tips: Memadamkan benda padat paling efektif menggunakan media cair";
    [SerializeField] private string tipSimulasiB = "Tips: Hindari pemadaman menggunakan media cair";
    [SerializeField] private string tipSimulasiC = "Tips: Hindari pemadaman menggunakan media cair";
    [SerializeField] private string lpgRootName = "Burning_LPG";
    [SerializeField] private string overlayImageName = "HealthDamagerOverlay";
    [SerializeField] private Image overlayImage;
    [SerializeField] private float overlayFailAlpha = 0.7f;

    [Header("Countdown")]
    [SerializeField] private float countdownSeconds = 60f;
    [SerializeField] private float restartDelaySeconds = 0.35f;
    [SerializeField] private string retrySceneName = "Simulasi_A";
    [SerializeField] private string[] levelSequence = new string[] { "Simulasi_A", "Simulasi_B", "Simulasi_C", "Simulasi_Ev" };
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private bool useSpawnedObjectsGate = true;
    [SerializeField] private bool requireManualStart = false;
    [SerializeField] private bool enableRestartDebugLogs = true;
    [SerializeField] private bool logRootNamesOnRestart = true;
    [Header("XR Simulation")]
    [SerializeField] private bool loadSimulationSceneInEditor = true;
    [SerializeField] private string simulatedEnvironmentSceneName = "";

    private bool countdownStarted;
    private bool countdownFinished;
    private bool isWin;
    private float remainingSeconds;
    private bool hasLoggedPostReloadRoots;
    private Coroutine endDialogRoutine;

    public bool IsRunning => countdownStarted && !countdownFinished;
    public bool IsFinished => countdownFinished;
    public bool IsWin => countdownFinished && isWin;
    public float RemainingSeconds => remainingSeconds;

    public void SetCountdownSeconds(float seconds)
    {
        countdownSeconds = Mathf.Max(0f, seconds);
        remainingSeconds = Mathf.Max(0f, countdownSeconds);
        RefreshTimerLabel();
    }

    public void SetUseSpawnedObjectsGate(bool enabled)
    {
        useSpawnedObjectsGate = enabled;
    }

    public void SetRequireManualStart(bool required)
    {
        requireManualStart = required;
    }

    public void StartCountdownNow()
    {
        if (countdownStarted || countdownFinished)
        {
            return;
        }

        countdownStarted = true;
        SetTimerVisible(true);
        RefreshTimerLabel();
    }

    public void ResetForNewLevel()
    {
        countdownStarted = false;
        countdownFinished = false;
        isWin = false;
        remainingSeconds = Mathf.Max(0f, countdownSeconds);
        hasLoggedPostReloadRoots = false;

        SetTimerVisible(false);
        if (endDialogRoot != null)
        {
            endDialogRoot.SetActive(false);
        }

        HideFailMessage();
        HideFailOverlay();

        if (nextRetButton != null)
        {
            nextRetButton.interactable = true;
        }

        ARLpgValveController lpgController = FindFirstObjectByType<ARLpgValveController>();
        if (lpgController != null)
        {
            lpgController.ResetForNewLevel();
        }

        ARPcElectricalController pcController = FindFirstObjectByType<ARPcElectricalController>();
        if (pcController != null)
        {
            pcController.ResetForNewLevel();
        }

        ResolveReferences();
        RefreshTimerLabel();
    }

    private void Awake()
    {
        EnsureLevelSequenceContains("Simulasi_Ev");
        ResolveReferences();
        remainingSeconds = Mathf.Max(0f, countdownSeconds);
        SetTimerVisible(false);
        if (endDialogRoot != null)
        {
            endDialogRoot.SetActive(false);
        }
        HideFailMessage();
        RefreshTimerLabel();
    }

    private void EnsureLevelSequenceContains(string levelName)
    {
        if (string.IsNullOrEmpty(levelName))
        {
            return;
        }

        if (levelSequence == null)
        {
            levelSequence = new string[] { levelName };
            return;
        }

        for (int i = 0; i < levelSequence.Length; i++)
        {
            if (string.Equals(levelSequence[i], levelName))
            {
                return;
            }
        }

        string[] updated = new string[levelSequence.Length + 1];
        for (int i = 0; i < levelSequence.Length; i++)
        {
            updated[i] = levelSequence[i];
        }

        updated[updated.Length - 1] = levelName;
        levelSequence = updated;
    }

    private void Start()
    {
        if (pendingRestartReset)
        {
            pendingRestartReset = false;
            hasLoggedPostReloadRoots = false;
            StartCoroutine(PostReloadResetRoutine());
        }
    }

    private void Update()
    {
        if (!countdownStarted)
        {
            ResolveReferences();
            if (requireManualStart)
            {
                return;
            }

            if (useSpawnedObjectsGate && !AreAllObjectsSpawned())
            {
                return;
            }

            countdownStarted = true;
            SetTimerVisible(true);
            RefreshTimerLabel();
            return;
        }

        if (countdownFinished)
        {
            return;
        }

        if (IsFireExtinguished())
        {
            if (ShouldDelayWin())
            {
                // Keep countdown running until valve is turned.
            }
            else
            {
                TriggerWin();
                return;
            }
        }

        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        RefreshTimerLabel();

        if (remainingSeconds <= 0f)
        {
            if (IsFireExtinguished() && !ShouldDelayWin())
            {
                TriggerWin();
            }
            else
            {
                TriggerLose();
            }
        }
    }

    private bool AreAllObjectsSpawned()
    {
        bool fireReady = fireSourcePlacer != null && fireSourcePlacer.HasPlacedObject;
        bool extinguisherReady = extinguisherSpawner == null || extinguisherSpawner.HasSpawnedObject;
        bool bucketReady = bucketSpawner == null || bucketSpawner.HasSpawnedObject;
        return fireReady && extinguisherReady && bucketReady;
    }

    private void ResolveReferences()
    {
        if (fireSourcePlacer == null)
        {
            fireSourcePlacer = FindFirstObjectByType<ARPlaceCube>();
        }

        if (extinguisherSpawner == null)
        {
            extinguisherSpawner = FindFirstObjectByType<ARExtinguisherSpawner>();
        }

        if (bucketSpawner == null)
        {
            bucketSpawner = FindFirstObjectByType<ARBucketSpawner>();
        }

        if (fireTarget == null)
        {
            fireTarget = FindFirstObjectByType<FireHealthTarget>();
        }

        if (failMessageText == null && !string.IsNullOrEmpty(failMessageTextName))
        {
            GameObject messageObject = FindSceneObjectByNameIncludingInactive(failMessageTextName);
            if (messageObject != null)
            {
                failMessageText = messageObject.GetComponent<Text>();
            }
        }

        if (failMessageContainer == null && !string.IsNullOrEmpty(failMessageContainerName))
        {
            failMessageContainer = FindSceneObjectByNameIncludingInactive(failMessageContainerName);
        }

        if (overlayImage == null && !string.IsNullOrEmpty(overlayImageName))
        {
            GameObject overlayObject = FindSceneObjectByNameIncludingInactive(overlayImageName);
            if (overlayObject != null)
            {
                overlayImage = overlayObject.GetComponent<Image>();
            }
        }
    }

    private void SetTimerVisible(bool isVisible)
    {
        if (timerRoot != null)
        {
            timerRoot.SetActive(isVisible);
        }
    }

    private void RefreshTimerLabel()
    {
        if (timerText == null)
        {
            return;
        }

        int totalSeconds = Mathf.CeilToInt(remainingSeconds);
        timerText.text = totalSeconds.ToString("00");
    }

    private bool IsFireExtinguished()
    {
        if (fireTarget == null)
        {
            fireTarget = FindFirstObjectByType<FireHealthTarget>();
        }

        return fireTarget != null && fireTarget.IsExtinguished;
    }

    private void TriggerWin()
    {
        countdownFinished = true;
        isWin = true;
        RecordLevelCompletion();
        StartWinDialogFlow(successMessage);
    }

    private void TriggerLose()
    {
        countdownFinished = true;
        isWin = false;
        ShowFailOverlay();
        TriggerVibration();
        StartEndDialogFlow(false, string.Empty);
    }

    public void TriggerFailWithMessage(string message)
    {
        if (countdownFinished)
        {
            return;
        }

        countdownFinished = true;
        isWin = false;
        ShowFailOverlay();
        TriggerVibration();
        StartEndDialogFlow(false, message);
    }

    public void TriggerWinWithMessage(string message)
    {
        if (countdownFinished)
        {
            return;
        }

        countdownFinished = true;
        isWin = true;
        RecordLevelCompletion();
        HideFailOverlay();
        StartWinDialogFlow(message);
    }

    private bool ShouldDelayWin()
    {
        if (!IsLpgFireTarget())
        {
            return false;
        }

        ARLpgValveController lpgController = FindFirstObjectByType<ARLpgValveController>();
        return lpgController == null || !lpgController.IsValveCompleted;
    }

    private bool IsLpgFireTarget()
    {
        if (fireTarget == null)
        {
            fireTarget = FindFirstObjectByType<FireHealthTarget>();
        }

        if (fireTarget == null)
        {
            return false;
        }

        string lpgLower = lpgRootName.ToLowerInvariant();
        Transform current = fireTarget.transform;
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

    private void StartEndDialogFlow(bool won, string message)
    {
        if (endDialogRoutine != null)
        {
            StopCoroutine(endDialogRoutine);
        }

        if (!string.IsNullOrEmpty(message))
        {
            endDialogRoutine = StartCoroutine(ShowMessageThenEndDialog(won, message));
        }
        else
        {
            ShowEndDialog(won);
        }
    }

    private void StartWinDialogFlow(string message)
    {
        if (endDialogRoutine != null)
        {
            StopCoroutine(endDialogRoutine);
        }

        endDialogRoutine = StartCoroutine(ShowWinMessagesThenEndDialog(message));
    }

    private IEnumerator ShowWinMessagesThenEndDialog(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            ShowFailMessage(message);
            float waitSeconds = Mathf.Max(0f, winMessageDelaySeconds);
            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }
        }

        string tipMessage = ResolveLevelTipMessage();
        if (!string.IsNullOrEmpty(tipMessage))
        {
            ShowFailMessage(tipMessage);
            float waitSeconds = Mathf.Max(0f, winTipDelaySeconds);
            if (waitSeconds > 0f)
            {
                yield return new WaitForSeconds(waitSeconds);
            }
        }

        HideFailMessage();
        ShowEndDialog(true);
        endDialogRoutine = null;
    }

    private IEnumerator ShowMessageThenEndDialog(bool won, string message)
    {
        ShowFailMessage(message);

        float waitSeconds = Mathf.Max(0f, endDialogDelaySeconds);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        HideFailMessage();
        ShowEndDialog(won);
        endDialogRoutine = null;
    }

    private void SetResultVisible(bool showWin, bool showLose)
    {
        // legacy placeholder - not used
    }

    private void ShowEndDialog(bool won)
    {
        // hide timer UI
        SetTimerVisible(false);

        if (endDialogRoot != null)
        {
            endDialogRoot.SetActive(true);
        }

        if (endStateText != null)
        {
            bool isEvacuationLevel = string.Equals(ResolveCurrentLevelName(), evacuationLevelName);
            if (isEvacuationLevel)
            {
                endStateText.text = won ? evacuationWinText : evacuationLoseText;
            }
            else
            {
                endStateText.text = won ? winText : loseText;
            }
        }

        if (finishTimeText != null)
        {
            int totalSeconds = Mathf.CeilToInt(remainingSeconds);
            finishTimeText.text = totalSeconds.ToString("00");
        }

        if (nextRetButton != null)
        {
            Text childText = nextRetButton.GetComponentInChildren<Text>(true);
            if (childText != null)
            {
                childText.text = won ? "Selanjutnya" : "Coba Lagi";
            }

            nextRetButton.onClick.RemoveAllListeners();
            if (won)
            {
                nextRetButton.onClick.AddListener(LoadNextScene);
            }
            else
            {
                nextRetButton.onClick.AddListener(RestartCurrentScene);
            }
        }
    }

    private void ShowFailMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || failMessageText == null)
        {
            return;
        }

        failMessageText.text = message;
        SetFailMessageVisible(true);
    }

    private void HideFailMessage()
    {
        if (failMessageText == null)
        {
            return;
        }

        SetFailMessageVisible(false);
    }

    private string ResolveLevelTipMessage()
    {
        string currentLevel = ResolveCurrentLevelName();
        if (string.IsNullOrEmpty(currentLevel))
        {
            return string.Empty;
        }

        if (string.Equals(currentLevel, "Simulasi_A"))
        {
            return tipSimulasiA;
        }

        if (string.Equals(currentLevel, "Simulasi_B"))
        {
            return tipSimulasiB;
        }

        if (string.Equals(currentLevel, "Simulasi_C"))
        {
            return tipSimulasiC;
        }

        return string.Empty;
    }

    private void SetFailMessageVisible(bool isVisible)
    {
        if (failMessageContainer != null)
        {
            failMessageContainer.SetActive(isVisible);
            if (failMessageText != null)
            {
                failMessageText.gameObject.SetActive(isVisible);
            }
            return;
        }

        if (failMessageText != null)
        {
            failMessageText.gameObject.SetActive(isVisible);
        }
    }

    private void ShowFailOverlay()
    {
        if (overlayImage == null)
        {
            return;
        }

        Color color = overlayImage.color;
        color.a = Mathf.Clamp01(overlayFailAlpha);
        overlayImage.color = color;
        overlayImage.gameObject.SetActive(true);
    }

    private void HideFailOverlay()
    {
        if (overlayImage == null)
        {
            return;
        }

        Color color = overlayImage.color;
        color.a = 0f;
        overlayImage.color = color;
        overlayImage.gameObject.SetActive(false);
    }

    private void TriggerVibration()
    {
#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
#endif
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

    private void LoadNextScene()
    {
        string nextLevelName = ResolveNextLevelName();
        if (string.IsNullOrEmpty(nextLevelName))
        {
            return;
        }

        if (string.Equals(nextLevelName, mainMenuSceneName))
        {
            RecapManager.RequestShowRecap();
            SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            return;
        }

        if (ARLevelLoader.Instance != null)
        {
            ARLevelLoader.Instance.LoadLevel(nextLevelName);
            return;
        }

        SceneManager.LoadScene(nextLevelName);
    }

    private string ResolveNextLevelName()
    {
        if (levelSequence == null || levelSequence.Length == 0)
        {
            return null;
        }

        string currentLevelName = null;
        if (ARLevelLoader.Instance != null)
        {
            currentLevelName = ARLevelLoader.Instance.CurrentLevelName;
        }

        if (string.IsNullOrEmpty(currentLevelName))
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                currentLevelName = activeScene.name;
            }
        }

        int currentIndex = -1;
        for (int i = 0; i < levelSequence.Length; i++)
        {
            if (string.Equals(levelSequence[i], currentLevelName))
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex < 0)
        {
            return levelSequence[0];
        }

        if (currentIndex >= levelSequence.Length - 1)
        {
            return mainMenuSceneName;
        }

        int nextIndex = Mathf.Clamp(currentIndex + 1, 0, levelSequence.Length - 1);
        return levelSequence[nextIndex];
    }

    private void RestartCurrentScene()
    {
        IncrementRetryCount();
        if (ARLevelLoader.Instance != null)
        {
            ARLevelLoader.Instance.ReloadCurrentLevel();
            return;
        }

        StartCoroutine(RestartCurrentSceneRoutine());
    }

    private IEnumerator RestartCurrentSceneRoutine()
    {
        if (nextRetButton != null)
        {
            nextRetButton.interactable = false;
        }

        LogRestart("Restart requested");

        ResetLocalState();
        SetTimerVisible(false);
        if (endDialogRoot != null)
        {
            endDialogRoot.SetActive(false);
        }

        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null)
        {
            LogRestart("ARSession.Reset() called");
            arSession.Reset();
        }
        else
        {
            LogRestart("ARSession not found in scene");
        }

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, restartDelaySeconds));

        if (!string.IsNullOrEmpty(retrySceneName))
        {
            LogRestart("Loading retry scene by name: " + retrySceneName);
            pendingRestartReset = true;
            SceneManager.LoadScene(retrySceneName, LoadSceneMode.Single);
            yield break;
        }

        LogRestart("Loading current scene by name: " + SceneManager.GetActiveScene().name);
        pendingRestartReset = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    private void ResetLocalState()
    {
        countdownStarted = false;
        countdownFinished = false;
        isWin = false;
        remainingSeconds = Mathf.Max(0f, countdownSeconds);
        RefreshTimerLabel();
    }

    private void RecordLevelCompletion()
    {
        string levelName = ResolveCurrentLevelName();
        if (string.IsNullOrEmpty(levelName))
        {
            return;
        }

        float elapsedSeconds = Mathf.Max(0f, countdownSeconds - remainingSeconds);
        RecapManager.RecordLevelCompletion(levelName, elapsedSeconds);
    }

    private void IncrementRetryCount()
    {
        string levelName = ResolveCurrentLevelName();
        if (string.IsNullOrEmpty(levelName))
        {
            return;
        }

        RecapManager.IncrementRetry(levelName);
    }

    private string ResolveCurrentLevelName()
    {
        if (ARLevelLoader.Instance != null && !string.IsNullOrEmpty(ARLevelLoader.Instance.CurrentLevelName))
        {
            return ARLevelLoader.Instance.CurrentLevelName;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() ? activeScene.name : null;
    }

    private void LogRestart(string message)
    {
        if (!enableRestartDebugLogs)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        string sceneInfo = "scene=" + activeScene.name + " index=" + activeScene.buildIndex + " loaded=" + activeScene.isLoaded;
        Debug.Log("[ARSpawnCountdownTimer] " + message + " | " + sceneInfo, this);

        int sceneCount = SceneManager.sceneCount;
        for (int i = 0; i < sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid())
            {
                continue;
            }

            int rootCount = scene.GetRootGameObjects().Length;
            Debug.Log("[ARSpawnCountdownTimer] Scene " + i + " name=" + scene.name + " loaded=" + scene.isLoaded + " roots=" + rootCount, this);

            if (logRootNamesOnRestart && scene == activeScene && !hasLoggedPostReloadRoots)
            {
                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    Debug.Log("[ARSpawnCountdownTimer] Root " + r + " name=" + roots[r].name + " active=" + roots[r].activeInHierarchy, this);
                }
                hasLoggedPostReloadRoots = true;
            }
        }
    }

    private IEnumerator PostReloadResetRoutine()
    {
        LogRestart("Post-reload reset starting");

        yield return null;

        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null)
        {
            arSession.enabled = false;
            yield return null;
            arSession.enabled = true;
            LogRestart("Post-reload ARSession toggled");
        }
        else
        {
            LogRestart("Post-reload ARSession not found");
        }

        ARPlaneManager planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (planeManager != null)
        {
            planeManager.enabled = false;
            yield return null;
            planeManager.enabled = true;
            LogRestart("Post-reload ARPlaneManager toggled");
        }

        ARRaycastManager raycastManager = FindFirstObjectByType<ARRaycastManager>();
        if (raycastManager != null)
        {
            raycastManager.enabled = false;
            yield return null;
            raycastManager.enabled = true;
            LogRestart("Post-reload ARRaycastManager toggled");
        }

        LogRestart("Post-reload reset finished");

#if UNITY_EDITOR
        TryReloadSimulationScene();
#endif
    }

#if UNITY_EDITOR
    private void TryReloadSimulationScene()
    {
        if (!loadSimulationSceneInEditor || string.IsNullOrEmpty(simulatedEnvironmentSceneName))
        {
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.name == simulatedEnvironmentSceneName)
            {
                LogRestart("Simulation scene already loaded: " + simulatedEnvironmentSceneName);
                return;
            }
        }

        LogRestart("Loading simulation scene additively: " + simulatedEnvironmentSceneName);
        SceneManager.LoadScene(simulatedEnvironmentSceneName, LoadSceneMode.Additive);
    }
#endif
}