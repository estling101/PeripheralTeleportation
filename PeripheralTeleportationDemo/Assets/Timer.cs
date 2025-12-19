using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    private float startTime;
    private bool timing;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void CoinCollected(bool isFirstCoin, bool isLastCoin)
    {
        // Start timer only on the first coin
        if (isFirstCoin && !timing)
        {
            timing = true;
            startTime = Time.time;
            Debug.Log("Timer started");
        }

        // Stop timer only on the last coin
        if (isLastCoin && timing)
        {
            float totalTime = Time.time - startTime;
            timing = false;

            Debug.Log("Timer stopped");
            Debug.Log("Total time: " + totalTime.ToString("F2") + " seconds");
        }
    }
}
