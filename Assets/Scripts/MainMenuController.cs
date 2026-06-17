using UnityEngine;
using UnityEngine.SceneManagement;

namespace Flare.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panel Panduan")]
        [Tooltip("Drag PanduanPanel GameObject (yang memiliki komponen PanduanPanelController) ke sini.")]
        public PanduanPanelController panduanPanel;

        public void StartSimulasi()
        {
            RecapManager.RequestNewRun();
            SceneManager.LoadScene("Core");
        }

        // public void StartMinigame()
        // {
        //     Debug.Log("Memulai mode minigame...");
        // }
        // public void OpenPustaka()
        // {
        //     Debug.Log("Membuka Pustaka Edukasi Kebakaran...");
        // }

        /// <summary>
        /// Dipanggil oleh panduanButton.onClick ─ membuka panel panduan.
        /// </summary>
        public void OpenPanduan()
        {
            if (panduanPanel != null)
                panduanPanel.ShowPanel();
            else
                Debug.LogWarning("[MainMenu] panduanPanel belum di-assign di Inspector!", this);
        }

        public void Quit()
        {
            Debug.Log("Keluar dari aplikasi FLARE.");
            Application.Quit();
        }
    }
}