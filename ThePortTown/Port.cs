using UnityEngine;
using TMPro; // For TextMeshPro
using System; // For Action

public class Port : MonoBehaviour
{
    public string portName = "New Port";
    public float taxPercentage = 0.1f; // 10% tax on player sales to this port
    public MoneyCollector portMoneyCollector; // Money collector for the port itself

    [Header("Connections")]
    public PortTown connectedPortTown; // Reference to the PortTown script

    void Start()
    {
        if (connectedPortTown == null)
        {
            Debug.LogError($"Port '{portName}' is missing a reference to its PortTown script!", this);
        }
        if (portMoneyCollector == null)
        {
            portMoneyCollector = GetComponent<MoneyCollector>();
            if (portMoneyCollector == null)
            {
                Debug.LogError($"Port '{portName}' is missing a MoneyCollector component!", this);
            }
        }
    }

    void OnTriggerEnter(Collider other)
    {
        ShipCargoHandler playerShipCargoHandler = other.GetComponent<ShipCargoHandler>();
        if (playerShipCargoHandler != null)
        {
            Debug.Log($"[Port] Player entered {portName} general zone. Setting currentPort on playerShipCargoHandler.");
            playerShipCargoHandler.SetCurrentPort(this);
            // This implicitly calls UIManager.RefreshAllUI() indirectly if UIManager is set up to listen
            // However, we can make it explicit if needed.
        }
    }

    void OnTriggerExit(Collider other)
    {
        ShipCargoHandler playerShipCargoHandler = other.GetComponent<ShipCargoHandler>();
        if (playerShipCargoHandler != null)
        {
            Debug.Log($"[Port] Player exited {portName} general zone. Clearing currentPort on playerShipCargoHandler.");
            playerShipCargoHandler.ClearCurrentPort();
        }
    }
}
