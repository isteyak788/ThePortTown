using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class ShipCargoHandler : MonoBehaviour
{
    [System.Serializable]
    public class CargoSlot
    {
        public Cargo cargoType;
        public int quantity;
        public bool isSelectedForAction; // New: For multi-selection in UI
        public int selectedQuantityForAction; // <--- NEW: Quantity selected for current action (sell/drop)

        public CargoSlot(Cargo type, int qty)
        {
            cargoType = type;
            quantity = qty;
            isSelectedForAction = false;
            selectedQuantityForAction = 0; // Initialize to 0
        }
    }

    public enum InventoryActionState
    {
        Normal,      // Default viewing mode
        Selling,     // Player is selecting cargo to sell
        Dropping     // Player is selecting cargo to drop
    }

    [Tooltip("List of cargo currently on the ship.")]
    public List<CargoSlot> shipCargo = new List<CargoSlot>();

    [Tooltip("Reference to the player's MoneyCollector script.")]
    public MoneyCollector playerMoneyCollector; // Assign in Inspector

    [Header("Trading/Interaction")]
    public Port _currentPort = null;
    public ShipCargoHandler currentOtherShip = null;

    [Header("UI Integration")]
    [Tooltip("Reference to the UIManager in the scene.")]
    public UIManager uiManager; // Assign in Inspector

    public InventoryActionState currentActionState = InventoryActionState.Normal;

    // Events for UI to subscribe to
    public delegate void OnShipCargoChanged();
    public event OnShipCargoChanged onShipCargoChanged;

    public delegate void OnInventoryActionStateChanged(InventoryActionState newState);
    public event OnInventoryActionStateChanged onInventoryActionStateChanged;

    void Start()
    {
        if (playerMoneyCollector == null)
        {
            playerMoneyCollector = GetComponent<MoneyCollector>();
            if (playerMoneyCollector == null)
            {
                Debug.LogError("ShipCargoHandler requires a MoneyCollector component on the same GameObject or assigned in Inspector.");
            }
        }
        if (uiManager == null)
        {
            Debug.LogError("UIManager reference is missing in ShipCargoHandler! Please assign it in the Inspector.");
        }

        SetActionState(InventoryActionState.Normal); // Ensure initial state is normal
        onShipCargoChanged?.Invoke(); // Initial UI update
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E)) // Toggle Inventory UI
        {
            if (uiManager != null)
            {
                if (uiManager.playerInventoryUI != null && uiManager.playerInventoryUI.gameObject.activeSelf)
                {
                    Debug.Log("ShipCargoHandler: E pressed, inventory is active. Calling ExitActionState.");
                    ExitActionState(); // Will also hide UI
                }
                else
                {
                    Debug.Log("ShipCargoHandler: E pressed, inventory is inactive. Preparing to show UI.");
                    SetActionState(InventoryActionState.Normal); // Set state to Normal *BEFORE* showing the UI
                    uiManager.ShowPlayerInventoryUI(this);
                }
            }
            else
            {
                Debug.LogWarning("UIManager is not assigned to ShipCargoHandler. Cannot open/close inventory via 'E'.");
            }
        }
    }

    public void AddCargo(Cargo cargoToAdd, int amount)
    {
        if (cargoToAdd == null || amount <= 0) return;

        CargoSlot existingSlot = shipCargo.Find(slot => slot.cargoType == cargoToAdd);
        if (existingSlot != null)
        {
            existingSlot.quantity += amount;
            // When quantity changes, ensure selectedQuantityForAction doesn't exceed new quantity
            existingSlot.selectedQuantityForAction = Mathf.Min(existingSlot.selectedQuantityForAction, existingSlot.quantity);
        }
        else
        {
            shipCargo.Add(new CargoSlot(cargoToAdd, amount));
        }
        Debug.Log($"Ship loaded {amount} units of {cargoToAdd.cargoName}. Total {cargoToAdd.cargoName}: {GetCargoQuantity(cargoToAdd)}");
        onShipCargoChanged?.Invoke(); // Notify UI
    }

    public bool RemoveCargo(Cargo cargoToRemove, int amount)
    {
        if (cargoToRemove == null || amount <= 0) return false;

        CargoSlot existingSlot = shipCargo.Find(slot => slot.cargoType == cargoToRemove);
        if (existingSlot != null && existingSlot.quantity >= amount)
        {
            existingSlot.quantity -= amount;
            // When quantity changes, ensure selectedQuantityForAction doesn't exceed new quantity
            existingSlot.selectedQuantityForAction = Mathf.Min(existingSlot.selectedQuantityForAction, existingSlot.quantity);

            if (existingSlot.quantity == 0)
            {
                shipCargo.Remove(existingSlot);
            }
            Debug.Log($"Ship unloaded {amount} units of {cargoToRemove.cargoName}. Total {cargoToRemove.cargoName}: {GetCargoQuantity(cargoToRemove)}");
            onShipCargoChanged?.Invoke(); // Notify UI
            return true;
        }
        Debug.LogWarning($"Could not remove {amount} units of {cargoToRemove.cargoName}. Not enough or not present.");
        return false;
    }

    public int GetCargoQuantity(Cargo cargoType)
    {
        CargoSlot slot = shipCargo.Find(s => s.cargoType == cargoType);
        return slot != null ? slot.quantity : 0;
    }

    /// <summary>
    /// Toggles the selection state of a cargo slot for multi-select actions.
    /// In quantity selection mode, this usually just selects the item to enable quantity controls.
    /// </summary>
    public void ToggleCargoSelection(Cargo cargoType)
    {
        if (currentActionState == InventoryActionState.Normal) return; // Only allow selection in action mode

        CargoSlot slot = shipCargo.Find(s => s.cargoType == cargoType);
        if (slot != null)
        {
            slot.isSelectedForAction = !slot.isSelectedForAction;
            // If selecting for action, ensure at least 1 is selected by default if quantity > 0
            if (slot.isSelectedForAction && slot.quantity > 0 && slot.selectedQuantityForAction == 0)
            {
                slot.selectedQuantityForAction = 1;
            }
            // If de-selecting, reset selected quantity
            else if (!slot.isSelectedForAction)
            {
                slot.selectedQuantityForAction = 0;
            }

            Debug.Log($"ShipCargoHandler: Toggled selection for {cargoType.cargoName}. Selected: {slot.isSelectedForAction}, Quantity: {slot.selectedQuantityForAction}");
            onShipCargoChanged?.Invoke(); // Refresh UI to show selection state and quantity
        }
    }

    /// <summary>
    /// Sets the current action state for inventory (Normal, Selling, Dropping).
    /// </summary>
    public void SetActionState(InventoryActionState newState)
    {
        if (currentActionState == newState)
        {
            Debug.Log($"[ShipCargoHandler] SetActionState: Already in {newState} state. No change.");
            return;
        }
        
        Debug.Log($"[ShipCargoHandler] SetActionState: Changing from {currentActionState} to {newState}");
        currentActionState = newState;
        
        // Clear all selections and selected quantities when state changes
        foreach (var slot in shipCargo)
        {
            slot.isSelectedForAction = false;
            slot.selectedQuantityForAction = 0; // Reset selected quantity
        }

        onShipCargoChanged?.Invoke(); // Trigger UI update for all items to reflect cleared states
        onInventoryActionStateChanged?.Invoke(currentActionState); // Notify UI to update buttons/visuals
    }

    /// <summary>
    /// Clears all currently selected cargo slots and their selected quantities.
    /// </summary>
    public void ClearAllSelections()
    {
        bool changed = false;
        foreach (var slot in shipCargo)
        {
            if (slot.isSelectedForAction || slot.selectedQuantityForAction > 0) // Also check selected quantity
            {
                slot.isSelectedForAction = false;
                slot.selectedQuantityForAction = 0; // Reset selected quantity
                changed = true;
            }
        }
        if (changed)
        {
            Debug.Log("ShipCargoHandler: Cleared all selections and quantities.");
            onShipCargoChanged?.Invoke(); // Only invoke if something actually changed
        }
    }

    /// <summary>
    /// Exits any action state, returning to normal view and hiding UI.
    /// </summary>
    public void ExitActionState()
    {
        Debug.Log("ShipCargoHandler: Calling SetActionState(Normal) from ExitActionState.");
        SetActionState(InventoryActionState.Normal); // This will also clear selections and trigger UI updates
        if (uiManager != null)
        {
            Debug.Log("ShipCargoHandler: Calling UIManager to hide inventory and trading UI.");
            uiManager.HidePlayerInventoryUI(); // This is the final step to hide the GameObject
            uiManager.HidePortTradingUI(); // Ensure trading UI is also hidden if open
        }
        else
        {
            Debug.LogError("UIManager reference is null in ShipCargoHandler. Cannot hide UI.");
        }
    }

    /// <summary>
    /// Executes the "OK" action based on the current state (Sell or Drop), using selected quantities.
    /// </summary>
    public void ExecuteSelectedCargoAction()
    {
        // Filter for items where isSelectedForAction is true AND selectedQuantityForAction is > 0
        List<CargoSlot> cargoToProcess = shipCargo
                                        .Where(s => s.isSelectedForAction && s.selectedQuantityForAction > 0)
                                        .ToList();

        if (!cargoToProcess.Any())
        {
            Debug.Log("ShipCargoHandler: No cargo selected with a quantity > 0 for action.");
            return;
        }

        if (currentActionState == InventoryActionState.Selling)
        {
            SellSelectedCargo(cargoToProcess);
        }
        else if (currentActionState == InventoryActionState.Dropping)
        {
            DropSelectedCargo(cargoToProcess);
        }

        // After action, return to normal state and clear selections/quantities
        Debug.Log("ShipCargoHandler: Executed action. Setting state back to Normal.");
        SetActionState(InventoryActionState.Normal); // This will clear selections and update UI
    }

    private void SellSelectedCargo(List<CargoSlot> cargoToSell)
    {
        if (_currentPort == null && currentOtherShip == null)
        {
            Debug.LogWarning("ShipCargoHandler: No port or other ship in range to sell cargo to.");
            return;
        }

        Port destinationPort = _currentPort;
        ShipCargoHandler destinationShip = currentOtherShip;

        if (destinationPort != null)
        {
            float totalRevenue = 0f;
            foreach (var slot in cargoToSell)
            {
                // Use slot.selectedQuantityForAction instead of slot.quantity
                int quantityToSell = slot.selectedQuantityForAction;
                if (quantityToSell <= 0) continue; // Skip if nothing selected for this item

                float unitBuyPrice = destinationPort.connectedPortTown.GetBuyPricePerUnit(slot.cargoType);
                float actualPaidByTown = destinationPort.connectedPortTown.BuyCargoFromPlayer(slot.cargoType, quantityToSell);

                if (actualPaidByTown > 0)
                {
                    playerMoneyCollector.AddMoney(actualPaidByTown * (1 - destinationPort.taxPercentage), $"Sold {quantityToSell} {slot.cargoType.cargoName} at {destinationPort.connectedPortTown.townName} (net of tax)");
                    destinationPort.portMoneyCollector.AddMoney(actualPaidByTown * destinationPort.taxPercentage, $"Tax on {quantityToSell} {slot.cargoType.cargoName} from {playerMoneyCollector.accountOwnerName}");

                    RemoveCargo(slot.cargoType, quantityToSell); // Remove from player's inventory
                    totalRevenue += actualPaidByTown;
                    Debug.Log($"ShipCargoHandler: Sold {quantityToSell} {slot.cargoType.cargoName} to {destinationPort.connectedPortTown.townName} for {actualPaidByTown:C2} (before tax).");
                }
                else
                {
                    Debug.LogWarning($"ShipCargoHandler: Town {destinationPort.connectedPortTown.townName} could not buy {quantityToSell} {slot.cargoType.cargoName}.");
                }
            }
            Debug.Log($"ShipCargoHandler: Finished selling selected cargo. Total revenue: {totalRevenue:C2}");
        }
        else if (destinationShip != null)
        {
            float totalRevenue = 0f;
            foreach (var slot in cargoToSell)
            {
                // Use slot.selectedQuantityForAction
                int quantityToSell = slot.selectedQuantityForAction;
                if (quantityToSell <= 0) continue;

                float unitValue = slot.cargoType.baseValuePerUnit;
                float totalForThisCargo = unitValue * quantityToSell;

                if (destinationShip.playerMoneyCollector != null && destinationShip.playerMoneyCollector.RemoveMoney(totalForThisCargo, $"Buying {quantityToSell} {slot.cargoType.cargoName} from {playerMoneyCollector.accountOwnerName}"))
                {
                    destinationShip.AddCargo(slot.cargoType, quantityToSell);
                    playerMoneyCollector.AddMoney(totalForThisCargo, $"Sold {quantityToSell} {slot.cargoType.cargoName} to {destinationShip.playerMoneyCollector.accountOwnerName}");
                    RemoveCargo(slot.cargoType, quantityToSell);
                    totalRevenue += totalForThisCargo;
                    Debug.Log($"ShipCargoHandler: Sold {quantityToSell} {slot.cargoType.cargoName} to {destinationShip.playerMoneyCollector.accountOwnerName} for {totalForThisCargo:C2}.");
                }
                else
                {
                    Debug.LogWarning($"ShipCargoHandler: Other ship ({destinationShip.name}) could not buy {quantityToSell} {slot.cargoType.cargoName}: insufficient funds or capacity.");
                }
            }
             Debug.Log($"ShipCargoHandler: Finished selling selected cargo to other ship. Total revenue: {totalRevenue:C2}");
        }
        else
        {
            Debug.LogWarning("ShipCargoHandler: Cannot sell cargo: No valid port or ship detected in range.");
        }
        onShipCargoChanged?.Invoke();
    }

    private void DropSelectedCargo(List<CargoSlot> cargoToDrop)
    {
        bool givenToOther = false;
        if (_currentPort != null)
        {
            Debug.Log($"ShipCargoHandler: Giving selected cargo to {_currentPort.connectedPortTown.townName}.");
            foreach (var slot in cargoToDrop)
            {
                // Use slot.selectedQuantityForAction
                int quantityToDrop = slot.selectedQuantityForAction;
                if (quantityToDrop <= 0) continue;

                _currentPort.connectedPortTown.AddCargoToTown(slot.cargoType, quantityToDrop);
                RemoveCargo(slot.cargoType, quantityToDrop);
            }
            givenToOther = true;
        }
        else if (currentOtherShip != null)
        {
            Debug.Log($"ShipCargoHandler: Giving selected cargo to {currentOtherShip.name}.");
            foreach (var slot in cargoToDrop)
            {
                // Use slot.selectedQuantityForAction
                int quantityToDrop = slot.selectedQuantityForAction;
                if (quantityToDrop <= 0) continue;

                currentOtherShip.AddCargo(slot.cargoType, quantityToDrop);
                RemoveCargo(slot.cargoType, quantityToDrop);
            }
            givenToOther = true;
        }

        if (!givenToOther)
        {
            Debug.Log("ShipCargoHandler: Dropping selected cargo into the sea.");
            foreach (var slot in cargoToDrop)
            {
                // Use slot.selectedQuantityForAction
                int quantityToDrop = slot.selectedQuantityForAction;
                if (quantityToDrop <= 0) continue;
                RemoveCargo(slot.cargoType, quantityToDrop);
            }
        }
        onShipCargoChanged?.Invoke();
    }

    public void SetCurrentPort(Port port)
    {
        _currentPort = port;
        if (_currentPort != null)
        {
            Debug.Log($"[ShipCargoHandler] Player's current port set to: {_currentPort.portName}.");
        }
        else
        {
            Debug.Log("[ShipCargoHandler] Player's current port cleared (set to null).");
        }

        if (uiManager != null)
        {
            uiManager.RefreshAllUI();
        }
        else
        {
            Debug.LogError("[ShipCargoHandler] UIManager is null when trying to refresh UI after port change!");
        }
    }

    public void ClearCurrentPort()
    {
        _currentPort = null;
        Debug.Log("[ShipCargoHandler] Player's current port cleared.");
        if (uiManager != null)
        {
            uiManager.RefreshAllUI();
        }
        else
        {
            Debug.LogError("[ShipCargoHandler] UIManager is null when trying to refresh UI after port clearance!");
        }
    }

    public (bool success, float amountPaid) BuyCargoFromPortTown(Cargo cargoType, int quantity)
    {
        if (_currentPort != null && _currentPort.connectedPortTown != null)
        {
            float saleRevenue = _currentPort.connectedPortTown.SellCargoToPlayer(cargoType, quantity);
            if (saleRevenue > 0)
            {
                if (playerMoneyCollector.RemoveMoney(saleRevenue, $"Purchasing {quantity} {cargoType.cargoName} from {_currentPort.connectedPortTown.townName}"))
                {
                    AddCargo(cargoType, quantity);
                    Debug.Log($"ShipCargoHandler: Player bought {quantity} units of {cargoType.cargoName} from {_currentPort.connectedPortTown.townName} for {saleRevenue:C2}.");
                    return (true, saleRevenue);
                }
                else
                {
                    Debug.LogWarning($"ShipCargoHandler: Player has insufficient funds to buy {quantity} {cargoType.cargoName}. Needed: {saleRevenue:C2}, Current: {playerMoneyCollector.currentBalance:C2}");
                    _currentPort.connectedPortTown.AddCargoToTown(cargoType, quantity);
                    return (false, 0f);
                }
            }
            else
            {
                Debug.Log($"ShipCargoHandler: Town could not sell {quantity} {cargoType.cargoName} to player.");
                return (false, 0f);
            }
        }
        else
        {
            Debug.Log("ShipCargoHandler: Cannot buy cargo: not in a port or port town not connected.");
            return (false, 0f);
        }
    }
    
    public void SetCurrentOtherShip(ShipCargoHandler otherShip)
    {
        currentOtherShip = otherShip;
        if (currentOtherShip != null)
        {
            Debug.Log($"ShipCargoHandler: Another ship entered player's range: {otherShip.gameObject.name}");
        }
        if (uiManager != null && uiManager.playerInventoryUI != null && uiManager.playerInventoryUI.gameObject.activeSelf)
        {
            uiManager.playerInventoryUI.UpdateActionButtonVisibility(_currentPort != null || currentOtherShip != null);
        }
    }

    public void ClearCurrentOtherShip()
    {
        currentOtherShip = null;
        Debug.Log($"ShipCargoHandler: Another ship exited player's range: {currentOtherShip?.gameObject.name ?? "N/A"}");
        if (uiManager != null && uiManager.playerInventoryUI != null && uiManager.playerInventoryUI.gameObject.activeSelf)
        {
            uiManager.playerInventoryUI.UpdateActionButtonVisibility(_currentPort != null || currentOtherShip != null);
        }
    }
}
