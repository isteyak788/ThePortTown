using UnityEngine;

[CreateAssetMenu(fileName = "NewCargo", menuName = "Cargo/Cargo Type")]
public class Cargo : ScriptableObject
{
    public string cargoName = "New Cargo";
    
    [Tooltip("The visual icon representing this cargo type in the UI.")]
    public Sprite cargoIcon; // <--- THIS IS THE NEWLY ADDED FIELD

    [Tooltip("The base value of one unit of this cargo.")]
    public float baseValuePerUnit = 10f;
    
    [Tooltip("How much 'space' or 'weight' one unit of this cargo takes on a ship.")]
    public int baseCapacity = 1; // Example: could be units, weight, etc.

    // You can add more properties here as needed for your game mechanics,
    // e.g., perishability, rarity, special effects, etc.
}
