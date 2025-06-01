using UnityEngine;

public class PlayerTriggerDetector : MonoBehaviour
{
    [Tooltip("The layer(s) that represent other ships.")]
    public LayerMask otherShipLayer;

    [Tooltip("Reference to the UIManager in the scene.")]
    public UIManager uiManager; // Assign in Inspector

    void Start()
    {
        if (uiManager == null)
        {
            Debug.LogError("UIManager reference is missing in PlayerTriggerDetector!");
        }

        // Ensure this GameObject has a Trigger Collider (e.g., SphereCollider) and a Rigidbody
        Collider collider = GetComponent<Collider>();
        if (collider == null || !collider.isTrigger)
        {
            Debug.LogError("PlayerTriggerDetector requires a Trigger Collider on this GameObject.");
            enabled = false;
            return;
        }
        if (GetComponent<Rigidbody>() == null)
        {
            Debug.LogWarning("PlayerTriggerDetector recommends a Rigidbody on this GameObject for trigger events.");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the entering object is on the otherShipLayer
        if (((1 << other.gameObject.layer) & otherShipLayer) != 0)
        {
            ShipCargoHandler otherShip = other.GetComponentInParent<ShipCargoHandler>();
            if (otherShip != null && otherShip != GetComponentInParent<ShipCargoHandler>()) // Make sure it's not self
            {
                if (uiManager != null)
                {
                    uiManager.SetPlayerNearbyShip(otherShip);
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        // Check if the exiting object is on the otherShipLayer
        if (((1 << other.gameObject.layer) & otherShipLayer) != 0)
        {
            ShipCargoHandler otherShip = other.GetComponentInParent<ShipCargoHandler>();
            if (otherShip != null && otherShip != GetComponentInParent<ShipCargoHandler>()) // Make sure it's not self
            {
                if (uiManager != null)
                {
                    uiManager.ClearPlayerNearbyShip();
                }
            }
        }
    }
}
