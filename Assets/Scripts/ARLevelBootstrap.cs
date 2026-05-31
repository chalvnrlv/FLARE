using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class ARLevelBootstrap : MonoBehaviour
{
    [SerializeField] private bool enableDebugLogs = true;

    private void Start()
    {
        WireLevelReferences();
    }

    private void WireLevelReferences()
    {
        ARRaycastManager raycastManager = FindFirstObjectByType<ARRaycastManager>();
        ARPlaneManager planeManager = FindFirstObjectByType<ARPlaneManager>();
        Camera arCamera = Camera.main;

        ARPlaceCube firePlacer = FindFirstObjectByType<ARPlaceCube>();

        ARPlaceCube[] firePlacers = FindObjectsByType<ARPlaceCube>(FindObjectsSortMode.None);
        for (int i = 0; i < firePlacers.Length; i++)
        {
            firePlacers[i].Configure(raycastManager, planeManager, arCamera);
        }

        ARExtinguisherSpawner[] extinguisherSpawners = FindObjectsByType<ARExtinguisherSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < extinguisherSpawners.Length; i++)
        {
            extinguisherSpawners[i].Configure(raycastManager, planeManager, arCamera, firePlacer);
        }

        ARBucketSpawner[] bucketSpawners = FindObjectsByType<ARBucketSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < bucketSpawners.Length; i++)
        {
            bucketSpawners[i].Configure(raycastManager, planeManager, arCamera, firePlacer);
        }

        ARWallMountSpawner[] wallMountSpawners = FindObjectsByType<ARWallMountSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < wallMountSpawners.Length; i++)
        {
            wallMountSpawners[i].Configure(raycastManager, planeManager, arCamera);
        }

        if (enableDebugLogs)
        {
            Debug.Log("[ARLevelBootstrap] Wired AR references for level scene.", this);
        }
    }
}
