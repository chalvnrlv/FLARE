using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARWallMountSpawner : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private ARPlaceCube fireSourcePlacer;
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;

    [Header("Spawn")]
    [SerializeField] private GameObject wallMountPrefab;
    [SerializeField] private float minDistanceFromPlayer = 0.8f;
    [SerializeField] private float maxDistanceFromPlayer = 2.0f;
    [SerializeField] private float minDistanceFromFireSource = 1.2f;
    [SerializeField] private int maxPlacementAttempts = 16;
    [SerializeField] private float checkInterval = 0.25f;
    [SerializeField] private Vector3 spawnRotationEuler = Vector3.zero;
    [SerializeField] private float spawnYOffset = 0.02f;
    [SerializeField] private bool autoAdjustToPlane = true;
    [SerializeField] private float maxAutoLift = 0.5f;
    [SerializeField] private bool faceCameraOnSpawn = true;
    [SerializeField] private bool useRandomPlacement = true;

    private bool hasSpawned;
    private float nextCheckTime;

    public bool HasSpawnedObject => hasSpawned;

    public void SetUseRandomPlacement(bool enabled)
    {
        useRandomPlacement = enabled;
    }

    public bool SpawnAtPose(Vector3 position, Quaternion rotation)
    {
        if (wallMountPrefab == null || hasSpawned)
        {
            return false;
        }

        GameObject spawnedObject = Instantiate(wallMountPrefab, position, rotation);
        SceneManager.MoveGameObjectToScene(spawnedObject, gameObject.scene);

        if (autoAdjustToPlane)
        {
            PlaceObjectOnTopOfPlane(spawnedObject, position.y);
        }

        hasSpawned = true;
        return true;
    }

    public void Configure(ARRaycastManager newRaycastManager, ARPlaneManager newPlaneManager, Camera newCamera)
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
        if (!useRandomPlacement || hasSpawned || Time.time < nextCheckTime)
        {
            return;
        }

        if (raycastManager == null || wallMountPrefab == null)
        {
            return;
        }

        if (planeManager != null && planeManager.trackables.count == 0)
        {
            return;
        }

        nextCheckTime = Time.time + checkInterval;
        TrySpawnRandomlyAroundPlayer();
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
            Vector3 spawnPosition = pose.position + Vector3.up * spawnYOffset;
            Quaternion spawnRotation = ResolveSpawnRotation(spawnPosition);
            GameObject spawnedObject = Instantiate(wallMountPrefab, spawnPosition, spawnRotation);
            SceneManager.MoveGameObjectToScene(spawnedObject, gameObject.scene);

            if (autoAdjustToPlane)
            {
                PlaceObjectOnTopOfPlane(spawnedObject, pose.position.y + spawnYOffset);
            }

            hasSpawned = true;
            return;
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

    private Quaternion ResolveSpawnRotation(Vector3 spawnPosition)
    {
        if (!faceCameraOnSpawn || arCamera == null)
        {
            return Quaternion.Euler(spawnRotationEuler);
        }

        Vector3 toCamera = arCamera.transform.position - spawnPosition;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 0.0001f)
        {
            return Quaternion.Euler(spawnRotationEuler);
        }

        return Quaternion.LookRotation(toCamera.normalized, Vector3.up);
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
}
