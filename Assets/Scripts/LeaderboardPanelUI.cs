using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardPanelUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject leaderboardRoot;
    [SerializeField] private string leaderboardRootName = "LeaderboardUI";
    [SerializeField] private Transform contentRoot;
    [SerializeField] private string contentRootName = "LeaderboardContent";
    [SerializeField] private LeaderboardEntryUI entryPrefab;
    [SerializeField] private Text emptyText;
    [SerializeField] private string emptyMessage = "Belum ada data.";
    [SerializeField] private GameObject recapPopupRoot;
    [SerializeField] private string recapPopupRootName = "RecapUI";
    [SerializeField] private RecapPanelUI recapPanel;
    [SerializeField] private Button recapBackdropButton;
    [SerializeField] private string recapBackdropName = "RecapBackdrop";

    [Header("Buttons")]
    [SerializeField] private Button clearButton;
    [SerializeField] private Button exportButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button addDummyButton;

    [Header("Behavior")]
    [SerializeField] private bool useMinuteFormatForTotal = true;

    private readonly List<LeaderboardEntryUI> spawnedEntries = new List<LeaderboardEntryUI>();
    private void Awake()
    {
        ResolveReferences();
        HookButtons();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Refresh();
    }

    public void Show()
    {
        if (leaderboardRoot != null)
        {
            leaderboardRoot.SetActive(true);
        }

        Refresh();
    }

    public void Hide()
    {
        if (leaderboardRoot != null)
        {
            leaderboardRoot.SetActive(false);
        }
    }

    public void Refresh()
    {
        if (contentRoot == null || entryPrefab == null)
        {
            return;
        }

        ClearEntries();

        List<LeaderboardEntry> entries = LeaderboardManager.LoadEntries();
        SortEntries(entries);

        if (entries.Count == 0)
        {
            if (emptyText != null)
            {
                emptyText.text = emptyMessage;
                emptyText.gameObject.SetActive(true);
            }
            return;
        }

        if (emptyText != null)
        {
            emptyText.gameObject.SetActive(false);
        }

        for (int i = 0; i < entries.Count; i++)
        {
            LeaderboardEntryUI row = Instantiate(entryPrefab, contentRoot);
            row.Bind(entries[i], i + 1, useMinuteFormatForTotal);
            row.Selected += HandleEntrySelected;
            spawnedEntries.Add(row);
        }
    }

    public void OnClearClicked()
    {
        LeaderboardManager.ClearHistory();
        Refresh();
    }

    public void OnExportClicked()
    {
        string path = LeaderboardManager.ExportToJsonFile();
        Debug.Log("[Leaderboard] Exported to: " + path, this);
    }

    public void OnCloseClicked()
    {
        Hide();
    }

    public void OnAddDummyClicked()
    {
        LeaderboardManager.AddDummyEntries(30);
        Refresh();
    }

    public void OnCloseRecapPopup()
    {
        if (recapPopupRoot != null)
        {
            recapPopupRoot.SetActive(false);
        }

        if (recapPanel != null)
        {
            recapPanel.ClearCustomRun();
        }
    }

    private void HandleEntrySelected(LeaderboardEntry entry)
    {
        if (entry == null || recapPanel == null)
        {
            return;
        }

        RecapRunData run = new RecapRunData
        {
            levels = entry.levels
        };

        recapPanel.ShowCustomRun(run);
        if (recapPopupRoot != null)
        {
            recapPopupRoot.SetActive(true);
        }
    }

    private void SortEntries(List<LeaderboardEntry> entries)
    {
        if (entries == null)
        {
            return;
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

            return string.Compare(b.completedUtc, a.completedUtc, System.StringComparison.Ordinal);
        });
    }

    private void ClearEntries()
    {
        for (int i = 0; i < spawnedEntries.Count; i++)
        {
            if (spawnedEntries[i] != null)
            {
                spawnedEntries[i].Selected -= HandleEntrySelected;
                Destroy(spawnedEntries[i].gameObject);
            }
        }

        spawnedEntries.Clear();
    }

    private void ResolveReferences()
    {
        if (leaderboardRoot == null && !string.IsNullOrEmpty(leaderboardRootName))
        {
            leaderboardRoot = FindByName(leaderboardRootName);
        }

        if (contentRoot == null && !string.IsNullOrEmpty(contentRootName))
        {
            GameObject contentObject = FindByName(contentRootName);
            if (contentObject != null)
            {
                contentRoot = contentObject.transform;
            }
        }

        if (recapPopupRoot == null && !string.IsNullOrEmpty(recapPopupRootName))
        {
            recapPopupRoot = FindByName(recapPopupRootName);
        }

        if (recapPanel == null && recapPopupRoot != null)
        {
            recapPanel = recapPopupRoot.GetComponentInChildren<RecapPanelUI>(true);
        }

        if (recapBackdropButton == null && !string.IsNullOrEmpty(recapBackdropName))
        {
            GameObject backdropObject = FindByName(recapBackdropName);
            if (backdropObject != null)
            {
                recapBackdropButton = backdropObject.GetComponent<Button>();
            }
        }
    }

    private void HookButtons()
    {
        if (clearButton != null)
        {
            clearButton.onClick.AddListener(OnClearClicked);
        }

        if (exportButton != null)
        {
            exportButton.onClick.AddListener(OnExportClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        if (addDummyButton != null)
        {
            addDummyButton.onClick.AddListener(OnAddDummyClicked);
        }

        if (recapBackdropButton != null)
        {
            recapBackdropButton.onClick.AddListener(OnCloseRecapPopup);
        }
    }

    private GameObject FindByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject found = FindInHierarchy(roots[i], objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private GameObject FindInHierarchy(GameObject root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        foreach (Transform child in root.transform)
        {
            GameObject found = FindInHierarchy(child.gameObject, targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
