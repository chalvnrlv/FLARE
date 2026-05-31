using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlaceCube : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;
    [SerializeField] private GameObject placementPrefab;
    [SerializeField] private bool enableFireHealthLogic = true;
    [SerializeField] private bool useRandomPlacement = true;
    [SerializeField] private float minDistanceFromPlayer = 0.6f;
    [SerializeField] private float maxDistanceFromPlayer = 1.5f;
    [SerializeField] private int maxPlacementAttempts = 12;
    [SerializeField] private float placementCheckInterval = 0.2f;

    private float nextPlacementCheckTime;
    private bool hasPlaced;
    private GameObject placedObject;

    public bool HasPlacedObject => hasPlaced;
    public GameObject PlacedObject => placedObject;

    public void SetUseRandomPlacement(bool enabled)
    {
        useRandomPlacement = enabled;
    }

    public bool PlaceObjectAt(Pose pose)
    {
        if (placementPrefab == null || hasPlaced)
        {
            return false;
        }

        placedObject = Instantiate(placementPrefab, pose.position, pose.rotation);
        SceneManager.MoveGameObjectToScene(placedObject, gameObject.scene);

        if (enableFireHealthLogic)
        {
            AttachFireHealthTarget(placedObject);
        }

        hasPlaced = true;
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

    // Update is called once per frame
    void Update()
    {
        if (!useRandomPlacement || !raycastManager || hasPlaced) return;
        if (placementPrefab == null) return;
        if (Time.time < nextPlacementCheckTime) return;
        if (!HasTrackedPlane()) return;

        nextPlacementCheckTime = Time.time + placementCheckInterval;
        TryPlaceObjectRandomlyAroundPlayer();
    }

    private bool HasTrackedPlane()
    {
        if (planeManager == null) return true;
        return planeManager.trackables.count > 0;
    }

    private void TryPlaceObjectRandomlyAroundPlayer()
    {
        var rayHits = new List<ARRaycastHit>();
        Vector3 playerPosition = arCamera ? arCamera.transform.position : transform.position;

        for (int i = 0; i < maxPlacementAttempts; i++)
        {
            float randomAngle = Random.Range(0f, Mathf.PI * 2f);
            float randomDistance = Random.Range(minDistanceFromPlayer, maxDistanceFromPlayer);
            Vector3 offset = new Vector3(Mathf.Cos(randomAngle), 0f, Mathf.Sin(randomAngle)) * randomDistance;

            // Cast from above down to ensure we land on detected plane geometry.
            Vector3 castOrigin = playerPosition + offset + Vector3.up * 1.5f;
            Ray downRay = new Ray(castOrigin, Vector3.down);

            rayHits.Clear();
            if (!raycastManager.Raycast(downRay, rayHits, TrackableType.PlaneWithinPolygon))
            {
                continue;
            }

            Pose pose = rayHits[0].pose;
            placedObject = Instantiate(placementPrefab, pose.position, pose.rotation);
            SceneManager.MoveGameObjectToScene(placedObject, gameObject.scene);

            if (enableFireHealthLogic)
            {
                AttachFireHealthTarget(placedObject);
            }

            hasPlaced = true;
            return;
        }
    }

    private void AttachFireHealthTarget(GameObject spawnedObject)
    {
        if (spawnedObject == null)
        {
            Debug.LogWarning("[ARPlaceCube] Cannot attach FireHealthTarget because spawnedObject is null.");
            return;
        }

        ParticleSystem fireParticle = FindFireParticle(spawnedObject);
        if (fireParticle == null)
        {
            Debug.LogWarning("[ARPlaceCube] No ParticleSystem found for fire target on spawned object " + spawnedObject.name + ".");
            return;
        }

        FireHealthTarget healthTarget = fireParticle.GetComponent<FireHealthTarget>();
        if (healthTarget == null)
        {
            healthTarget = fireParticle.gameObject.AddComponent<FireHealthTarget>();
            Debug.Log("[ARPlaceCube] Added FireHealthTarget to " + fireParticle.gameObject.name + ".", fireParticle.gameObject);
        }
        else
        {
            Debug.Log("[ARPlaceCube] FireHealthTarget already exists on " + fireParticle.gameObject.name + ".", fireParticle.gameObject);
        }

        healthTarget.Initialize(50f);
        Debug.Log("[ARPlaceCube] FireHealthTarget initialized with 50 HP on " + fireParticle.gameObject.name + ".", fireParticle.gameObject);
    }

    private ParticleSystem FindFireParticle(GameObject root)
    {
        ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
        if (particles == null || particles.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < particles.Length; i++)
        {
            string objectName = particles[i].gameObject.name;
            if (!string.IsNullOrEmpty(objectName) && objectName.ToLowerInvariant().Contains("fire"))
            {
                return particles[i];
            }
        }

        return particles[0];
    }

    private void Reset()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        planeManager = GetComponent<ARPlaneManager>();
        arCamera = Camera.main;
    }
}