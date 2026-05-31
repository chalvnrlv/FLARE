using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.InputSystem;

public class ARTapToPlaceSimulation : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARPlaceCube firePlacer;
    [SerializeField] private ARWallMountSpawner wallMountSpawner;

    [Header("UI")]
    [SerializeField] private string textDialogueName = "TextDialogue";
    [SerializeField] private Text textDialogue;
    [SerializeField] private string textContainerName = "TextContainer";
    [SerializeField] private GameObject textContainer;
    [SerializeField] private string scanMessage = "Gerakan ponsel anda di sekitar bidang datar untuk memulai simulasi!";
    [SerializeField] private string tapMessage = "Sentuh area yang terdeteksi untuk memulai simulasi!";
    [SerializeField] private string startMessage = "Padamkan sumber api dengan alat yang benar!";
    [SerializeField] private float startMessageDuration = 2f;

    [Header("Placement")]
    [SerializeField] private int requiredPlaneCount = 3;
    [SerializeField] private float fireDistanceForward = 1.0f;
    [SerializeField] private float wallMountDistanceBehind = 1.0f;
    [SerializeField] private float fallbackPlaneYOffset = 0.02f;

    private bool hasPlaced;
    private Coroutine startMessageRoutine;

    private void Awake()
    {
        ResolveReferences();
        DisableRandomPlacers();
        UpdateDialogueMessage();
        SetPlaneVisualization(true);
    }

    private void Update()
    {
        if (hasPlaced)
        {
            return;
        }

        ResolveReferences();
        UpdateDialogueMessage();

        if (!CanTapToPlace())
        {
            return;
        }

        if (TryGetTapPosition(out Vector2 screenPosition))
        {
            TryPlaceFromTap(screenPosition);
        }
    }

    private void ResolveReferences()
    {
        if (raycastManager == null)
        {
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
        }

        if (planeManager == null)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }

        if (firePlacer == null)
        {
            firePlacer = FindFirstObjectByType<ARPlaceCube>();
        }

        if (wallMountSpawner == null)
        {
            wallMountSpawner = FindFirstObjectByType<ARWallMountSpawner>();
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
    }

    private void DisableRandomPlacers()
    {
        if (firePlacer != null)
        {
            firePlacer.SetUseRandomPlacement(false);
        }

        if (wallMountSpawner != null)
        {
            wallMountSpawner.SetUseRandomPlacement(false);
        }
    }

    private bool CanTapToPlace()
    {
        if (raycastManager == null || planeManager == null || arCamera == null || firePlacer == null || wallMountSpawner == null)
        {
            return false;
        }

        if (firePlacer.HasPlacedObject || wallMountSpawner.HasSpawnedObject)
        {
            hasPlaced = true;
            HideDialogueMessage();
            SetPlaneVisualization(false);
            return false;
        }

        return planeManager.trackables.count >= requiredPlaneCount;
    }

    private void UpdateDialogueMessage()
    {
        if (textDialogue == null || planeManager == null)
        {
            return;
        }

        string nextMessage = planeManager.trackables.count >= requiredPlaneCount ? tapMessage : scanMessage;
        textDialogue.text = nextMessage;
        SetDialogueVisible(true);
    }

    private void HideDialogueMessage()
    {
        if (textDialogue == null)
        {
            return;
        }

        SetDialogueVisible(false);
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

    private bool TryGetTapPosition(out Vector2 screenPosition)
    {
        screenPosition = Vector2.zero;

        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                screenPosition = touch.position.ReadValue();
                return true;
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        return false;
    }

    private void TryPlaceFromTap(Vector2 screenPosition)
    {
        if (raycastManager == null)
        {
            return;
        }

        var hits = new List<ARRaycastHit>();
        if (!raycastManager.Raycast(screenPosition, hits, TrackableType.PlaneWithinPolygon))
        {
            return;
        }

        Pose hitPose = hits[0].pose;
        Vector3 cameraForward = arCamera != null ? arCamera.transform.forward : Vector3.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.0001f)
        {
            cameraForward = Vector3.forward;
        }

        Vector3 firePosition = hitPose.position;
        if (arCamera != null)
        {
            firePosition = arCamera.transform.position + cameraForward.normalized * fireDistanceForward;
            firePosition.y = hitPose.position.y;
        }

        Pose firePose = new Pose(firePosition, Quaternion.LookRotation(-cameraForward.normalized, Vector3.up));
        if (!firePlacer.PlaceObjectAt(firePose))
        {
            return;
        }

        Vector3 wallMountPosition = firePosition - cameraForward.normalized * (fireDistanceForward + wallMountDistanceBehind);
        wallMountPosition = ResolvePlanePosition(wallMountPosition, hitPose.position.y + fallbackPlaneYOffset);

        Vector3 lookDirection = firePosition - wallMountPosition;
        lookDirection.y = 0f;
        Quaternion wallMountRotation = lookDirection.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
            : Quaternion.identity;

        if (!wallMountSpawner.SpawnAtPose(wallMountPosition, wallMountRotation))
        {
            return;
        }

        hasPlaced = true;
        ShowStartMessage();
        SetPlaneVisualization(false);
    }

    private void SetPlaneVisualization(bool isVisible)
    {
        if (planeManager == null)
        {
            return;
        }

        planeManager.enabled = isVisible;

        foreach (ARPlane plane in planeManager.trackables)
        {
            if (plane != null)
            {
                plane.gameObject.SetActive(isVisible);
            }
        }
    }

    private void ShowStartMessage()
    {
        if (textDialogue == null)
        {
            return;
        }

        if (startMessageRoutine != null)
        {
            StopCoroutine(startMessageRoutine);
        }

        textDialogue.text = startMessage;
        textDialogue.gameObject.SetActive(true);
        startMessageRoutine = StartCoroutine(HideStartMessageAfterDelay());
    }

    private IEnumerator HideStartMessageAfterDelay()
    {
        float waitSeconds = Mathf.Max(0f, startMessageDuration);
        if (waitSeconds > 0f)
        {
            yield return new WaitForSeconds(waitSeconds);
        }

        HideDialogueMessage();
        startMessageRoutine = null;
    }

    private Vector3 ResolvePlanePosition(Vector3 desiredPosition, float fallbackY)
    {
        if (raycastManager == null)
        {
            return new Vector3(desiredPosition.x, fallbackY, desiredPosition.z);
        }

        var hits = new List<ARRaycastHit>();
        Vector3 castOrigin = desiredPosition + Vector3.up * 1.5f;
        Ray ray = new Ray(castOrigin, Vector3.down);
        if (raycastManager.Raycast(ray, hits, TrackableType.PlaneWithinPolygon))
        {
            return hits[0].pose.position;
        }

        return new Vector3(desiredPosition.x, fallbackY, desiredPosition.z);
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
