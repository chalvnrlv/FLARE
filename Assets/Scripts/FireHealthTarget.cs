using System.Collections;
using UnityEngine;

public class FireHealthTarget : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 50f;

    [Header("Visual Shrink")]
    [SerializeField] private float minScaleMultiplierAtZeroHealth = 0.2f;
    [SerializeField] private float shrinkSmoothing = 8f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool enableParticleCollisionDamage = false;
    [SerializeField] private float currentHealth;
    [SerializeField] private float healthRatio;

    [Header("Layer")]
    [SerializeField] private string fireHitboxLayerName = "FireHitbox";

    [SerializeField] private ParticleSystem fireParticle;
    [SerializeField] private float fallbackColliderRadius = 0.55f;
    [SerializeField] private float minimumRuntimeColliderRadius = 0.25f;

    [Header("Audio")]
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private AudioClip fireLoopClip;
    [SerializeField] private float fireLoopVolume = 1f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    private Vector3 initialLocalScale;
    private float targetHealthRatio = 1f;
    private float displayedHealthRatio = 1f;
    private int lastDamageFrame = -1;
    private float nextCollisionLogTime;
    private Coroutine audioFadeRoutine;

    public bool IsExtinguished => currentHealth <= 0f;

    private void Awake()
    {
        ResolveReferences();
        AssignFireHitboxLayer();
        EnsureCollider();
        CacheInitialScale();
        PrepareFireAudio();
        StartFireAudio();

        currentHealth = maxHealth;
        targetHealthRatio = 1f;
        displayedHealthRatio = 1f;
        healthRatio = 1f;
        ApplyVisualsImmediate();
    }

    private void Update()
    {
        if (displayedHealthRatio == targetHealthRatio)
        {
            return;
        }

        displayedHealthRatio = Mathf.Lerp(displayedHealthRatio, targetHealthRatio, Time.deltaTime * Mathf.Max(0f, shrinkSmoothing));
        if (Mathf.Abs(displayedHealthRatio - targetHealthRatio) < 0.001f)
        {
            displayedHealthRatio = targetHealthRatio;
        }

        ApplyVisualScale(displayedHealthRatio);
    }

    public void Initialize(float health)
    {
        maxHealth = Mathf.Max(0f, health);
        currentHealth = maxHealth;

        ResolveReferences();
        AssignFireHitboxLayer();
        EnsureCollider();
        CacheInitialScale();

        PrepareFireAudio();
        StartFireAudio();

        targetHealthRatio = 1f;
        displayedHealthRatio = 1f;
        healthRatio = 1f;
        ApplyVisualsImmediate();

        LogDebug("Initialized with health=" + currentHealth);
    }

    private void AssignFireHitboxLayer()
    {
        if (string.IsNullOrEmpty(fireHitboxLayerName))
        {
            return;
        }

        int layer = LayerMask.NameToLayer(fireHitboxLayerName);
        if (layer < 0)
        {
            return;
        }

        gameObject.layer = layer;
    }

    private void ResolveReferences()
    {
        if (fireParticle == null)
        {
            fireParticle = GetComponent<ParticleSystem>();
        }

        if (fireParticle == null)
        {
            fireParticle = GetComponentInChildren<ParticleSystem>(true);
        }

        if (fireAudioSource == null)
        {
            fireAudioSource = GetComponent<AudioSource>();
        }

        if (fireAudioSource == null)
        {
            fireAudioSource = GetComponentInChildren<AudioSource>(true);
        }
    }

    private void EnsureCollider()
    {
        float desiredRadius = Mathf.Max(fallbackColliderRadius, minimumRuntimeColliderRadius);
        bool shouldUseTrigger = !enableParticleCollisionDamage;

        Collider existingCollider = GetComponent<Collider>();
        if (existingCollider is SphereCollider existingSphere)
        {
            existingSphere.radius = Mathf.Max(existingSphere.radius, desiredRadius);
            existingSphere.isTrigger = shouldUseTrigger;
            return;
        }

        if (existingCollider != null)
        {
            existingCollider.isTrigger = shouldUseTrigger;
            return;
        }

        SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
        sphereCollider.radius = desiredRadius;
        sphereCollider.isTrigger = shouldUseTrigger;
    }

    private void CacheInitialScale()
    {
        initialLocalScale = transform.localScale;
    }

    private void ApplyVisualsImmediate()
    {
        ApplyVisualScale(displayedHealthRatio);
    }

    private void ApplyVisualScale(float ratio)
    {
        float clampedRatio = Mathf.Clamp01(ratio);
        float scaleMultiplier = Mathf.Lerp(minScaleMultiplierAtZeroHealth, 1f, clampedRatio);
        transform.localScale = initialLocalScale * scaleMultiplier;
    }

    private void OnParticleCollision(GameObject other)
    {
        if (!enableParticleCollisionDamage)
        {
            return;
        }

        if (enableDebugLogs && Time.time >= nextCollisionLogTime)
        {
            nextCollisionLogTime = Time.time + 0.35f;
            string senderName = other != null ? other.name : "null";
            LogDebug("OnParticleCollision received from " + senderName);
        }

        if (currentHealth <= 0f)
        {
            return;
        }

        if (lastDamageFrame == Time.frameCount)
        {
            return;
        }

        ARFireExtinguisher extinguisher = ResolveExtinguisherFromCollision(other);
        if (extinguisher == null || !extinguisher.IsActivelySpraying)
        {
            if (enableDebugLogs && Time.time >= nextCollisionLogTime)
            {
                nextCollisionLogTime = Time.time + 0.35f;
                LogDebug("Collision ignored: extinguisher missing or not actively spraying");
            }
            return;
        }

        float damage = extinguisher.DepletionPerSecond * Time.deltaTime;
        ApplyDamage(damage);
        lastDamageFrame = Time.frameCount;
    }

    public void ApplyDamageFromHitbox(float damagePerSecond, float deltaTime)
    {
        if (currentHealth <= 0f || damagePerSecond <= 0f || deltaTime <= 0f)
        {
            return;
        }

        float damageAmount = damagePerSecond * deltaTime;
        ApplyDamage(damageAmount);
    }

    private ARFireExtinguisher ResolveExtinguisherFromCollision(GameObject collisionSender)
    {
        if (collisionSender != null)
        {
            ARFireExtinguisher fromSender = collisionSender.GetComponent<ARFireExtinguisher>();
            if (fromSender != null)
            {
                return fromSender;
            }

            ARFireExtinguisher fromParent = collisionSender.GetComponentInParent<ARFireExtinguisher>();
            if (fromParent != null)
            {
                return fromParent;
            }
        }

        return null;
    }

    private void ApplyDamage(float damageAmount)
    {
        if (damageAmount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
        targetHealthRatio = maxHealth <= 0f ? 0f : currentHealth / maxHealth;
        healthRatio = targetHealthRatio;

        LogDebug("Damage applied=" + damageAmount.ToString("F3") + " currentHealth=" + currentHealth.ToString("F2") + " ratio=" + healthRatio.ToString("F2"));

        if (currentHealth > 0f)
        {
            return;
        }

        displayedHealthRatio = 0f;
        ApplyVisualsImmediate();

        if (fireParticle != null)
        {
            var emission = fireParticle.emission;
            emission.enabled = false;
            fireParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        FadeOutFireAudio();

        LogDebug("Fire extinguished");
    }

    public void ExtinguishNow()
    {
        if (currentHealth <= 0f)
        {
            return;
        }

        currentHealth = 0f;
        targetHealthRatio = 0f;
        healthRatio = 0f;
        displayedHealthRatio = 0f;
        ApplyVisualsImmediate();

        if (fireParticle != null)
        {
            var emission = fireParticle.emission;
            emission.enabled = false;
            fireParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        FadeOutFireAudio();

        LogDebug("Fire extinguished by ExtinguishNow");
    }

    private void PrepareFireAudio()
    {
        if (fireAudioSource == null)
        {
            return;
        }

        if (fireLoopClip != null)
        {
            fireAudioSource.clip = fireLoopClip;
        }

        fireAudioSource.loop = true;
        fireAudioSource.volume = Mathf.Max(0f, fireLoopVolume);
        fireAudioSource.playOnAwake = false;
    }

    private void StartFireAudio()
    {
        if (fireAudioSource == null || fireAudioSource.clip == null)
        {
            return;
        }

        if (!fireAudioSource.isPlaying)
        {
            fireAudioSource.volume = Mathf.Max(0f, fireLoopVolume);
            fireAudioSource.Play();
        }
    }

    private void FadeOutFireAudio()
    {
        if (fireAudioSource == null)
        {
            return;
        }

        if (audioFadeRoutine != null)
        {
            StopCoroutine(audioFadeRoutine);
        }

        audioFadeRoutine = StartCoroutine(FadeOutAudioRoutine());
    }

    private IEnumerator FadeOutAudioRoutine()
    {
        if (fireAudioSource == null)
        {
            yield break;
        }

        float startVolume = fireAudioSource.volume;
        float duration = Mathf.Max(0.01f, fadeOutDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fireAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        fireAudioSource.volume = 0f;
        fireAudioSource.Stop();
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
        {
            return;
        }

        Debug.Log("[FireHealthTarget] " + message, this);
    }
}
