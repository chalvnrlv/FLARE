using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARExtinguisherSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ARPlaceCube fireSourcePlacer;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;

    [Header("Spawn")]
    [SerializeField] private GameObject extinguisherPrefab;
    [SerializeField] private float minDistanceFromPlayer = 0.8f;
    [SerializeField] private float maxDistanceFromPlayer = 2.0f;
    [SerializeField] private float minDistanceFromFireSource = 1.2f;
    [SerializeField] private int maxPlacementAttempts = 16;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private Vector3 spawnRotationEuler = new Vector3(0f, 0f, 90f);
    [SerializeField] private float spawnYOffset = 0.02f;
    [SerializeField] private bool autoAdjustToPlane = true;
    [SerializeField] private float maxAutoLift = 0.5f;

    [Header("Mount Spawn")]
    [SerializeField] private string mountRootName = "wall_mount";
    [SerializeField] private string mountChildName = "Apar_mount";
    [SerializeField] private Transform mountOverride;
    [SerializeField] private Transform spawnOffset;
    [SerializeField] private bool parentToMount = true;
    [SerializeField] private bool allowRandomFallback = false;
    [SerializeField] private bool applyMountedScale = false;
    [SerializeField] private Vector3 mountedLocalScale = Vector3.one;

    private bool hasSpawned;
    private float nextCheckTime;

    public bool HasSpawnedObject => hasSpawned;

    public bool RespawnOnMount(bool destroyExisting)
    {
        if (destroyExisting)
        {
            DestroyExistingMounted();
        }

        hasSpawned = false;
        return TrySpawnOnMount();
    }

    public void Configure(ARRaycastManager newRaycastManager, ARPlaneManager newPlaneManager, Camera newCamera, ARPlaceCube newFireSourcePlacer)
    {
        if (newRaycastManager != null)
        {
            raycastManager = newRaycastManager;
        }

        if (newPlaneManager != null)
        {
            planeManager = newPlaneManager;
        }

        if (newCamera != null)
        {
            arCamera = newCamera;
        }

        if (newFireSourcePlacer != null)
        {
            fireSourcePlacer = newFireSourcePlacer;
        }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (raycastManager == null)
        {
            raycastManager = GetComponent<ARRaycastManager>();
        }

        if (planeManager == null)
        {
            planeManager = GetComponent<ARPlaneManager>();
        }

        if (fireSourcePlacer == null)
        {
            fireSourcePlacer = GetComponent<ARPlaceCube>();
        }

        if (raycastManager == null)
        {
            raycastManager = FindFirstObjectByType<ARRaycastManager>();
        }

        if (planeManager == null)
        {
            planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        if (fireSourcePlacer == null)
        {
            fireSourcePlacer = FindFirstObjectByType<ARPlaceCube>();
        }

        if (arCamera == null)
        {
            arCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (hasSpawned || Time.time < nextCheckTime)
        {
            return;
        }

        if (raycastManager == null || extinguisherPrefab == null)
        {
            return;
        }

        if (fireSourcePlacer != null && !fireSourcePlacer.HasPlacedObject)
        {
            return;
        }

        if (planeManager != null && planeManager.trackables.count == 0)
        {
            return;
        }

        nextCheckTime = Time.time + checkInterval;
        if (TrySpawnOnMount())
        {
            return;
        }

        if (allowRandomFallback)
        {
            TrySpawnRandomlyAroundPlayer();
        }
    }

    private bool TrySpawnOnMount()
    {
        Transform mount = ResolveMountTransform();
        if (mount == null)
        {
            return false;
        }

        GameObject spawnedObject = Instantiate(extinguisherPrefab);
        SceneManager.MoveGameObjectToScene(spawnedObject, gameObject.scene);
        ApplyMountPose(mount, spawnedObject);

        hasSpawned = true;
        return true;
    }

    private Transform ResolveMountTransform()
    {
        if (mountOverride != null)
        {
            return mountOverride;
        }

        GameObject root = FindSceneObjectByNameIncludingInactive(mountRootName);
        if (root == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(mountChildName))
        {
            return root.transform;
        }

        GameObject child = SearchHierarchyForName(root, mountChildName);
        return child != null ? child.transform : root.transform;
    }

    private void ApplyMountPose(Transform mount, GameObject spawnedObject)
    {
        if (mount == null || spawnedObject == null)
        {
            return;
        }

        if (parentToMount)
        {
            spawnedObject.transform.SetParent(mount, false);
            spawnedObject.transform.localPosition = spawnOffset != null ? spawnOffset.localPosition : Vector3.zero;
            spawnedObject.transform.localRotation = spawnOffset != null ? spawnOffset.localRotation : Quaternion.identity;
            if (applyMountedScale)
            {
                spawnedObject.transform.localScale = mountedLocalScale;
            }
        }
        else
        {
            Vector3 offset = spawnOffset != null ? spawnOffset.localPosition : Vector3.zero;
            Quaternion rotationOffset = spawnOffset != null ? spawnOffset.localRotation : Quaternion.identity;
            spawnedObject.transform.position = mount.position + offset;
            spawnedObject.transform.rotation = mount.rotation * rotationOffset;
        }
    }

    private void DestroyExistingMounted()
    {
        Transform mount = ResolveMountTransform();
        if (mount == null)
        {
            return;
        }

        for (int i = mount.childCount - 1; i >= 0; i--)
        {
            Transform child = mount.GetChild(i);
            if (child != null && child.GetComponentInChildren<ARFireExtinguisher>(true) != null)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void TrySpawnRandomlyAroundPlayer()
    {
        Vector3 center = arCamera != null ? arCamera.transform.position : transform.position;
        var hits = new List<ARRaycastHit>();

        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            Vector3 castOrigin = center + offset + Vector3.up * 1.5f;
            Ray ray = new Ray(castOrigin, Vector3.down);

            hits.Clear();
            if (!raycastManager.Raycast(ray, hits, TrackableType.PlaneWithinPolygon))
            {
                continue;
            }

            Pose pose = hits[0].pose;
            if (!IsFarEnoughFromFireSource(pose.position))
            {
                continue;
            }

            Quaternion spawnRotation = Quaternion.Euler(spawnRotationEuler);
            Vector3 spawnPosition = pose.position + Vector3.up * spawnYOffset;
            GameObject spawnedObject = Instantiate(extinguisherPrefab, spawnPosition, spawnRotation);
            SceneManager.MoveGameObjectToScene(spawnedObject, gameObject.scene);

            if (autoAdjustToPlane)
            {
                PlaceObjectOnTopOfPlane(spawnedObject, pose.position.y + spawnYOffset);
            }

            hasSpawned = true;
            return;
        }
    }

    private void PlaceObjectOnTopOfPlane(GameObject spawnedObject, float planeY)
    {
        if (spawnedObject == null)
        {
            return;
        }

        Renderer[] renderers = spawnedObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Vector3 fallbackPosition = spawnedObject.transform.position;
            fallbackPosition.y = planeY;
            spawnedObject.transform.position = fallbackPosition;
            return;
        }

        float minY = float.MaxValue;
        for (int i = 0; i < renderers.Length; i++)
        {
            minY = Mathf.Min(minY, renderers[i].bounds.min.y);
        }

        float lift = planeY - minY;
        if (lift > 0f && lift <= maxAutoLift)
        {
            spawnedObject.transform.position += new Vector3(0f, lift, 0f);
        }
        else if (lift > maxAutoLift)
        {
            Vector3 safePosition = spawnedObject.transform.position;
            safePosition.y = planeY;
            spawnedObject.transform.position = safePosition;
        }
    }

    private bool IsFarEnoughFromFireSource(Vector3 candidatePosition)
    {
        if (fireSourcePlacer == null || fireSourcePlacer.PlacedObject == null)
        {
            return true;
        }

        float distanceToFireSource = Vector3.Distance(candidatePosition, fireSourcePlacer.PlacedObject.transform.position);
        return distanceToFireSource >= minDistanceFromFireSource;
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

        if (IsNameMatch(obj.name, targetName))
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

    private bool IsNameMatch(string actualName, string targetName)
    {
        if (string.IsNullOrEmpty(actualName) || string.IsNullOrEmpty(targetName))
        {
            return false;
        }

        if (actualName == targetName)
        {
            return true;
        }

        if (actualName.StartsWith(targetName))
        {
            return true;
        }

        return actualName.Contains(targetName);
    }
}
