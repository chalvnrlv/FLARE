using UnityEngine;

public class ARLevelTimerConfig : MonoBehaviour
{
    [SerializeField] private float countdownSeconds = 60f;

    private void Start()
    {
        ARSpawnCountdownTimer timer = FindFirstObjectByType<ARSpawnCountdownTimer>();
        if (timer == null)
        {
            return;
        }

        timer.SetCountdownSeconds(countdownSeconds);
    }
}
