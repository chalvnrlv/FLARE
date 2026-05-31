using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ARLpgValveController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ARBurningAreaTrigger burningArea;
    [SerializeField] private FireHealthTarget fireTarget;
    [SerializeField] private Transform valveTransform;
    [SerializeField] private Camera arCamera;

    [Header("UI")]
    [SerializeField] private string textDialogueName = "TextDialogue";
    [SerializeField] private Text textDialogue;
    [SerializeField] private string textContainerName = "TextContainer";
    [SerializeField] private GameObject textContainer;
    [SerializeField] private Button valveButton;
    [SerializeField] private string valveButtonName = "ValveButton";

    [Header("LPG Settings")]
    [SerializeField] private string lpgRootName = "Burning_LPG";
    [SerializeField] private string valveChildName = "Valve";
    [SerializeField] private string promptMessage = "Matikan gas dari LPG!";
    [SerializeField] private string successMessage = "Kerja Bagus !";
    [SerializeField] private float valveRotateDuration = 1f;

    private bool isWaitingForValve;
    private bool isValveTurning;
    private bool isValveCompleted;

    public bool IsWaitingForValve => isWaitingForValve;
    public bool IsValveCompleted => isValveCompleted;

    private void Awake()
    {
        ResolveReferences();
        SetValveButtonVisible(false);
    }

    private void OnDestroy()
    {
        SetValveButtonVisible(false);
    }

    public void ResetForNewLevel()
    {
        ResetState();
    }

    private void Update()
    {
        ResolveReferences();

        if (isValveTurning)
        {
            return;
        }

        if (isValveCompleted)
        {
            SetValveButtonVisible(false);
            return;
        }

        if (!IsLpgFireTarget())
        {
            ResetState();
            return;
        }

        if (fireTarget != null && fireTarget.IsExtinguished)
        {
            isWaitingForValve = true;
            isValveCompleted = false;
            ShowDialogue(promptMessage);
            UpdateValveButtonVisibility();
        }
        else
        {
            ResetState();
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

        if (valveButton == null)
        {
            GameObject buttonObject = FindSceneObjectByNameIncludingInactive(valveButtonName);
            if (buttonObject != null)
            {
                valveButton = buttonObject.GetComponent<Button>();
                if (valveButton != null)
                {
                    valveButton.onClick.RemoveListener(OnValveButtonClicked);
                    valveButton.onClick.AddListener(OnValveButtonClicked);
                }
            }
        }

        if (valveTransform == null)
        {
            valveTransform = ResolveValveTransform();
        }
    }

    private void UpdateValveButtonVisibility()
    {
        bool shouldShow = isWaitingForValve && IsCameraInsideBurningArea();
        SetValveButtonVisible(shouldShow);
    }

    private void SetValveButtonVisible(bool isVisible)
    {
        if (valveButton == null)
        {
            return;
        }

        valveButton.gameObject.SetActive(isVisible);
        valveButton.interactable = isVisible;
    }

    private void OnValveButtonClicked()
    {
        if (!isWaitingForValve || isValveTurning)
        {
            return;
        }

        if (valveTransform == null)
        {
            return;
        }

        StartCoroutine(RotateValveAndFinish());
    }

    private IEnumerator RotateValveAndFinish()
    {
        isValveTurning = true;
        SetValveButtonVisible(false);

        float duration = Mathf.Max(0.01f, valveRotateDuration);
        float elapsed = 0f;
        Quaternion startRotation = valveTransform.localRotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, 0f, 360f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            valveTransform.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        valveTransform.localRotation = endRotation;
        isValveTurning = false;
        isWaitingForValve = false;
        isValveCompleted = true;

        ShowDialogue(successMessage);

        ARSpawnCountdownTimer timer = FindFirstObjectByType<ARSpawnCountdownTimer>();
        if (timer != null)
        {
            timer.TriggerWinWithMessage(successMessage);
        }
    }

    private bool IsCameraInsideBurningArea()
    {
        if (burningArea == null || arCamera == null)
        {
            return false;
        }

        return burningArea.IsPositionInsideArea(arCamera.transform.position);
    }

    private bool IsLpgFireTarget()
    {
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

    private Transform ResolveValveTransform()
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
            if (!string.IsNullOrEmpty(allChildren[i].name) && allChildren[i].name.Contains(valveChildName))
            {
                return allChildren[i];
            }
        }

        return null;
    }

    private void ShowDialogue(string message)
    {
        if (textDialogue == null || string.IsNullOrEmpty(message))
        {
            return;
        }

        textDialogue.text = message;
        SetDialogueVisible(true);
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

    private void ResetState()
    {
        isWaitingForValve = false;
        isValveTurning = false;
        isValveCompleted = false;
        SetValveButtonVisible(false);
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
