using UnityEngine;
using System.Collections.Generic; // Required for List

public class CannonController : MonoBehaviour
{
    // Enum to select the local firing direction axis
    public enum FiringDirectionAxis
    {
        Local_Z_Plus,  // Default: cannon.forward (blue arrow)
        Local_Z_Minus, // -cannon.forward (opposite blue arrow)
        Local_X_Plus,  // cannon.right (red arrow)
        Local_X_Minus, // -cannon.right (opposite red arrow)
        Local_Y_Plus,  // cannon.up (green arrow)
        Local_Y_Minus  // -cannon.up (opposite green arrow)
    }

    // Nested class to hold bullet configuration properties
    [System.Serializable]
    public class BulletConfig
    {
        [Tooltip("The prefab GameObject for the cannonball.")]
        public GameObject bulletPrefab;
        [Tooltip("The speed at which the cannonball travels.")]
        public float bulletSpeed = 50.0f;
        [Tooltip("How long the cannonball exists before being destroyed (in seconds).")]
        public float bulletLifetime = 3.0f;
        [Tooltip("The damage dealt by the cannonball.")]
        public int damage = 10;
        
        [Tooltip("The local axis direction from which the bullet will be fired relative to the cannon's rotation.")]
        public FiringDirectionAxis fireDirectionAxis = FiringDirectionAxis.Local_Z_Plus;

        [Tooltip("Optional: Prefab for a muzzle flash effect.")]
        public GameObject muzzleFlashPrefab;
        [Tooltip("Optional: Audio clip to play when a cannon fires.")]
        public AudioClip fireSound;
        [Tooltip("Volume of the fire sound.")]
        [Range(0f, 1f)]
        public float fireSoundVolume = 0.8f;
    }

    [Header("Cannon Setup")]
    [Tooltip("Drag one or more parent GameObjects here. Each parent should contain individual cannon visual meshes as its children.")]
    public List<Transform> cannonParents; // CHANGED: Now a List to allow multiple parents

    [Header("Firing Settings")]
    [Tooltip("Time between shots for all cannons (in seconds).")]
    public float fireRate = 1.0f;
    [Tooltip("The key to press to fire all cannons.")]
    public KeyCode fireKey = KeyCode.Space;

    [Header("Bullet Configuration")]
    public BulletConfig bulletConfiguration; // Instance of the BulletConfig class

    private List<Transform> cannons = new List<Transform>(); // List to store individual cannon transforms
    private float nextFireTime; // When the ship can fire next

    void Awake()
    {
        if (cannonParents == null || cannonParents.Count == 0)
        {
            Debug.LogError("CannonController: No 'Cannon Parents' assigned. Please assign one or more GameObjects that hold your cannons.", this);
            enabled = false; 
            return;
        }

        if (bulletConfiguration.bulletPrefab == null)
        {
            Debug.LogError("CannonController: 'Bullet Prefab' in Bullet Configuration is not assigned. Please assign a bullet prefab.", this);
            enabled = false;
            return;
        }

        // Populate the cannons list by iterating through children of ALL assigned cannonParents
        PopulateCannonsList();
        
        // Ensure the bullet prefab has a Rigidbody for physics
        if (bulletConfiguration.bulletPrefab.GetComponent<Rigidbody>() == null)
        {
            Debug.LogWarning("CannonController: Bullet Prefab does not have a Rigidbody. It might not move as expected.", bulletConfiguration.bulletPrefab);
        }
        // Ensure the bullet prefab has a Collider for collisions
        if (bulletConfiguration.bulletPrefab.GetComponent<Collider>() == null)
        {
            Debug.LogWarning("CannonController: Bullet Prefab does not have a Collider. It might pass through objects.", bulletConfiguration.bulletPrefab);
        }
        
        // Initialize nextFireTime to allow immediate firing on start
        nextFireTime = Time.time;
    }

    // This method populates the list of all cannons from all assigned parents
    void PopulateCannonsList()
    {
        cannons.Clear(); // Clear any existing cannons in the list

        foreach (Transform parent in cannonParents)
        {
            if (parent == null)
            {
                Debug.LogWarning("CannonController: One of the assigned 'Cannon Parents' is null. Skipping it.", this);
                continue; // Skip this null entry and proceed to the next parent
            }

            // Add all direct children of the current parent to the list of cannons
            foreach (Transform childCannon in parent)
            {
                cannons.Add(childCannon);
            }
        }

        if (cannons.Count == 0)
        {
            Debug.LogWarning("CannonController: No cannon GameObjects found under any of the assigned Cannon Parents. Cannons will not fire.", this);
        }
    }

    void Update()
    {
        // Check if enough time has passed since the last shot and if the fire key is pressed
        if (Input.GetKeyDown(fireKey) && Time.time >= nextFireTime)
        {
            FireAllCannons();
            nextFireTime = Time.time + 1f / fireRate; // Set next allowed fire time
        }
    }

    void FireAllCannons()
    {
        if (cannons.Count == 0)
        {
            Debug.LogWarning("CannonController: No cannons to fire! Check 'Cannon Parents' assignment and their children.", this);
            return;
        }

        foreach (Transform cannon in cannons)
        {
            // Determine the firing direction based on the selected axis
            Vector3 fireDirection;
            switch (bulletConfiguration.fireDirectionAxis)
            {
                case FiringDirectionAxis.Local_Z_Plus:
                    fireDirection = cannon.forward;
                    break;
                case FiringDirectionAxis.Local_Z_Minus:
                    fireDirection = -cannon.forward;
                    break;
                case FiringDirectionAxis.Local_X_Plus:
                    fireDirection = cannon.right;
                    break;
                case FiringDirectionAxis.Local_X_Minus:
                    fireDirection = -cannon.right;
                    break;
                case FiringDirectionAxis.Local_Y_Plus:
                    fireDirection = cannon.up;
                    break;
                case FiringDirectionAxis.Local_Y_Minus:
                    fireDirection = -cannon.up;
                    break;
                default:
                    fireDirection = cannon.forward; // Default to forward if somehow unhandled
                    break;
            }

            // Instantiate bullet at the cannon's position and rotation
            // We use cannon.rotation for the bullet's initial rotation, but the velocity is controlled by fireDirection
            GameObject bulletInstance = Instantiate(
                bulletConfiguration.bulletPrefab,
                cannon.position,
                cannon.rotation 
            );
            
            // Get bullet's Rigidbody and apply force
            Rigidbody bulletRb = bulletInstance.GetComponent<Rigidbody>();
            if (bulletRb != null)
            {
                // Set bullet's initial velocity in the chosen direction
                bulletRb.linearVelocity = fireDirection * bulletConfiguration.bulletSpeed;
            }
            else
            {
                Debug.LogWarning("CannonController: Bullet prefab '" + bulletConfiguration.bulletPrefab.name + "' is missing a Rigidbody. Cannot apply fire force.", bulletConfiguration.bulletPrefab);
            }

            // --- Instantiate Muzzle Flash (Optional) ---
            if (bulletConfiguration.muzzleFlashPrefab != null)
            {
                // Instantiate muzzle flash at cannon position and rotation, make it a temporary child of cannon
                GameObject muzzleFlash = Instantiate(
                    bulletConfiguration.muzzleFlashPrefab,
                    cannon.position,
                    cannon.rotation, // Muzzle flash should generally align with the cannon's barrel
                    cannon // Make it a child of the cannon for easy positioning relative to the barrel
                );
                Destroy(muzzleFlash, 0.5f); // Adjust duration as needed
            }

            // --- Play Fire Sound (Optional) ---
            if (bulletConfiguration.fireSound != null)
            {
                // Play sound at the cannon's position
                AudioSource.PlayClipAtPoint(bulletConfiguration.fireSound, cannon.position, bulletConfiguration.fireSoundVolume);
            }

            // Destroy the bullet after its configured lifespan
            Destroy(bulletInstance, bulletConfiguration.bulletLifetime);
        }
    }
}
