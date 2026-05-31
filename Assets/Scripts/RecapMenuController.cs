using UnityEngine;
using UnityEngine.UI;

public class RecapMenuController : MonoBehaviour
{
    [Header("Recap UI")]
    [SerializeField] private GameObject recapRoot;
    [SerializeField] private string recapRootName = "RecapUI";
    [SerializeField] private RecapPanelUI recapPanel;

    [Header("Leaderboard UI")]
    [SerializeField] private LeaderboardPanelUI leaderboardPanel;
    [SerializeField] private string leaderboardRootName = "LeaderboardUI";

    [Header("Name Prompt")]
    [SerializeField] private GameObject namePromptRoot;
    [SerializeField] private string namePromptRootName = "NamePrompt";
    [SerializeField] private InputField nameInput;
    [SerializeField] private string nameInputName = "NameInput";

    private void Start()
    {
        ResolveReferences();

        if (RecapManager.ConsumeShowRecapRequest())
        {
            SetRecapVisible(true);
        }
    }

    public void OnSelesaiClicked()
    {
        ResolveReferences();
        if (recapRoot != null)
        {
            recapRoot.SetActive(false);
        }

        if (namePromptRoot != null)
        {
            namePromptRoot.SetActive(true);
        }

        if (nameInput != null)
        {
            nameInput.text = string.Empty;
            nameInput.ActivateInputField();
        }
    }

    public void OnSubmitName()
    {
        if (nameInput == null)
        {
            Debug.LogWarning("[RecapMenu] Name input missing.", this);
            return;
        }

        string playerName = nameInput.text != null ? nameInput.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(playerName))
        {
            Debug.LogWarning("[RecapMenu] Player name is empty.", this);
            return;
        }

        RecapRunData recap = RecapManager.ReadPersistedRun();
        if (recap == null)
        {
            Debug.LogWarning("[RecapMenu] Recap run data is null.", this);
        }
        else
        {
            Debug.Log("[RecapMenu] Saving entry for " + playerName + ", levels=" + (recap.levels != null ? recap.levels.Count : 0), this);
        }
        if (recap != null)
        {
            LeaderboardManager.AddEntry(playerName, recap);
            if (leaderboardPanel != null)
            {
                leaderboardPanel.Refresh();
            }
        }

        ARLevelLoader.RequestARResetOnNextLoad();

        if (namePromptRoot != null)
        {
            namePromptRoot.SetActive(false);
        }
    }

    public void OnCancelName()
    {
        if (namePromptRoot != null)
        {
            namePromptRoot.SetActive(false);
        }
    }

    private void SetRecapVisible(bool isVisible)
    {
        if (recapRoot != null)
        {
            recapRoot.SetActive(isVisible);
        }

        if (recapPanel != null)
        {
            recapPanel.Refresh();
        }
    }

    private void ResolveReferences()
    {
        if (recapRoot == null && !string.IsNullOrEmpty(recapRootName))
        {
            recapRoot = FindByName(recapRootName);
        }

        if (recapPanel == null && recapRoot != null)
        {
            recapPanel = recapRoot.GetComponentInChildren<RecapPanelUI>(true);
        }

        if (leaderboardPanel == null)
        {
            GameObject leaderboardRoot = FindByName(leaderboardRootName);
            if (leaderboardRoot != null)
            {
                leaderboardPanel = leaderboardRoot.GetComponentInChildren<LeaderboardPanelUI>(true);
            }
        }

        if (namePromptRoot == null && !string.IsNullOrEmpty(namePromptRootName))
        {
            namePromptRoot = FindByName(namePromptRootName);
        }

        if (nameInput == null && !string.IsNullOrEmpty(nameInputName))
        {
            GameObject inputObject = FindByName(nameInputName);
            if (inputObject != null)
            {
                nameInput = inputObject.GetComponent<InputField>();
            }
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
