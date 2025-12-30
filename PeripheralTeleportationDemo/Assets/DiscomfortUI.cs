using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DiscomfortUI : MonoBehaviour
{
    public Slider slider;
    public TMP_Text valueText;

    [Header("XR Pointer")]
    public GameObject rightController; //assign contoller here

    [Header("Panel")]
    public GameObject panel;

    [Header("Follow Camera")]
    public float distanceForward = 2.0f;
    public float heightOffset = 0.5f;

    private bool isVisible;


    

    

    private void Awake()
    {
        gameObject.SetActive(false);
        if (slider != null)
            slider.onValueChanged.AddListener(UpdateLabel);
    }


    private void Update()
    {
        if (!isVisible) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        transform.position =
            cam.transform.position +
            cam.transform.forward * distanceForward +
            Vector3.up * heightOffset;

        transform.LookAt(cam.transform);
        transform.Rotate(0, 180f, 0);
    }

    // Show the canvas at a specific world position (e.g., last coin)
    public void Show(Vector3 worldPosition)
    {
        Debug.Log("DiscomfortUI.Show called");
        panel.SetActive(true);
        isVisible = true;


        // enable right controller (pointer)
        if (rightController != null)
            rightController.SetActive(true);


        gameObject.SetActive(true);
        float distanceAbove = 0.5f; 
        transform.position = worldPosition + Vector3.up * distanceAbove;

        // make it face the player (camera)
        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.LookAt(cam.transform);
            transform.Rotate(0, 180f, 0); // Correct orientation
        }

        slider.value = 0; // default
        
        UpdateLabel(slider.value);
    }

    private void UpdateLabel(float val)
    {
        if (valueText != null)
            valueText.text = "Discomfort: " + Mathf.RoundToInt(val).ToString();
    }

    public int GetValue()
    {
        return Mathf.RoundToInt(slider.value);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        panel.SetActive(false);
        isVisible = false;

        // disable right controller (pointer)
        if (rightController != null)
            rightController.SetActive(false);
    }

    // when button press
    public void OnConfirm()
    {
        int selectedValue = GetValue();
        Debug.Log("Discomfort rating: " + selectedValue);
        Hide();
    }
}
