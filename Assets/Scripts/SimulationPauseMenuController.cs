using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


public class SimulationPauseMenuController : MonoBehaviour
{
    [Header("Referensi Timer")]
    [Tooltip("ARSpawnCountdownTimer yang ada di scene Core.")]
    [SerializeField] private ARSpawnCountdownTimer timer;

    [Header("Tombol Pause (tampil di HUD)")]
    [Tooltip("Tombol pause yang tampil selama simulasi berjalan.")]
    [SerializeField] private GameObject pauseButton;

    [Header("Panel Pause Menu")]
    [Tooltip("Panel overlay yang tampil ketika game dijeda.")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button backToMainMenuButton;

    [Header("Panel Konfirmasi Back to Main Menu")]
    [Tooltip("Panel konfirmasi sebelum kembali ke main menu.")]
    [SerializeField] private GameObject confirmPanel;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;

    [Header("Pengaturan Scene")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ── State internal ──────────────────────────────────────────────────────────
    private bool isPaused;

    // ── Unity Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        // Pastikan panel default tertutup saat game mulai.
        SetActive(pauseMenuPanel, false);
        SetActive(confirmPanel, false);

        // Daftarkan listener button.
        AddListener(resumeButton, OnResumeClicked);
        AddListener(backToMainMenuButton, OnBackToMainMenuClicked);
        AddListener(confirmYesButton, OnConfirmYesClicked);
        AddListener(confirmNoButton, OnConfirmNoClicked);
    }

    private void Update()
    {
        // Tombol pause hanya tampil saat simulasi sedang aktif berjalan dan tidak di-pause.
        if (pauseButton != null)
        {
            bool simulationRunning = timer != null && timer.IsRunning && !timer.IsFinished;
            pauseButton.SetActive(simulationRunning);
        }
    }

    private void OnDestroy()
    {
        // Pastikan timeScale kembali normal jika object dihancurkan saat pause.
        if (isPaused)
        {
            Time.timeScale = 1f;
        }
    }

    // ── Public: dipanggil oleh onClick PauseButton di Inspector / Button ────────

    /// <summary>Dipanggil oleh PauseButton.onClick di Inspector.</summary>
    public void OnPauseClicked()
    {
        if (isPaused)
        {
            return;
        }

        Pause();
    }

    // ── Logika Pause / Resume ───────────────────────────────────────────────────

    private void Pause()
    {
        isPaused = true;

        // Bekukan semua animasi, physics, dan countdown timer.
        Time.timeScale = 0f;

        if (timer != null)
        {
            timer.PauseCountdown();
        }

        // Tampilkan pause menu, sembunyikan pause button dari HUD.
        SetActive(pauseButton, false);
        SetActive(pauseMenuPanel, true);
        SetActive(confirmPanel, false);
    }

    private void Resume()
    {
        isPaused = false;

        // Pulihkan waktu normal.
        Time.timeScale = 1f;

        if (timer != null)
        {
            timer.ResumeCountdown();
        }

        // Sembunyikan semua overlay.
        SetActive(pauseMenuPanel, false);
        SetActive(confirmPanel, false);
    }

    // ── Callback Button ─────────────────────────────────────────────────────────

    private void OnResumeClicked()
    {
        Resume();
    }

    private void OnBackToMainMenuClicked()
    {
        // Tampilkan panel konfirmasi, tapi tetap pause (jangan resume dulu).
        SetActive(confirmPanel, true);
    }

    private void OnConfirmYesClicked()
    {
        // Pastikan timeScale normal sebelum load scene baru.
        Time.timeScale = 1f;
        isPaused = false;

        SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
    }

    private void OnConfirmNoClicked()
    {
        // Tutup konfirmasi, kembali ke pause menu.
        SetActive(confirmPanel, false);
    }

    // ── Helper ──────────────────────────────────────────────────────────────────

    private static void SetActive(GameObject obj, bool active)
    {
        if (obj != null)
        {
            obj.SetActive(active);
        }
    }

    private static void AddListener(Button btn, UnityEngine.Events.UnityAction action)
    {
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }
    }
}
