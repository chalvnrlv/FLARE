using UnityEngine;
using UnityEngine.SceneManagement; 

namespace Flare.UI
{
    public class MainMenuController : MonoBehaviour
    {
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

        public void Quit()
        {
            Debug.Log("Keluar dari aplikasi FLARE.");
            Application.Quit();
        }
    }
}