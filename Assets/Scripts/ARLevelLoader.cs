using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;

public class ARLevelLoader : MonoBehaviour
{
    public static ARLevelLoader Instance { get; private set; }
    private static bool pendingArReset;

    [Header("Scenes")]
    [SerializeField] private string startingLevelName = "Simulasi_A";
    [SerializeField] private string coreSceneName = "Core";
    [SerializeField] private string simulatedEnvironmentSceneName = "";
    [SerializeField] private bool enableDebugLogs = true;

    private string currentLevelName;
    private bool isLoading;

    public string CurrentLevelName => currentLevelName;
    public bool IsLoading => isLoading;

    public static void RequestARResetOnNextLoad()
    {
        pendingArReset = true;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(startingLevelName))
        {
            LoadLevel(startingLevelName);
        }
    }

    public void LoadLevel(string levelName)
    {
        if (isLoading || string.IsNullOrEmpty(levelName))
        {
            return;
        }

        StartCoroutine(LoadLevelRoutine(levelName));
    }

    public void ReloadCurrentLevel()
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrEmpty(currentLevelName))
        {
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && !string.Equals(activeScene.name, coreSceneName))
            {
                currentLevelName = activeScene.name;
            }
        }

        if (!string.IsNullOrEmpty(currentLevelName))
        {
            LoadLevel(currentLevelName);
        }
    }

    private IEnumerator LoadLevelRoutine(string levelName)
    {
        isLoading = true;

        yield return UnloadNonPersistentScenes(levelName);

        if (!string.IsNullOrEmpty(currentLevelName))
        {
            currentLevelName = null;
        }

        LogLoader("Loading level additively: " + levelName);
        AsyncOperation loadOp = SceneManager.LoadSceneAsync(levelName, LoadSceneMode.Additive);
        if (loadOp != null)
        {
            while (!loadOp.isDone)
            {
                yield return null;
            }
        }

        Scene loadedScene = SceneManager.GetSceneByName(levelName);
        if (loadedScene.IsValid())
        {
            SceneManager.SetActiveScene(loadedScene);
            currentLevelName = levelName;
        }

        ResetLevelState();

        isLoading = false;
    }

    private void LogLoader(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        Debug.Log("[ARLevelLoader] " + message + " | active=" + activeScene.name, this);
    }

    private void ResetLevelState()
    {
        ClearEquippedObjects();

        ARSpawnCountdownTimer[] timers = FindObjectsByType<ARSpawnCountdownTimer>(FindObjectsSortMode.None);
        for (int i = 0; i < timers.Length; i++)
        {
            timers[i].ResetForNewLevel();
        }

        if (pendingArReset)
        {
            pendingArReset = false;
            StartCoroutine(ResetArSubsystems());
        }
    }

    private IEnumerator ResetArSubsystems()
    {
        yield return null;

        ARSession arSession = FindFirstObjectByType<ARSession>();
        if (arSession != null)
        {
            arSession.enabled = false;
            yield return null;
            arSession.enabled = true;
        }

        ARPlaneManager planeManager = FindFirstObjectByType<ARPlaneManager>();
        if (planeManager != null)
        {
            planeManager.enabled = false;
            yield return null;
            planeManager.enabled = true;
        }

        ARRaycastManager raycastManager = FindFirstObjectByType<ARRaycastManager>();
        if (raycastManager != null)
        {
            raycastManager.enabled = false;
            yield return null;
            raycastManager.enabled = true;
        }
    }

    private IEnumerator UnloadNonPersistentScenes(string nextLevelName)
    {
        int sceneCount = SceneManager.sceneCount;
        for (int i = sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            if (IsPersistentScene(scene.name))
            {
                continue;
            }

            if (string.Equals(scene.name, nextLevelName))
            {
                LogLoader("Unloading existing level scene: " + scene.name);
            }
            else
            {
                LogLoader("Unloading non-persistent scene: " + scene.name);
            }

            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }

    private bool IsPersistentScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return false;
        }

        if (string.Equals(sceneName, coreSceneName))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(simulatedEnvironmentSceneName) && string.Equals(sceneName, simulatedEnvironmentSceneName))
        {
            return true;
        }

        return false;
    }

    private void ClearEquippedObjects()
    {
        if (!ARSingleEquipSlot.HasEquippedObject)
        {
            return;
        }

        GameObject equipped = ARSingleEquipSlot.EquippedObject;
        if (equipped != null)
        {
            Destroy(equipped);
        }

        ARSingleEquipSlot.ResetSlot();
    }
}
