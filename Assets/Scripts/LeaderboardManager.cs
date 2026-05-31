using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class LeaderboardManager
{
    private const string PlayerPrefsKey = "flare.leaderboard";
    private const int MaxEntries = 30;
    private static readonly string[] LevelOrder = new string[] { "Simulasi_A", "Simulasi_B", "Simulasi_C", "Simulasi_Ev" };

    public static List<LeaderboardEntry> LoadEntries()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            Debug.Log("[Leaderboard] No saved entries.");
            return new List<LeaderboardEntry>();
        }

        LeaderboardData data = JsonUtility.FromJson<LeaderboardData>(json);
        if (data == null || data.entries == null)
        {
            Debug.LogWarning("[Leaderboard] Failed to parse leaderboard JSON.");
        }
        return data != null && data.entries != null ? data.entries : new List<LeaderboardEntry>();
    }

    public static void AddEntry(string playerName, RecapRunData recap)
    {
        if (recap == null)
        {
            Debug.LogWarning("[Leaderboard] Recap is null, cannot add entry.");
            return;
        }

        List<LeaderboardEntry> entries = LoadEntries();
        LeaderboardEntry entry = new LeaderboardEntry
        {
            playerName = playerName,
            totalDurationSeconds = SumDurations(recap),
            totalRetries = SumRetries(recap),
            completedUtc = DateTime.UtcNow.ToString("o"),
            levels = CloneLevels(recap)
        };

        entries.Add(entry);
        entries.Sort((a, b) => a.totalDurationSeconds.CompareTo(b.totalDurationSeconds));

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        LeaderboardData data = new LeaderboardData { entries = entries };
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log("[Leaderboard] Saved entries: " + entries.Count + ", last=" + playerName);
    }

    public static void AddDummyEntries(int count)
    {
        if (count <= 0)
        {
            return;
        }

        List<LeaderboardEntry> entries = LoadEntries();
        int startIndex = entries.Count + 1;

        for (int i = 0; i < count; i++)
        {
            RecapRunData dummyRecap = BuildDummyRecap();
            LeaderboardEntry entry = new LeaderboardEntry
            {
                playerName = "Test" + (startIndex + i),
                totalDurationSeconds = SumDurations(dummyRecap),
                totalRetries = SumRetries(dummyRecap),
                completedUtc = DateTime.UtcNow.AddMinutes(-(startIndex + i)).ToString("o"),
                levels = CloneLevels(dummyRecap)
            };

            entries.Add(entry);
        }

        entries.Sort((a, b) =>
        {
            int timeCompare = a.totalDurationSeconds.CompareTo(b.totalDurationSeconds);
            if (timeCompare != 0)
            {
                return timeCompare;
            }

            int retryCompare = a.totalRetries.CompareTo(b.totalRetries);
            if (retryCompare != 0)
            {
                return retryCompare;
            }

            return string.Compare(b.completedUtc, a.completedUtc, StringComparison.Ordinal);
        });

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        LeaderboardData data = new LeaderboardData { entries = entries };
        PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log("[Leaderboard] Added dummy entries: " + count + ", total=" + entries.Count);
    }

    public static void ClearHistory()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
    }

    public static string ExportToJsonFile()
    {
        string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            json = JsonUtility.ToJson(new LeaderboardData());
        }

        string path = Path.Combine(Application.persistentDataPath, "leaderboard.json");
        File.WriteAllText(path, json);
        return path;
    }

    private static float SumDurations(RecapRunData recap)
    {
        float sum = 0f;
        if (recap.levels == null)
        {
            return sum;
        }

        for (int i = 0; i < recap.levels.Count; i++)
        {
            sum += Mathf.Max(0f, recap.levels[i].lastDurationSeconds);
        }

        return sum;
    }

    private static int SumRetries(RecapRunData recap)
    {
        int sum = 0;
        if (recap.levels == null)
        {
            return sum;
        }

        for (int i = 0; i < recap.levels.Count; i++)
        {
            sum += Mathf.Max(0, recap.levels[i].retryCount);
        }

        return sum;
    }

    private static RecapRunData BuildDummyRecap()
    {
        RecapRunData recap = new RecapRunData();
        for (int i = 0; i < LevelOrder.Length; i++)
        {
            LevelRecap level = new LevelRecap
            {
                levelName = LevelOrder[i],
                lastDurationSeconds = UnityEngine.Random.Range(8f, 65f),
                retryCount = UnityEngine.Random.Range(0, 4),
                lastUpdatedUtc = DateTime.UtcNow.ToString("o")
            };
            recap.levels.Add(level);
        }

        return recap;
    }

    private static List<LevelRecap> CloneLevels(RecapRunData recap)
    {
        List<LevelRecap> levels = new List<LevelRecap>();
        if (recap.levels == null)
        {
            return levels;
        }

        for (int i = 0; i < recap.levels.Count; i++)
        {
            LevelRecap source = recap.levels[i];
            if (source == null)
            {
                continue;
            }

            LevelRecap copy = new LevelRecap
            {
                levelName = source.levelName,
                lastDurationSeconds = source.lastDurationSeconds,
                retryCount = source.retryCount,
                lastUpdatedUtc = source.lastUpdatedUtc
            };
            levels.Add(copy);
        }

        return levels;
    }
}

[Serializable]
public class LeaderboardEntry
{
    public string playerName;
    public float totalDurationSeconds;
    public int totalRetries;
    public string completedUtc;
    public List<LevelRecap> levels = new List<LevelRecap>();
}

[Serializable]
public class LeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}
