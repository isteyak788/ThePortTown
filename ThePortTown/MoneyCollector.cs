using UnityEngine;
using TMPro; // Make sure you have TextMeshPro imported

public class MoneyCollector : MonoBehaviour
{
    [Tooltip("The current money balance for this entity.")]
    public float currentBalance = 0f;

    [Tooltip("A descriptive name for the owner of this account (e.g., 'Player', 'Port Alpha').")]
    public string accountOwnerName = "Unnamed Account";

    public delegate void OnBalanceChanged(float newBalance);
    public event OnBalanceChanged onBalanceChanged;

    private void Start()
    {
        // Invoke on start to ensure UI elements display initial balance
        onBalanceChanged?.Invoke(currentBalance);
    }

    /// <summary>
    /// Adds money to the account.
    /// </summary>
    /// <param name="amount">The amount of money to add.</param>
    /// <param name="source">A string describing where the money came from (e.g., "Cargo Sale", "Tax Collection").</param>
    public void AddMoney(float amount, string source = "Unknown Source")
    {
        if (amount < 0)
        {
            Debug.LogWarning($"{accountOwnerName}: Attempted to add negative money. Use RemoveMoney for deductions. Amount: {amount} from {source}");
            return;
        }

        currentBalance += amount;
        Debug.Log($"{accountOwnerName} received {amount:C2} from {source}. New balance: {currentBalance:C2}");
        onBalanceChanged?.Invoke(currentBalance);
    }

    /// <summary>
    /// Removes money from the account.
    /// </summary>
    /// <param name="amount">The amount of money to remove.</param>
    /// <param name="reason">A string describing why the money was removed (e.g., "Purchase", "Fine").</param>
    /// <returns>True if money was successfully removed, false if not enough balance.</returns>
    public bool RemoveMoney(float amount, string reason = "Unknown Reason")
    {
        if (amount < 0)
        {
            Debug.LogWarning($"{accountOwnerName}: Attempted to remove negative money. Use AddMoney for additions. Amount: {amount} for {reason}");
            return false;
        }

        if (currentBalance >= amount)
        {
            currentBalance -= amount;
            Debug.Log($"{accountOwnerName} paid {amount:C2} for {reason}. New balance: {currentBalance:C2}");
            onBalanceChanged?.Invoke(currentBalance);
            return true;
        }
        else
        {
            Debug.LogWarning($"{accountOwnerName}: Insufficient funds to remove {amount:C2} for {reason}. Current balance: {currentBalance:C2}");
            return false;
        }
    }
}
