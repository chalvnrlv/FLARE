using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class RecapPanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text recapText;
    [SerializeField] private string emptyMessage = "Belum ada data recap.";

    [Header("Level A")]
    [SerializeField] private Text aDurationText;
    [SerializeField] private Text aRetriesText;
    [SerializeField] private string aDurationName = "A_duration";
    [SerializeField] private string aRetriesName = "A_retries";

    [Header("Level B")]
    [SerializeField] private Text bDurationText;
    [SerializeField] private Text bRetriesText;
    [SerializeField] private string bDurationName = "B_duration";
    [SerializeField] private string bRetriesName = "B_retries";

    [Header("Level C")]
    [SerializeField] private Text cDurationText;
    [SerializeField] private Text cRetriesText;
    [SerializeField] private string cDurationName = "C_duration";
    [SerializeField] private string cRetriesName = "C_retries";

    [Header("Level Ev")]
    [SerializeField] private Text evDurationText;
    [SerializeField] private Text evRetriesText;
    [SerializeField] private string evDurationName = "Ev_duration";
    [SerializeField] private string evRetriesName = "Ev_retries";

    [Header("Levels")]
    [SerializeField] private string[] levelOrder = new string[] { "Simulasi_A", "Simulasi_B", "Simulasi_C", "Simulasi_Ev" };

    private RecapRunData customRun;

    private void OnEnable()
    {
        ResolveTextReferences();
        Refresh();
    }

    public void Refresh()
    {
        if (recapText == null && aDurationText == null && aRetriesText == null)
        {
            return;
        }

        RecapRunData data = customRun ?? RecapManager.ReadPersistedRun();
        if (data == null || data.levels == null || data.levels.Count == 0)
        {
            if (recapText != null)
            {
                recapText.text = emptyMessage;
            }
            return;
        }

        ApplyLevel(data, "Simulasi_A", aDurationText, aRetriesText);
        ApplyLevel(data, "Simulasi_B", bDurationText, bRetriesText);
        ApplyLevel(data, "Simulasi_C", cDurationText, cRetriesText);
        ApplyLevel(data, "Simulasi_Ev", evDurationText, evRetriesText);

        if (recapText != null)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < levelOrder.Length; i++)
            {
                LevelRecap recap = FindLevel(data, levelOrder[i]);
                if (recap == null)
                {
                    continue;
                }

                builder.Append(levelOrder[i]);
                builder.Append(": ");
                builder.Append(FormatDuration(recap.lastDurationSeconds));
                builder.Append(" | Ulang: ");
                builder.Append(recap.retryCount);
                if (i < levelOrder.Length - 1)
                {
                    builder.AppendLine();
                }
            }

            recapText.text = builder.ToString();
        }
    }

    public void ShowCustomRun(RecapRunData run)
    {
        customRun = run;
        Refresh();
    }

    public void ClearCustomRun()
    {
        customRun = null;
    }

    private void ResolveTextReferences()
    {
        aDurationText = aDurationText != null ? aDurationText : FindTextByName(aDurationName);
        aRetriesText = aRetriesText != null ? aRetriesText : FindTextByName(aRetriesName);

        bDurationText = bDurationText != null ? bDurationText : FindTextByName(bDurationName);
        bRetriesText = bRetriesText != null ? bRetriesText : FindTextByName(bRetriesName);

        cDurationText = cDurationText != null ? cDurationText : FindTextByName(cDurationName);
        cRetriesText = cRetriesText != null ? cRetriesText : FindTextByName(cRetriesName);

        evDurationText = evDurationText != null ? evDurationText : FindTextByName(evDurationName);
        evRetriesText = evRetriesText != null ? evRetriesText : FindTextByName(evRetriesName);
    }

    private Text FindTextByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform found = FindChildByName(transform, objectName);
        if (found == null)
        {
            return null;
        }

        return found.GetComponent<Text>();
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        foreach (Transform child in root)
        {
            Transform found = FindChildByName(child, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void ApplyLevel(RecapRunData data, string levelName, Text durationText, Text retriesText)
    {
        LevelRecap recap = FindLevel(data, levelName);
        if (recap == null)
        {
            return;
        }

        if (durationText != null)
        {
            durationText.text = FormatDuration(recap.lastDurationSeconds);
        }

        if (retriesText != null)
        {
            retriesText.text = recap.retryCount.ToString();
        }
    }

    private LevelRecap FindLevel(RecapRunData data, string levelName)
    {
        for (int i = 0; i < data.levels.Count; i++)
        {
            if (string.Equals(data.levels[i].levelName, levelName, StringComparison.Ordinal))
            {
                return data.levels[i];
            }
        }

        return null;
    }

    private string FormatDuration(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        int remaining = totalSeconds % 100;
        return remaining.ToString("00");
    }
}
