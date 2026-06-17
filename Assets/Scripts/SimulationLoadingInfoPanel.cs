using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SimulationLoadingInfoPanel : MonoBehaviour
{
    [Header("Referensi Timer Simulasi")]
    [Tooltip("ARSpawnCountdownTimer di scene Core.")]
    [SerializeField] private ARSpawnCountdownTimer timer;

    [Header("UI Panel")]
    [Tooltip("Panel yang tampil saat loading. Default: active di Inspector.")]
    [SerializeField] private GameObject loadingInfoPanel;

    [Tooltip("Text informasi yang ditampilkan. Kosongkan jika ingin menggunakan text bawaan di scene.")]
    [SerializeField] private Text infoText;

    [Header("Konten & Durasi")]
    [Tooltip("Pesan informasi yang ditampilkan kepada user.")]
    [SerializeField] private string infoMessage = "Pastikan area simulasi kurang lebih memiliki luasan 2x2 meter";

    [Tooltip("Minimum durasi panel ditampilkan (detik), berjalan bersamaan dengan loading.")]
    [SerializeField] private float minimumDisplaySeconds = 2f;


    private void Awake()
    {
        // Blokir auto-start simulasi selama panel belum selesai.
        if (timer != null)
        {
            timer.SetRequireManualStart(true);
        }

        // Tampilkan panel dan set pesan.
        ShowPanel();

        StartCoroutine(LoadingInfoRoutine());
    }

    // ── Coroutine ─────────────────────────────────────────────────────────

    private IEnumerator LoadingInfoRoutine()
    {
        float elapsed = 0f;

        while (elapsed < minimumDisplaySeconds || IsLevelStillLoading())
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Sembunyikan panel dan buka gerbang simulasi.
        HidePanel();

        if (timer != null)
        {
            timer.SetRequireManualStart(false);
        }
    }


    private void ShowPanel()
    {
        if (loadingInfoPanel != null)
        {
            loadingInfoPanel.SetActive(true);
        }

        if (infoText != null && !string.IsNullOrEmpty(infoMessage))
        {
            infoText.text = infoMessage;
        }
    }

    private void HidePanel()
    {
        if (loadingInfoPanel != null)
        {
            loadingInfoPanel.SetActive(false);
        }
    }

    private bool IsLevelStillLoading()
    {
        if (ARLevelLoader.Instance == null)
        {
            return false;
        }

        return ARLevelLoader.Instance.IsLoading;
    }
}
