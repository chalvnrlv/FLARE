using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardEntryUI : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private Text rankText;
    [SerializeField] private Text nameText;
    [SerializeField] private Text timeText;
    [SerializeField] private Text retriesText;
    [SerializeField] private Text dateText;
    [SerializeField] private Button headerButton;

    private LeaderboardEntry boundEntry;

    public event Action<LeaderboardEntry> Selected;

    private void Awake()
    {
        if (headerButton != null)
        {
            headerButton.onClick.AddListener(OnHeaderClicked);
        }

    }

    public void Bind(LeaderboardEntry entry, int rank, bool useMinuteFormat)
    {
        boundEntry = entry;
        if (rankText != null)
        {
            rankText.text = rank.ToString();
        }

        if (nameText != null)
        {
            nameText.text = entry != null ? entry.playerName : string.Empty;
        }

        if (timeText != null)
        {
            timeText.text = FormatDuration(entry != null ? entry.totalDurationSeconds : 0f, useMinuteFormat);
        }

        if (retriesText != null)
        {
            retriesText.text = entry != null ? entry.totalRetries.ToString() : "0";
        }

        if (dateText != null)
        {
            dateText.text = entry != null ? FormatDate(entry.completedUtc) : string.Empty;
        }

    }

    private void OnHeaderClicked()
    {
        if (boundEntry != null)
        {
            Selected?.Invoke(boundEntry);
        }
    }

    private string FormatDuration(float seconds, bool useMinuteFormat)
    {
        int totalSeconds = Mathf.Max(0, Mathf.RoundToInt(seconds));
        if (!useMinuteFormat)
        {
            int remaining = totalSeconds % 100;
            return remaining.ToString("00");
        }

        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return string.Format("{0:00}:{1:00}", minutes, remainingSeconds);
    }

    private string FormatDate(string utc)
    {
        if (string.IsNullOrEmpty(utc))
        {
            return string.Empty;
        }

        DateTime parsed;
        if (!DateTime.TryParse(utc, out parsed))
        {
            return utc;
        }

        DateTime local = parsed.ToLocalTime();
        return local.ToString("dd/MM/yyyy");
    }
}
