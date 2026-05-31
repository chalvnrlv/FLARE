using System;
using System.Collections.Generic;
using UnityEngine;

public class RecapManager : MonoBehaviour
{
    private const string PlayerPrefsKey = "flare.recap.run";
    private static bool pendingNewRun;
    private static bool pendingShowRecap;

    public static RecapManager Instance { get; private set; }

    [Header("Levels")]
    [SerializeField] private string[] levelOrder = new string[] { "Simulasi_A", "Simulasi_B", "Simulasi_C", "Simulasi_Ev" };

    private RecapRunData currentRun;

    public static void RequestNewRun()
    {
        pendingNewRun = true;
        if (Instance != null)
        {
            Instance.StartNewRunInternal();
        }
    }

    public static void RequestShowRecap()
    {
        pendingShowRecap = true;
    }

    public static bool ConsumeShowRecapRequest()
    {
        if (!pendingShowRecap)
        {
            return false;
        }

        pendingShowRecap = false;
        return true;
    }

    public static void RecordLevelCompletion(string levelName, float durationSeconds)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.RecordLevelCompletionInternal(levelName, durationSeconds);
    }

    public static void IncrementRetry(string levelName)
    {
        if (Instance == null)
        {
            return;
        }

        Instance.IncrementRetryInternal(levelName);
    }

    public static RecapRunData ReadPersistedRun()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonUtility.FromJson<RecapRunData>(json);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadOrCreateRun();

        if (pendingNewRun)
        {
            pendingNewRun = false;
            StartNewRunInternal();
        }
    }

    private void LoadOrCreateRun()
    {
        currentRun = ReadPersistedRun();
        if (currentRun == null)
        {
            currentRun = new RecapRunData();
        }

        EnsureLevelEntries();
        SaveRun();
    }

    private void StartNewRunInternal()
    {
        currentRun = new RecapRunData();
        EnsureLevelEntries();
        SaveRun();
    }

    private void RecordLevelCompletionInternal(string levelName, float durationSeconds)
    {
        LevelRecap recap = GetOrCreateLevel(levelName);
        recap.lastDurationSeconds = durationSeconds;
        recap.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        SaveRun();
    }

    private void IncrementRetryInternal(string levelName)
    {
        LevelRecap recap = GetOrCreateLevel(levelName);
        recap.retryCount += 1;
        recap.lastUpdatedUtc = DateTime.UtcNow.ToString("o");
        SaveRun();
    }

    private void EnsureLevelEntries()
    {
        if (currentRun.levels == null)
        {
            currentRun.levels = new List<LevelRecap>();
        }

        for (int i = 0; i < levelOrder.Length; i++)
        {
            GetOrCreateLevel(levelOrder[i]);
        }
    }

    private LevelRecap GetOrCreateLevel(string levelName)
    {
        if (currentRun.levels == null)
        {
            currentRun.levels = new List<LevelRecap>();
        }

        for (int i = 0; i < currentRun.levels.Count; i++)
        {
            if (string.Equals(currentRun.levels[i].levelName, levelName, StringComparison.Ordinal))
            {
                return currentRun.levels[i];
            }
        }

        LevelRecap newRecap = new LevelRecap
        {
            levelName = levelName,
            lastDurationSeconds = 0f,
            retryCount = 0,
            lastUpdatedUtc = string.Empty
        };
        currentRun.levels.Add(newRecap);
        return newRecap;
    }

    private void SaveRun()
    {
        string json = JsonUtility.ToJson(currentRun);
        PlayerPrefs.SetString(PlayerPrefsKey, json);
        PlayerPrefs.Save();
    }
}

[Serializable]
public class RecapRunData
{
    public List<LevelRecap> levels = new List<LevelRecap>();
}

[Serializable]
public class LevelRecap
{
    public string levelName;
    public float lastDurationSeconds;
    public int retryCount;
    public string lastUpdatedUtc;
}
