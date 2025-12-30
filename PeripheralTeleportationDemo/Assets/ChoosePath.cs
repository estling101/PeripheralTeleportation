using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

public class PathManager : MonoBehaviour
{
    [Header("Rig")]
    public XROrigin xrRig;

    [Header("Path Selection")]
    public int selectedPathIndex = 0;

    [Tooltip("Teleport on Start")]
    public bool teleportOnStart = true;

    private int lastPathIndex = -1;

    void Start()
    {
        if (teleportOnStart)
        {
            ApplyPathSelection();
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        if (selectedPathIndex == lastPathIndex) return;

        ApplyPathSelection();
    }

    private void ApplyPathSelection()
    {
        lastPathIndex = selectedPathIndex;

        ActivateSelectedPath();
        TeleportToSelectedPath();
    }

    private void ActivateSelectedPath()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            bool shouldBeActive = (i == selectedPathIndex);
            transform.GetChild(i).gameObject.SetActive(shouldBeActive);
        }
    }

    [ContextMenu("Teleport To Selected Path")]
    public void TeleportToSelectedPath()
    {
        
        Transform path = transform.GetChild(selectedPathIndex);
       
        Transform coin = path.GetChild(0);

        xrRig.transform.SetPositionAndRotation(
            coin.position,
            Quaternion.Euler(0, coin.rotation.eulerAngles.y, 0)
        );
    }
}
