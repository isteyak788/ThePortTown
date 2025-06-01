using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUIItem : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI cargoNameText;
    public TextMeshProUGUI cargoQuantityText; // Total quantity on ship
    public Image cargoIconImage;
    public Image selectionHighlight; 

    [Header("Quantity Selection UI")]
    [Tooltip("The panel containing the quantity selection buttons and text. This will be shown/hidden.")]
    public GameObject quantitySelectionPanel; // Assign this parent GameObject
    public TextMeshProUGUI currentSelectionQuantityText; // Text to show how many are selected
    public Button plusButton;
    public Button plusPlusButton; // +10
    public Button minusButton;
    public Button minusMinusButton; // -10

    private ShipCargoHandler.CargoSlot _currentCargoSlot;
    private ShipCargoHandler.InventoryActionState _currentActionState;

    void Awake()
    {
        // Add listeners for the buttons (best done in Awake or OnEnable for UI elements)
        if (plusButton != null) plusButton.onClick.AddListener(OnPlusClick);
        if (plusPlusButton != null) plusPlusButton.onClick.AddListener(OnPlusPlusClick);
        if (minusButton != null) minusButton.onClick.AddListener(OnMinusClick);
        if (minusMinusButton != null) minusMinusButton.onClick.AddListener(OnMinusMinusClick);
    }

    void OnDestroy()
    {
        // Remove listeners to prevent memory leaks if this object is destroyed
        if (plusButton != null) plusButton.onClick.RemoveListener(OnPlusClick);
        if (plusPlusButton != null) plusPlusButton.onClick.RemoveListener(OnPlusPlusClick);
        if (minusButton != null) minusButton.onClick.RemoveListener(OnMinusClick);
        if (minusMinusButton != null) minusMinusButton.onClick.RemoveListener(OnMinusMinusClick);
    }

    public void SetCargoSlot(ShipCargoHandler.CargoSlot slot, ShipCargoHandler.InventoryActionState actionState)
    {
        _currentCargoSlot = slot;
        _currentActionState = actionState;

        if (slot.cargoType != null)
        {
            if (cargoNameText != null) cargoNameText.text = slot.cargoType.cargoName;
            else Debug.LogWarning("InventoryUIItem: cargoNameText is not assigned.", this);

            if (cargoQuantityText != null) cargoQuantityText.text = "x" + slot.quantity.ToString();
            else Debug.LogWarning("InventoryUIItem: cargoQuantityText is not assigned.", this);

            if (cargoIconImage != null)
            {
                if (slot.cargoType.cargoIcon != null)
                {
                    cargoIconImage.sprite = slot.cargoType.cargoIcon;
                    cargoIconImage.enabled = true;
                }
                else
                {
                    cargoIconImage.enabled = false;
                    Debug.LogWarning($"Cargo type '{slot.cargoType.cargoName}' has no 'cargoIcon' assigned. Image will be hidden.");
                }
            }
            else
            {
                Debug.LogWarning("InventoryUIItem: 'cargoIconImage' (the Image component) is not assigned. Cannot display icon.", this);
            }
        }
        else
        {
            if (cargoNameText != null) cargoNameText.text = "Empty Slot";
            if (cargoQuantityText != null) cargoQuantityText.text = "";
            if (cargoIconImage != null) cargoIconImage.enabled = false;
        }

        // Update visibility of quantity selection UI and selection highlight
        UpdateUIState();
    }

    private void UpdateUIState()
    {
        bool isInActionState = (_currentActionState == ShipCargoHandler.InventoryActionState.Selling ||
                                _currentActionState == ShipCargoHandler.InventoryActionState.Dropping);

        // Control visibility of the quantity selection panel
        if (quantitySelectionPanel != null)
        {
            // Only show quantity selection if in an action state AND the item is selected
            quantitySelectionPanel.SetActive(isInActionState && _currentCargoSlot.isSelectedForAction);
        }
        else
        {
            Debug.LogWarning("InventoryUIItem: quantitySelectionPanel is not assigned. Quantity buttons won't appear.");
        }

        // Update selection highlight (only if not in Normal state, as Normal state doesn't have a highlight use-case for individual items)
        if (selectionHighlight != null)
        {
            selectionHighlight.gameObject.SetActive(isInActionState && _currentCargoSlot.isSelectedForAction);
        }
        else
        {
            Debug.LogWarning("InventoryUIItem: selectionHighlight is not assigned.");
        }

        // Update selected quantity display and button interactability
        UpdateQuantityUI();
    }

    private void UpdateQuantityUI()
    {
        if (_currentCargoSlot == null) return; // Should not happen if SetCargoSlot was called

        if (currentSelectionQuantityText != null)
        {
            currentSelectionQuantityText.text = _currentCargoSlot.selectedQuantityForAction.ToString();
        }
        else
        {
            Debug.LogWarning("InventoryUIItem: currentSelectionQuantityText is not assigned.");
        }

        // Enable/disable buttons based on current selection and available quantity
        if (plusButton != null) plusButton.interactable = _currentCargoSlot.selectedQuantityForAction < _currentCargoSlot.quantity;
        if (plusPlusButton != null) plusPlusButton.interactable = _currentCargoSlot.selectedQuantityForAction + 10 <= _currentCargoSlot.quantity;
        if (minusButton != null) minusButton.interactable = _currentCargoSlot.selectedQuantityForAction > 0;
        if (minusMinusButton != null) minusMinusButton.interactable = _currentCargoSlot.selectedQuantityForAction - 10 >= 0;
    }

    // --- Button Click Handlers ---
    private void OnPlusClick()
    {
        if (_currentCargoSlot == null) return;
        _currentCargoSlot.selectedQuantityForAction = Mathf.Min(_currentCargoSlot.selectedQuantityForAction + 1, _currentCargoSlot.quantity);
        UpdateQuantityUI();
        Debug.Log($"Cargo: {_currentCargoSlot.cargoType.cargoName}, Selected: {_currentCargoSlot.selectedQuantityForAction}");
    }

    private void OnPlusPlusClick()
    {
        if (_currentCargoSlot == null) return;
        _currentCargoSlot.selectedQuantityForAction = Mathf.Min(_currentCargoSlot.selectedQuantityForAction + 10, _currentCargoSlot.quantity);
        UpdateQuantityUI();
        Debug.Log($"Cargo: {_currentCargoSlot.cargoType.cargoName}, Selected: {_currentCargoSlot.selectedQuantityForAction}");
    }

    private void OnMinusClick()
    {
        if (_currentCargoSlot == null) return;
        _currentCargoSlot.selectedQuantityForAction = Mathf.Max(_currentCargoSlot.selectedQuantityForAction - 1, 0);
        UpdateQuantityUI();
        Debug.Log($"Cargo: {_currentCargoSlot.cargoType.cargoName}, Selected: {_currentCargoSlot.selectedQuantityForAction}");
    }

    private void OnMinusMinusClick()
    {
        if (_currentCargoSlot == null) return;
        _currentCargoSlot.selectedQuantityForAction = Mathf.Max(_currentCargoSlot.selectedQuantityForAction - 10, 0);
        UpdateQuantityUI();
        Debug.Log($"Cargo: {_currentCargoSlot.cargoType.cargoName}, Selected: {_currentCargoSlot.selectedQuantityForAction}");
    }
}
