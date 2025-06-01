using UnityEngine;

public class PortTradeTrigger : MonoBehaviour
{
    [Tooltip("Reference to the Port component associated with this trigger.")]
    public Port connectedPort; // Assign in Inspector

    private ShipCargoHandler playerShipCargoHandler;

    void Start()
    {
        if (connectedPort == null)
        {
            Debug.LogError("PortTradeTrigger: 'connectedPort' is not assigned. Please assign the Port component in the Inspector.", this);
            enabled = false; // Disable script if no port is assigned
            return;
        }

        // --- FIX: Replaced obsolete FindObjectOfType with FindFirstObjectByType ---
        playerShipCargoHandler = FindFirstObjectByType<ShipCargoHandler>();
        // ---------------------------------------------------------------------

        if (playerShipCargoHandler == null)
        {
            Debug.LogError("PortTradeTrigger: ShipCargoHandler (Player Ship) not found in the scene. Make sure the player ship has a ShipCargoHandler component.", this);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (playerShipCargoHandler == null)
        {
            Debug.LogWarning("PortTradeTrigger: Player Ship Cargo Handler is null. Cannot process trigger enter.");
            return;
        }

        // Assuming the player ship has a specific tag, e.g., "Player"
        if (other.CompareTag("Player"))
        {
            // Set the player's current port in their ShipCargoHandler
            playerShipCargoHandler.SetCurrentPort(connectedPort);
            Debug.Log($"{playerShipCargoHandler.gameObject.name} entered {connectedPort.portName} trade zone.");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (playerShipCargoHandler == null)
        {
            Debug.LogWarning("PortTradeTrigger: Player Ship Cargo Handler is null. Cannot process trigger exit.");
            return;
        }

        if (other.CompareTag("Player"))
        {
            // Clear the player's current port in their ShipCargoHandler
            playerShipCargoHandler.ClearCurrentPort();
            Debug.Log($"{playerShipCargoHandler.gameObject.name} exited {connectedPort.portName} trade zone.");
        }
    }
}
