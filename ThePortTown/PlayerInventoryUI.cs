using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshProUGUI
using System.Collections.Generic;

public class PlayerInventoryUI : MonoBehaviour
{
    [Header("Main Panel Elements")]
    public Button sellButton;
    public Button dropButton;
    public Button okActionButton; // For multi-select actions
    public Button closeButton; // Close inventory panel

    [Header("Action Mode Panels")] // NEW HEADER FOR CLARITY
    public GameObject normalActionPanel; // Panel containing Sell/Drop buttons
    public GameObject okActionPanel;     // Panel containing the OK button (and potentially other elements for action mode)

    [Header("Cargo Display")]
    public Transform cargoListContainer; // Parent for individual cargo UI items
    public GameObject inventoryCargoUIPrefab; // Prefab for a single cargo item

    // --- NEW FIELDS FOR CUSTOMIZABLE OK BUTTON TEXT ---
    [Header("OK Button Text Customization")]
    [Tooltip("The TextMeshProUGUI component on the OK button.")]
    public TextMeshProUGUI okButtonText; // <--- NEW: Assign the TextMeshProUGUI from the OK button in Inspector
    [Tooltip("The label for the OK button when in 'Selling' mode.")]
    public string sellActionOkButtonLabel = "Sell Selected"; // <--- NEW: Default label for selling
    [Tooltip("The label for the OK button when in 'Dropping' mode.")]
    public string dropActionOkButtonLabel = "Drop Selected"; // <--- NEW: Default label for dropping
    // ----------------------------------------------------

    private ShipCargoHandler _playerShipCargoHandler;
    private Port _currentPort; // This is set by UIManager when inventory opens
    private ShipCargoHandler _currentOtherShip; // This is set by UIManager when inventory opens

    void Awake()
    {
        // Assign button listeners
        if (sellButton != null) sellButton.onClick.AddListener(OnClickSell);
        else Debug.LogError("Sell Button is not assigned in PlayerInventoryUI.");

        if (dropButton != null) dropButton.onClick.AddListener(OnClickDrop);
        else Debug.LogError("Drop Button is not assigned in PlayerInventoryUI.");

        if (okActionButton != null) okActionButton.onClick.AddListener(OnClickOKAction);
        else Debug.LogError("OK Action Button is not assigned in PlayerInventoryUI.");

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnClickClosePanel);
        }
        else
        {
            Debug.LogError("Close Button is not assigned in PlayerInventoryUI. Please assign it in the Inspector.");
        }
        
        // Initial state setup for panels (they should be set up in Inspector, but a fail-safe)
        if (normalActionPanel == null) Debug.LogError("Normal Action Panel is not assigned in PlayerInventoryUI.");
        if (okActionPanel == null) Debug.LogError("OK Action Panel is not assigned in PlayerInventoryUI.");

        // Ensure panels are hidden initially
        if (normalActionPanel != null) normalActionPanel.SetActive(false);
        if (okActionPanel != null) okActionPanel.SetActive(false);
    }

    public void InitializeInventoryUI(ShipCargoHandler playerHandler, Port currentPort, ShipCargoHandler currentOtherShip)
    {
        _playerShipCargoHandler = playerHandler;
        _currentPort = currentPort; // Receive current port from UIManager
        _currentOtherShip = currentOtherShip; // Receive current other ship from UIManager

        if (_playerShipCargoHandler == null)
        {
            Debug.LogError("Player Ship Cargo Handler is null when initializing PlayerInventoryUI.");
            return;
        }

        // Subscribe to relevant events
        // Ensure we only subscribe once
        _playerShipCargoHandler.onShipCargoChanged -= RefreshPlayerCargoDisplay; // Unsubscribe defensively
        _playerShipCargoHandler.onShipCargoChanged += RefreshPlayerCargoDisplay;
        
        RefreshPlayerCargoDisplay();
        
        // This is where _currentPort and _currentOtherShip are used for the initial update
        UpdateActionButtonVisibility(_currentPort != null || _currentOtherShip != null);
        UpdateActionButtonsState(_playerShipCargoHandler.currentActionState); // Ensure correct initial state on open
    }

    void OnEnable() // When the panel becomes active
    {
        // Re-subscribe if OnDisable unsubscribed. Ensures fresh state if UI is re-enabled.
        if (_playerShipCargoHandler != null)
        {
            _playerShipCargoHandler.onInventoryActionStateChanged -= OnPlayerActionStateChanged; // Unsubscribe defensively
            _playerShipCargoHandler.onInventoryActionStateChanged += OnPlayerActionStateChanged;
            _playerShipCargoHandler.onShipCargoChanged -= RefreshPlayerCargoDisplay; // Unsubscribe defensively
            _playerShipCargoHandler.onShipCargoChanged += RefreshPlayerCargoDisplay;
            
            // Re-evaluate visibility and state when enabled
            UpdateActionButtonVisibility(_currentPort != null || _currentOtherShip != null);
            UpdateActionButtonsState(_playerShipCargoHandler.currentActionState); 
        }
        else
        {
             Debug.LogWarning("[PlayerInventoryUI] _playerShipCargoHandler is null OnEnable. Check initialization order.");
        }
    }

    void OnDisable() // When the panel is hidden
    {
        if (_playerShipCargoHandler != null)
        {
            // Unsubscribe to prevent errors if ShipCargoHandler tries to invoke when UI is off
            _playerShipCargoHandler.onShipCargoChanged -= RefreshPlayerCargoDisplay;
            _playerShipCargoHandler.onInventoryActionStateChanged -= OnPlayerActionStateChanged; // Unsubscribe
            _playerShipCargoHandler.ClearAllSelections(); // Ensure selections are cleared when closing
        }
        // These references are transient, reset them on disable
        _currentPort = null;
        _currentOtherShip = null;
    }

    public void RefreshPlayerCargoDisplay()
    {
        if (_playerShipCargoHandler == null)
        {
            Debug.LogError("RefreshPlayerCargoDisplay: Player Ship Cargo Handler is null.");
            return;
        }

        // Clear previous cargo UI items
        foreach (Transform child in cargoListContainer)
        {
            Destroy(child.gameObject);
        }

        // Create new UI items for each cargo slot
        foreach (var slot in _playerShipCargoHandler.shipCargo)
        {
            if (inventoryCargoUIPrefab == null)
            {
                Debug.LogError("Inventory Cargo UI Prefab is not assigned in PlayerInventoryUI! Cannot create cargo UI items.");
                return;
            }

            GameObject cargoUI = Instantiate(inventoryCargoUIPrefab, cargoListContainer);
            InventoryUIItem uiItem = cargoUI.GetComponent<InventoryUIItem>();
            if (uiItem != null)
            {
                uiItem.SetCargoSlot(slot, _playerShipCargoHandler.currentActionState);
                // Listen to the UI item's click event
                Button itemButton = cargoUI.GetComponent<Button>();
                if (itemButton != null)
                {
                    itemButton.onClick.RemoveAllListeners(); // Prevent duplicate listeners on refresh
                    itemButton.onClick.AddListener(() => _playerShipCargoHandler.ToggleCargoSelection(slot.cargoType));
                }
                else
                {
                    Debug.LogWarning($"InventoryUIItem prefab '{inventoryCargoUIPrefab.name}' is missing a Button component. Cannot add click listener.");
                }
            }
            else
            {
                Debug.LogWarning($"InventoryUIItem prefab '{inventoryCargoUIPrefab.name}' is missing the InventoryUIItem script.");
            }
        }
        // Update button states after refreshing cargo display
        UpdateActionButtonsState(_playerShipCargoHandler.currentActionState);
    }

    // Called by ShipCargoHandler when its state changes via onInventoryActionStateChanged event
    public void OnPlayerActionStateChanged(ShipCargoHandler.InventoryActionState newState)
    {
        Debug.Log($"[PlayerInventoryUI] OnPlayerActionStateChanged called. New state: {newState}");
        UpdateActionButtonsState(newState);
        RefreshPlayerCargoDisplay(); // Refresh to update selection visuals and ensure buttons are correct
    }

    // Controls visibility of Sell/Drop vs OK buttons
    private void UpdateActionButtonsState(ShipCargoHandler.InventoryActionState state)
    {
        bool isNormal = (state == ShipCargoHandler.InventoryActionState.Normal);
        bool isAction = (state == ShipCargoHandler.InventoryActionState.Selling || state == ShipCargoHandler.InventoryActionState.Dropping);

        Debug.Log($"[PlayerInventoryUI] Updating action buttons state. IsNormal: {isNormal}, IsAction: {isAction}. OK Panel should be active: {isAction}");

        // Control the panels containing the buttons
        if (normalActionPanel != null) normalActionPanel.SetActive(isNormal);
        else Debug.LogError("Normal Action Panel is not assigned in PlayerInventoryUI. Cannot set active state.");

        if (okActionPanel != null) okActionPanel.SetActive(isAction);
        else Debug.LogError("OK Action Panel is not assigned in PlayerInventoryUI. Cannot set active state.");

        // --- MODIFIED: Use the new public TextMeshProUGUI field and public strings ---
        if (okButtonText != null) // Check if the TextMeshProUGUI component itself is assigned
        {
            if (state == ShipCargoHandler.InventoryActionState.Selling)
            {
                okButtonText.text = sellActionOkButtonLabel; // Use the inspector-set label
            }
            else if (state == ShipCargoHandler.InventoryActionState.Dropping)
            {
                okButtonText.text = dropActionOkButtonLabel; // Use the inspector-set label
            }
            else // Normal or other states (e.g., if somehow visible)
            {
                okButtonText.text = "OK"; // A fallback default
            }
        }
        else if (okActionPanel != null && okActionPanel.activeSelf) // Only warn if panel is active and text is missing
        {
            Debug.LogWarning("okButtonText is not assigned in PlayerInventoryUI. Cannot set button label. Assign the TextMeshProUGUI component on your OK button to this field.");
        }
        // -------------------------------------------------------------------------
    }

    // Controls if Sell/Drop buttons are interactable based on nearby entities
    // This is called by UIManager.RefreshAllUI or UIManager.SetPlayerNearbyShip/ClearPlayerNearbyShip
    public void UpdateActionButtonVisibility(bool canSellOrGive)
    {
        Debug.Log($"[PlayerInventoryUI] UpdateActionButtonVisibility called. canSellOrGive: {canSellOrGive}. Current Port: {_currentPort?.portName ?? "None"}, Other Ship: {_currentOtherShip?.name ?? "None"}");

        if (sellButton == null)
        {
            Debug.LogError("PlayerInventoryUI: Sell Button is null in UpdateActionButtonVisibility! Please assign it in the Inspector.");
            // Don't return, as dropButton might still be valid
        }
        else
        {
            sellButton.interactable = canSellOrGive;
            Debug.Log($"[PlayerInventoryUI] Sell button interactable set to: {sellButton.interactable}");
        }

        if (dropButton == null)
        {
            Debug.LogError("PlayerInventoryUI: Drop Button is null in UpdateActionButtonVisibility! Please assign it in the Inspector.");
        }
        else
        {
            dropButton.interactable = true; // Always allow dropping
            Debug.Log($"[PlayerInventoryUI] Drop button interactable set to: {dropButton.interactable}");
        }
    }

    // Update the port/other ship references
    // This is important because UIManager passes this when showing the inventory.
    public void SetInteractionTargets(Port port, ShipCargoHandler otherShip)
    {
        _currentPort = port;
        _currentOtherShip = otherShip;
        Debug.Log($"[PlayerInventoryUI] Interaction targets updated. Port: {_currentPort?.portName ?? "None"}, Other Ship: {_currentOtherShip?.name ?? "None"}");
        // Immediately update button visibility based on new targets
        UpdateActionButtonVisibility(_currentPort != null || _currentOtherShip != null);
    }


    // --- Button Click Handlers ---
    public void OnClickSell()
    {
        if (_playerShipCargoHandler != null)
        {
            Debug.Log("PlayerInventoryUI: Sell button clicked. Setting state to Selling.");
            _playerShipCargoHandler.SetActionState(ShipCargoHandler.InventoryActionState.Selling);
        }
    }

    public void OnClickDrop()
    {
        if (_playerShipCargoHandler != null)
        {
            Debug.Log("PlayerInventoryUI: Drop button clicked. Setting state to Dropping.");
            _playerShipCargoHandler.SetActionState(ShipCargoHandler.InventoryActionState.Dropping);
        }
    }

    public void OnClickOKAction()
    {
        if (_playerShipCargoHandler != null)
        {
            Debug.Log("PlayerInventoryUI: OK button clicked. Executing selected cargo action.");
            _playerShipCargoHandler.ExecuteSelectedCargoAction();
            // After executing, ExitActionState will be called by ShipCargoHandler which will reset state
        }
    }

    public void OnClickClosePanel()
    {
        // This is the crucial call that starts the closing process
        if (_playerShipCargoHandler != null)
        {
            Debug.Log("PlayerInventoryUI: Close button clicked. Calling ShipCargoHandler.ExitActionState().");
            _playerShipCargoHandler.ExitActionState(); // This will also hide the UI via UIManager
        }
        else
        {
            Debug.LogError("PlayerShipCargoHandler is null in PlayerInventoryUI. Cannot perform ExitActionState.");
        }
    }
}
