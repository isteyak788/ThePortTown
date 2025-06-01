using UnityEngine;
using UnityEngine.UI;
using TMPro; // For TextMeshPro
using System.Collections.Generic;

public class PortTradingUI : MonoBehaviour
{
    [Header("Main Panel Elements")]
    public TextMeshProUGUI townNameText;
    public TextMeshProUGUI portMoneyText;
    public TextMeshProUGUI townEconomyText;
    public Button closeButton; // Button to close the panel

    [Header("Town's Goods to Buy")]
    public Transform townGoodsBuyContainer; // Parent for town's cargo items
    public GameObject townGoodsBuyUIPrefab; // Prefab for a single cargo item to buy

    private Port _activePort;
    private ShipCargoHandler _playerShipCargoHandler;

    void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnClickClosePanel);
        }
    }

    public void InitializePortUI(Port port, ShipCargoHandler playerHandler)
    {
        _activePort = port;
        _playerShipCargoHandler = playerHandler;

        RefreshPortSpecificUI();
        RefreshAvailableGoods();
    }

    void OnDisable() // When the panel is hidden
    {
        // Unsubscribe from specific town goods availability changes, as UIManager handles townDataChanged
        if (_activePort != null && _activePort.connectedPortTown != null)
        {
            _activePort.connectedPortTown.onGoodsAvailabilityChanged -= (c, q) => RefreshAvailableGoods();
        }

        _activePort = null;
        _playerShipCargoHandler = null;
    }

    public void RefreshPortSpecificUI()
    {
        if (_activePort == null || _activePort.connectedPortTown == null) return;

        townNameText.text = _activePort.connectedPortTown.townName;
        // UIManager is responsible for updating these, as it's subscribed to the MoneyCollector/PortTown events
        UIManager.Instance.UpdatePortMoneyUI(_activePort.portMoneyCollector.currentBalance);
        UIManager.Instance.UpdateTownEconomyUI(_activePort.connectedPortTown.economyPoints);
    }

    public void RefreshAvailableGoods()
    {
        if (_activePort == null || _activePort.connectedPortTown == null) return;

        // Clear existing UI elements
        foreach (Transform child in townGoodsBuyContainer)
        {
            Destroy(child.gameObject);
        }

        // Populate with goods available from town
        foreach (Cargo cargoType in _activePort.connectedPortTown.producibleCargoTypes)
        {
            int quantity = _activePort.connectedPortTown.GetAvailableCargoQuantity(cargoType);
            float price = _activePort.connectedPortTown.GetSellPricePerUnit(cargoType);

            if (quantity > 0)
            {
                GameObject cargoBuyUI = Instantiate(townGoodsBuyUIPrefab, townGoodsBuyContainer);
                cargoBuyUI.transform.Find("CargoNameText").GetComponent<TextMeshProUGUI>().text = cargoType.cargoName;
                cargoBuyUI.transform.Find("QuantityText").GetComponent<TextMeshProUGUI>().text = $"Qty: {quantity}";
                cargoBuyUI.transform.Find("PriceText").GetComponent<TextMeshProUGUI>().text = $"Price: {price:C2} / unit";

                Button buyButton = cargoBuyUI.transform.Find("BuyButton").GetComponent<Button>();
                buyButton.onClick.RemoveAllListeners();
                // Set the quantity to buy as 1 for simplicity. PlayerInventoryUI will handle selling/dropping
                buyButton.onClick.AddListener(() => OnClickBuyCargo(cargoType, 1));
            }
        }
    }

    // --- UI Button Click Handlers ---
    public void OnClickBuyCargo(Cargo cargoType, int quantity)
    {
        if (_playerShipCargoHandler != null && _activePort != null)
        {
            _playerShipCargoHandler.BuyCargoFromPortTown(cargoType, quantity);
            // Refresh UI is handled by UIManager.RefreshAllUI via events triggered by transactions.
        }
    }

    public void OnClickClosePanel()
    {
        UIManager.Instance.HidePortTradingUI();
    }
}
