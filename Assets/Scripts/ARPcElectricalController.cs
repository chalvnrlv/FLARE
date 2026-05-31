using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ARPcElectricalController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARBurningAreaTrigger burningArea;
    [SerializeField] private FireHealthTarget fireTarget;
    [SerializeField] private Camera arCamera;

    [Header("UI")]
    [SerializeField] private string textDialogueName = "TextDialogue";
    [SerializeField] private Text textDialogue;
    [SerializeField] private string textContainerName = "TextContainer";
    [SerializeField] private GameObject textContainer;
    [SerializeField] private Button electricalOffButton;
    [SerializeField] private string electricalOffButtonName = "ElectricalOffButton";

    [Header("PC Settings")]
    [SerializeField] private string pcRootName = "Burning_PC";
    [SerializeField] private string sparksChildName = "Sparks";
    [SerializeField] private float promptDuration = 10f;
    [SerializeField] private float shortPromptDuration = 2f;
    [SerializeField] private string promptMessage = "Matikan sumber listrik terlebih dahulu sebelum memadamkan api!";
    [SerializeField] private string shortPromptMessage = "Matikan sumber listrik terlebih dahulu!";

    private bool isPowerOff;
    private bool promptShown;
    private Coroutine promptRoutine;

    public bool IsPowerOff => isPowerOff;

    private void Awake()
    {
        ResolveReferences();
        SetElectricalButtonVisible(false);
    }

    private void Update()
    {
        ResolveReferences();

        if (!IsPcFireTarget())
        {
            ResetState();
            return;
        }

        if (!isPowerOff)
        {
            bool inside = IsCameraInsideBurningArea();
            SetElectricalButtonVisible(inside);

            if (inside && !promptShown)
            {
                ShowPrompt(promptMessage, promptDuration);
                promptShown = true;
            }
        }
        else
        {
            SetElectricalButtonVisible(false);
        }
    }

    private void ResolveReferences()
    {
        if (burningArea == null)
        {
            burningArea = FindFirstObjectByType<ARBurningAreaTrigger>();
        }

        if (fireTarget == null)
        {
            fireTarget = FindFirstObjectByType<FireHealthTarget>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
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

        if (electricalOffButton == null)
        {
            GameObject buttonObject = FindSceneObjectByNameIncludingInactive(electricalOffButtonName);
            if (buttonObject != null)
            {
                electricalOffButton = buttonObject.GetComponent<Button>();
                if (electricalOffButton != null)
                {
                    electricalOffButton.onClick.RemoveListener(OnElectricalOffClicked);
                    electricalOffButton.onClick.AddListener(OnElectricalOffClicked);
                }
            }
        }
    }

    public void ResetForNewLevel()
    {
        ResetState();
    }

    public void ShowMustTurnOffMessage()
    {
        if (!IsPcFireTarget() || isPowerOff)
        {
            return;
        }

        ShowPrompt(shortPromptMessage, shortPromptDuration);
    }

    private void OnElectricalOffClicked()
    {
        if (isPowerOff)
        {
            return;
        }

        Transform sparks = ResolveSparksTransform();
        if (sparks != null)
        {
            sparks.gameObject.SetActive(false);
        }

        isPowerOff = true;
        SetElectricalButtonVisible(false);
    }

    private void ShowPrompt(string message, float duration)
    {
        if (textDialogue == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        if (promptRoutine != null)
        {
            StopCoroutine(promptRoutine);
        }

        textDialogue.text = message;
        SetDialogueVisible(true);
        promptRoutine = StartCoroutine(HidePromptAfterDelay(duration));
    }

    private IEnumerator HidePromptAfterDelay(float duration)
    {
        float waitSeconds = Mathf.Max(0f, duration);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        if (textDialogue != null)
        {
            SetDialogueVisible(false);
        }

        promptRoutine = null;
    }

    private void SetElectricalButtonVisible(bool isVisible)
    {
        if (electricalOffButton == null)
        {
            return;
        }

        electricalOffButton.gameObject.SetActive(isVisible);
        electricalOffButton.interactable = isVisible;
    }

    private bool IsCameraInsideBurningArea()
    {
        if (burningArea == null || arCamera == null)
        {
            return false;
        }

        return burningArea.IsPositionInsideArea(arCamera.transform.position);
    }

    public bool IsPcFireTarget()
    {
        if (fireTarget == null)
        {
            return false;
        }

        string pcLower = pcRootName.ToLowerInvariant();
        Transform current = fireTarget.transform;
        while (current != null)
        {
            if (!string.IsNullOrEmpty(current.name) && current.name.ToLowerInvariant().Contains(pcLower))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private Transform ResolveSparksTransform()
    {
        if (fireTarget == null)
        {
            return null;
        }

        Transform root = fireTarget.transform;
        while (root.parent != null)
        {
            root = root.parent;
        }

        Transform[] allChildren = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allChildren.Length; i++)
        {
            if (!string.IsNullOrEmpty(allChildren[i].name) && allChildren[i].name.Contains(sparksChildName))
            {
                return allChildren[i];
            }
        }

        return null;
    }

    private void ResetState()
    {
        isPowerOff = false;
        promptShown = false;
        SetElectricalButtonVisible(false);
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
}
