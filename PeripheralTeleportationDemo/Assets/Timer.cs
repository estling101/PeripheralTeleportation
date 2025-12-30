using UnityEngine;
using Unity.XR.CoreUtils;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance;

    private float startTime;
    private bool timing;

    public DiscomfortUI discomfortUI; // reference to panel with ui
    public GameObject panel; 
    public XROrigin xrRig;           // reference to rig to now pos

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

            {
            // position panel 1.5 meters in front of XR Rig camera
            Vector3 forward = xrRig.transform.forward;
            Vector3 spawnPos = xrRig.transform.position + forward * 3f;

            discomfortUI.Show(spawnPos);
            }

            float totalTime = Time.time - startTime;
            timing = false;

            Debug.Log("Timer stopped");
            Debug.Log("Total time: " + totalTime.ToString("F2") + " seconds");

    
        
        }
    }
}
