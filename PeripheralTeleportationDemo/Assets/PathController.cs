using System.Collections.Generic;
using UnityEngine;
using Unity.XR.CoreUtils;

public class PathController : MonoBehaviour
{
    [Header("Path Settings")]
    public int coinsVisibleAhead = 2;

    private List<Coin> coins = new List<Coin>();
    private int currentIndex = 0;

    public DiscomfortUI discomfortUI;
    public XROrigin xrRig;

    private void Awake()
    {
        // get all coins that are children of this path
        coins.AddRange(GetComponentsInChildren<Coin>());

    }

    private void Start()
    {
        UpdateCoinVisibility();
    }

    public void CoinCollected(Coin collectedCoin)
    {
        // move index forward
        currentIndex = coins.IndexOf(collectedCoin) + 1;
        UpdateCoinVisibility();
        
    }

    private void UpdateCoinVisibility()
    {
        for (int i = 0; i < coins.Count; i++)
        {
            bool shouldBeVisible =
                i >= currentIndex &&
                i < currentIndex + coinsVisibleAhead;

            coins[i].SetVisible(shouldBeVisible);
        }
    }
}
