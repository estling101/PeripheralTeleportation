using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Coin Settings")]
    public bool isFirstCoin;
    public bool isLastCoin;

    [Header("Rotation Settings")]
    public float rotationSpeed = 180f;

    private MeshRenderer meshRenderer;
    private PathController pathController;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        pathController = GetComponentInParent<PathController>();
    }

    /*private void Start()
    {
        // Scale last coin
        if (isLastCoin)
        {
            transform.localScale = Vector3.one * 5f;
        }
    }*/

    private void Update()
    {
        transform.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        // Timing logic
        TimeManager.Instance.CoinCollected(isFirstCoin, isLastCoin);

        // Notify path
        if (pathController != null)
            pathController.CoinCollected(this);

        Destroy(gameObject);
    }

    public void SetVisible(bool visible)
    {
        if (meshRenderer != null)
            meshRenderer.enabled = visible;
    }
}
