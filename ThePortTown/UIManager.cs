using UnityEngine;
using TMPro; // For TextMeshPro
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Top Bar / Persistent UI")]
    public TextMeshProUGUI playerMoneyText;
    public TextMeshProUGUI playerCargoCountText;

    [Header("Main UI Panels")]
    public PlayerInventoryUI playerInventoryUI; // New: Reference to player inventory UI
    public PortTradingUI portTradingUI; // Existing: Reference to port trading UI

    [Header("References (assigned in Inspector)")]
    public ShipCargoHandler playerShipCargoHandler;
    private Port _currentPort; // Current active port (set by Port.cs)
    private ShipCargoHandler _currentOtherShip; // Current active other ship (set by PlayerTriggerDetector)

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure all UI panels are assigned and initially hidden
        if (playerInventoryUI == null) Debug.LogError("UIManager: PlayerInventoryUI reference is missing! Please assign it.");
        if (portTradingUI == null) Debug.LogError("UIManager: PortTradingUI reference is missing! Please assign it.");

        if (playerInventoryUI != null) playerInventoryUI.gameObject.SetActive(false);
        if (portTradingUI != null) portTradingUI.gameObject.SetActive(false);
    }

    void Start()
    {
        if (playerShipCargoHandler != null)
        {
            if (playerShipCargoHandler.playerMoneyCollector != null)
            {
                playerShipCargoHandler.playerMoneyCollector.onBalanceChanged += UpdatePlayerMoneyUI;
                UpdatePlayerMoneyUI(playerShipCargoHandler.playerMoneyCollector.currentBalance);
            }
            playerShipCargoHandler.onShipCargoChanged += UpdatePlayerCargoCountUI;
            UpdatePlayerCargoCountUI();

            // CRITICAL SUBSCRIPTION FOR "OK" BUTTON VISIBILITY
            // This ensures PlayerInventoryUI updates when ShipCargoHandler changes its state.
            if (playerInventoryUI != null)
            {
                playerShipCargoHandler.onInventoryActionStateChanged += playerInventoryUI.OnPlayerActionStateChanged;
            }
            else
            {
                Debug.LogError("UIManager: playerInventoryUI is null. Cannot subscribe to onInventoryActionStateChanged.");
            }
        }
        else
        {
            Debug.LogError("UIManager: Player Ship Cargo Handler reference is missing! Please assign it.");
        }

        RefreshAllUI();
    }

    void OnDestroy()
    {
        // Always unsubscribe from events to prevent memory leaks and null reference exceptions
        if (playerShipCargoHandler != null)
        {
            if (playerShipCargoHandler.playerMoneyCollector != null)
            {
                playerShipCargoHandler.playerMoneyCollector.onBalanceChanged -= UpdatePlayerMoneyUI;
            }
            playerShipCargoHandler.onShipCargoChanged -= UpdatePlayerCargoCountUI;
            if (playerInventoryUI != null)
            {
                 playerShipCargoHandler.onInventoryActionStateChanged -= playerInventoryUI.OnPlayerActionStateChanged;
            }
        }

        if (_currentPort != null)
        {
            if (_currentPort.portMoneyCollector != null)
            {
                _currentPort.portMoneyCollector.onBalanceChanged -= UpdatePortMoneyUI;
            }
            if (_currentPort.connectedPortTown != null)
            {
                _currentPort.connectedPortTown.onEconomyChanged -= UpdateTownEconomyUI;
                if (portTradingUI != null)
                {
                    _currentPort.connectedPortTown.onTownDataChanged -= portTradingUI.RefreshAvailableGoods;
                    _currentPort.connectedPortTown.onGoodsAvailabilityChanged -= (c, q) => portTradingUI.RefreshAvailableGoods();
                }
            }
        }
    }

    // --- Player Inventory UI Management ---
    public void ShowPlayerInventoryUI(ShipCargoHandler playerHandler)
    {
        if (playerInventoryUI != null)
        {
            // NEW: Pass current port and other ship to PlayerInventoryUI
            playerInventoryUI.SetInteractionTargets(playerHandler._currentPort, playerHandler.currentOtherShip); // Access ShipCargoHandler's internal references
            playerInventoryUI.InitializeInventoryUI(playerHandler, playerHandler._currentPort, playerHandler.currentOtherShip);
            playerInventoryUI.gameObject.SetActive(true);
            Debug.Log("UIManager: Player Inventory UI shown.");
            RefreshAllUI(); // Ensure all related UI is refreshed
        }
        else
        {
            Debug.LogError("UIManager: PlayerInventoryUI reference is null. Cannot show Player Inventory UI.");
        }
    }

    public void HidePlayerInventoryUI()
    {
        if (playerInventoryUI != null)
        {
            Debug.Log("UIManager: Hiding Player Inventory UI GameObject.");
            playerInventoryUI.gameObject.SetActive(false);
            // ShipCargoHandler's ExitActionState handles state reset.
        }
        else
        {
            Debug.LogError("UIManager: PlayerInventoryUI reference is null. Cannot hide Player Inventory UI.");
        }
    }

    // --- Port Trading UI Management (Called by PortTradeTrigger.cs) ---
    public void ShowPortTradingUI(Port port, ShipCargoHandler shipHandler)
    {
        _currentPort = port; // Set UIManager's internal _currentPort
        _currentOtherShip = shipHandler.currentOtherShip; // UIManager's internal _currentOtherShip from player's handler

        // Subscribe to port/town specific events when port UI is shown
        if (_currentPort != null)
        {
            if (_currentPort.portMoneyCollector != null)
            {
                _currentPort.portMoneyCollector.onBalanceChanged += UpdatePortMoneyUI;
            }
            if (_currentPort.connectedPortTown != null)
            {
                _currentPort.connectedPortTown.onEconomyChanged += UpdateTownEconomyUI;
                if (portTradingUI != null)
                {
                    _currentPort.connectedPortTown.onTownDataChanged += portTradingUI.RefreshAvailableGoods;
                    _currentPort.connectedPortTown.onGoodsAvailabilityChanged += (c, q) => portTradingUI.RefreshAvailableGoods();
                }
            }
        }

        if (portTradingUI != null)
        {
            portTradingUI.InitializePortUI(port, shipHandler);
            portTradingUI.gameObject.SetActive(true);
            Debug.Log("UIManager: Port Trading UI shown.");
            RefreshAllUI(); // Refresh all relevant UI
        }
        else
        {
            Debug.LogError("UIManager: PortTradingUI reference is null. Cannot show Port Trading UI.");
        }
    }

    public void HidePortTradingUI()
    {
        if (portTradingUI != null)
        {
            portTradingUI.gameObject.SetActive(false);
            Debug.Log("UIManager: Port Trading UI hidden.");
        }
        else
        {
            Debug.LogError("UIManager: PortTradingUI reference is null. Cannot hide Port Trading UI.");
        }

        // Unsubscribe from port/town specific events when port UI is hidden
        if (_currentPort != null)
        {
            if (_currentPort.portMoneyCollector != null)
            {
                _currentPort.portMoneyCollector.onBalanceChanged -= UpdatePortMoneyUI;
            }
            if (_currentPort.connectedPortTown != null)
            {
                _currentPort.connectedPortTown.onEconomyChanged -= UpdateTownEconomyUI;
                if (portTradingUI != null)
                {
                    _currentPort.connectedPortTown.onTownDataChanged -= portTradingUI.RefreshAvailableGoods;
                    _currentPort.connectedPortTown.onGoodsAvailabilityChanged -= (c, q) => portTradingUI.RefreshAvailableGoods();
                }
            }
        }
        _currentPort = null;
    }

    // --- General UI Refresh Methods ---
    public void RefreshAllUI()
    {
        Debug.Log("[UIManager] RefreshAllUI called.");
        if (playerShipCargoHandler != null && playerShipCargoHandler.playerMoneyCollector != null)
        {
            UpdatePlayerMoneyUI(playerShipCargoHandler.playerMoneyCollector.currentBalance);
        }
        UpdatePlayerCargoCountUI();

        if (playerInventoryUI != null && playerInventoryUI.gameObject.activeSelf)
        {
            // Pass the current interaction targets to PlayerInventoryUI for its button visibility logic
            playerInventoryUI.SetInteractionTargets(playerShipCargoHandler?._currentPort, playerShipCargoHandler?.currentOtherShip);
            playerInventoryUI.RefreshPlayerCargoDisplay(); // This also calls UpdateActionButtonsState
        }

        if (portTradingUI != null && portTradingUI.gameObject.activeSelf)
        {
            portTradingUI.RefreshPortSpecificUI();
            portTradingUI.RefreshAvailableGoods();
        }
    }

    // --- Individual UI Update Methods ---
    private void UpdatePlayerMoneyUI(float newBalance)
    {
        if (playerMoneyText != null)
        {
            playerMoneyText.text = $"Money: {newBalance:C2}";
        }
    }

    private void UpdatePlayerCargoCountUI()
    {
        if (playerCargoCountText != null && playerShipCargoHandler != null)
        {
            int totalCargo = 0;
            foreach (var slot in playerShipCargoHandler.shipCargo)
            {
                totalCargo += slot.quantity;
            }
            playerCargoCountText.text = $"Cargo: {totalCargo} units";
        }
    }

    // These methods update PortTradingUI's internal texts
    public void UpdatePortMoneyUI(float newBalance)
    {
        if (portTradingUI != null && portTradingUI.portMoneyText != null && _currentPort != null)
        {
            portTradingUI.portMoneyText.text = $"Port Money: {newBalance:C2}";
        }
    }

    public void UpdateTownEconomyUI(float newEconomy)
    {
        if (portTradingUI != null && portTradingUI.townEconomyText != null && _currentPort != null && _currentPort.connectedPortTown != null)
        {
            portTradingUI.townEconomyText.text = $"Economy: {newEconomy:F0}";
        }
    }

    // Called by PlayerTriggerDetector
    public void SetPlayerNearbyShip(ShipCargoHandler otherShip)
    {
        _currentOtherShip = otherShip;
        if (playerShipCargoHandler != null) playerShipCargoHandler.SetCurrentOtherShip(otherShip); // Update player ship's reference
        
        // If inventory is open, update its targets and visibility
        if (playerInventoryUI != null && playerInventoryUI.gameObject.activeSelf)
        {
            playerInventoryUI.SetInteractionTargets(playerShipCargoHandler?._currentPort, _currentOtherShip);
        }
    }

    public void ClearPlayerNearbyShip()
    {
        _currentOtherShip = null;
        if (playerShipCargoHandler != null) playerShipCargoHandler.ClearCurrentOtherShip(); // Update player ship's reference
        
        // If inventory is open, update its targets and visibility
        if (playerInventoryUI != null && playerInventoryUI.gameObject.activeSelf)
        {
            playerInventoryUI.SetInteractionTargets(playerShipCargoHandler?._currentPort, _currentOtherShip);
        }
    }
}
