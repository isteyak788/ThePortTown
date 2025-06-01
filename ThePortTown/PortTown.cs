using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Linq; // For LINQ operations like .ToList()

public class PortTown : MonoBehaviour
{
    [Tooltip("The name of this Port Town.")]
    public string townName = "New Port Town";

    [Tooltip("Reference to the town's MoneyCollector script.")]
    public MoneyCollector townMoneyCollector; // Assign in Inspector

    [Header("Economy Settings")]
    [Tooltip("Current economy points of the town. Affects supply generation and purchasing power.")]
    public float economyPoints = 100f;
    [Tooltip("Rate at which economy points increase when the town makes profit (e.g., selling goods).")]
    public float economyGrowthRate = 0.1f;
    [Tooltip("Rate at which economy points decrease when the town loses money (e.g., buying goods at a loss).")]
    public float economyDecayRate = 0.05f;
    [Tooltip("Minimum economy points the town can have.")]
    public float minEconomyPoints = 10f;
    [Tooltip("Maximum economy points the town can have (optional cap).")]
    public float maxEconomyPoints = 1000f;

    [Header("Economy Conversion")]
    [Tooltip("How much currency translates to 1 economy point. E.g., 100 means $100 for 1 economy point.")]
    public float currencyPerEconomyPoint = 100f;

    [Header("Population Settings")]
    [Tooltip("Current population of the town. Affects needs, production, and town health.")]
    public int population = 100;
    [Tooltip("Rate at which population grows when needs are met (per tick).")]
    public float populationGrowthRate = 0.005f;
    [Tooltip("Rate at which population decreases when needs are not met (per tick).")]
    public float populationDecayRate = 0.015f;
    [Tooltip("Minimum population the town can have.")]
    public int minPopulation = 10;
    [Tooltip("Maximum population the town can have.")]
    public int maxPopulation = 1000;
    [Tooltip("How often population is evaluated (in seconds).")]
    public float populationEvaluationInterval = 120f;
    private float _populationEvaluationTimer;

    [Header("Player Investment Settings")]
    [Tooltip("Total amount of money the player has invested in this town.")]
    public float playerInvestmentAmount = 0f;
    [Tooltip("How often player investments are evaluated and paid out (in seconds).")]
    public float investmentPayoutInterval = 600f;
    private float _investmentPayoutTimer;
    [Tooltip("The base percentage of economy growth/decay applied to player's investment (e.g., 0.02 for 2%).")]
    public float baseInvestmentReturnRate = 0.01f;
    [Tooltip("Multiplier for the player's actual return on investment (e.g., 2.0 for 4% return when economy grows 2%).")]
    public float playerReturnMultiplier = 2.0f;
    private float _lastEconomyPoints;

    [Header("Supply Generation")]
    [Tooltip("List of cargo types this town is *naturally good at* generating.")]
    public List<Cargo> producibleCargoTypes = new List<Cargo>();
    [Tooltip("Base amount of cargo units generated per interval per economy point.")]
    public float baseProductionRatePerEconomyPoint = 0.005f;
    [Tooltip("Base amount of cargo units generated per interval per population unit (additive to economy).")]
    public float baseProductionRatePerPopulation = 0.002f;
    [Tooltip("How often the town generates new supplies (in seconds).")]
    public float supplyGenerationInterval = 60f;
    private float _supplyGenerationTimer;

    [System.Serializable]
    public class CargoGenerationModifier
    {
        public Cargo cargoType;
        [Tooltip("Multiplier for this cargo's generation rate. >1 for advantage, <1 for disadvantage.")]
        public float generationMultiplier = 1.0f;
    }

    [Tooltip("Specific multipliers for cargo generation rates based on town traits (e.g., biome).")]
    public List<CargoGenerationModifier> cargoGenerationModifiers = new List<CargoGenerationModifier>();
    [Tooltip("Multiplier applied to production rate for demanded items that are NOT explicitly in 'Producible Cargo Types'.")]
    [Range(0f, 1f)] public float defaultUnproducibleGenerationRateMultiplier = 0.1f;

    [System.Serializable]
    public class CargoDemand
    {
        [Tooltip("The type of cargo this town has a demand for.")]
        public Cargo cargoType;
        [Tooltip("The ideal quantity of this cargo the town tries to maintain. Influences prices.")]
        public int idealQuantity;
        [Tooltip("How much of this cargo is consumed per population unit per interval.")]
        public float consumptionRatePerPopulation;
        [Tooltip("Is this an essential cargo (e.g., food)? If unmet, it affects economy/population.")]
        public bool isEssential;
    }

    [Tooltip("List of cargo types this town has a demand for, and their consumption rates.")]
    public List<CargoDemand> townDemands = new List<CargoDemand>();

    [Tooltip("How often the town consumes goods and checks unmet needs (in seconds).")]
    public float consumptionInterval = 30f;
    private float _consumptionTimer;

    [Tooltip("Threshold percentage of essential goods below which economy/population suffer.")]
    [Range(0f, 1f)] public float essentialGoodsThreshold = 0.3f;
    [Tooltip("Multiplier for negative economy impact if essential needs are unmet.")]
    public float unmetNeedsEconomyPenaltyMultiplier = 0.75f;
    [Tooltip("Multiplier for negative population impact if essential needs are unmet.")]
    public float unmetNeedsPopulationPenaltyMultiplier = 1.0f;

    [Header("Current Goods in Town")]
    public Dictionary<Cargo, int> AvailableGoods
    {
        get { return availableGoods; }
        private set { availableGoods = value; }
    }
    [SerializeField] private Dictionary<Cargo, int> availableGoods = new Dictionary<Cargo, int>();

    [Tooltip("Multiplier for buying prices from the player. A value > 1 means the town pays more.")]
    public float buyPriceMultiplier = 1.1f;
    [Tooltip("Multiplier for selling prices to the player. A value < 1 means the town sells cheaper.")]
    public float sellPriceMultiplier = 0.9f;

    [Header("Price Fluctuation Settings")]
    [Tooltip("How strongly prices are affected by supply vs demand. Higher = more volatile.")]
    public float priceElasticity = 0.4f;
    [Tooltip("Minimum price multiplier (e.g., 0.5 means cannot sell for less than 50% of base).")]
    public float minPriceMultiplier = 0.6f;
    [Tooltip("Maximum price multiplier (e.g., 2.0 means cannot sell for more than 200% of base).")]
    public float maxPriceMultiplier = 1.8f;

    // UI Events
    public delegate void OnEconomyChanged(float newEconomyPoints);
    public event OnEconomyChanged onEconomyChanged;

    public delegate void OnPopulationChanged(int newPopulation);
    public event OnPopulationChanged onPopulationChanged;

    public delegate void OnGoodsAvailabilityChanged(Cargo cargoType, int newQuantity);
    public event OnGoodsAvailabilityChanged onGoodsAvailabilityChanged;

    public delegate void OnTownDataChanged();
    public event OnTownDataChanged onTownDataChanged;

    private MoneyCollector _playerMoneyCollector;


    void Start()
    {
        if (townMoneyCollector == null)
        {
            townMoneyCollector = GetComponent<MoneyCollector>();
            if (townMoneyCollector == null)
            {
                Debug.LogError($"PortTown ({townName}): Requires a MoneyCollector component on the same GameObject or assigned in Inspector.");
                enabled = false;
                return;
            }
        }

        // Initialize available goods for all producible and demanded types
        HashSet<Cargo> initialCargoTypes = new HashSet<Cargo>();
        foreach (Cargo cargo in producibleCargoTypes) initialCargoTypes.Add(cargo);
        foreach (CargoDemand demand in townDemands) initialCargoTypes.Add(demand.cargoType);

        foreach (Cargo cargo in initialCargoTypes)
        {
            if (!availableGoods.ContainsKey(cargo))
            {
                availableGoods.Add(cargo, 0);
            }
        }

        _supplyGenerationTimer = supplyGenerationInterval;
        _consumptionTimer = consumptionInterval;
        _populationEvaluationTimer = populationEvaluationInterval;
        _investmentPayoutTimer = investmentPayoutInterval;
        _lastEconomyPoints = economyPoints;

        Debug.Log($"{townName} initialized with {economyPoints} economy points and {population} population.");

        onEconomyChanged?.Invoke(economyPoints);
        onPopulationChanged?.Invoke(population);
        onTownDataChanged?.Invoke();
    }

    void Update()
    {
        _supplyGenerationTimer -= Time.deltaTime;
        if (_supplyGenerationTimer <= 0)
        {
            GenerateSupplies();
            _supplyGenerationTimer = supplyGenerationInterval;
        }

        _consumptionTimer -= Time.deltaTime;
        if (_consumptionTimer <= 0)
        {
            ConsumeGoods();
            _consumptionTimer = consumptionInterval;
        }

        _populationEvaluationTimer -= Time.deltaTime;
        if (_populationEvaluationTimer <= 0)
        {
            EvaluatePopulation();
            _populationEvaluationTimer = populationEvaluationInterval;
        }

        _investmentPayoutTimer -= Time.deltaTime;
        if (_investmentPayoutTimer <= 0)
        {
            EvaluatePlayerInvestments();
            _investmentPayoutTimer = investmentPayoutInterval;
        }
    }

    public void AdjustEconomy(float change)
    {
        economyPoints += change;
        economyPoints = Mathf.Clamp(economyPoints, minEconomyPoints, maxEconomyPoints);
        onEconomyChanged?.Invoke(economyPoints);
        onTownDataChanged?.Invoke();
    }

    public void AdjustPopulation(int change)
    {
        population += change;
        population = Mathf.Clamp(population, minPopulation, maxPopulation);
        onPopulationChanged?.Invoke(population);
        Debug.Log($"{townName} population adjusted by {change}. New population: {population}");
        onTownDataChanged?.Invoke();
    }

    /// <summary>
    /// Generates new supplies based on economy points, population, and demand,
    /// considering specific production advantages/disadvantages.
    /// </summary>
    private void GenerateSupplies()
    {
        HashSet<Cargo> cargoTypesToAttemptGeneration = new HashSet<Cargo>();
        foreach (Cargo cargo in producibleCargoTypes)
        {
            cargoTypesToAttemptGeneration.Add(cargo);
        }
        foreach (CargoDemand demand in townDemands)
        {
            cargoTypesToAttemptGeneration.Add(demand.cargoType);
        }

        if (cargoTypesToAttemptGeneration.Count == 0) return;

        foreach (Cargo cargoType in cargoTypesToAttemptGeneration)
        {
            float currentGenerationMultiplier = 1.0f;
            float currentBaseProductionRate;

            bool isExplicitlyProducible = producibleCargoTypes.Contains(cargoType);

            if (isExplicitlyProducible)
            {
                currentBaseProductionRate = (economyPoints * baseProductionRatePerEconomyPoint) +
                                            (population * baseProductionRatePerPopulation);
            }
            else
            {
                currentBaseProductionRate = (economyPoints * baseProductionRatePerEconomyPoint * defaultUnproducibleGenerationRateMultiplier) +
                                            (population * baseProductionRatePerPopulation * defaultUnproducibleGenerationRateMultiplier);
            }

            CargoGenerationModifier modifier = cargoGenerationModifiers.Find(m => m.cargoType == cargoType);
            if (modifier != null)
            {
                currentGenerationMultiplier *= modifier.generationMultiplier;
            }

            currentBaseProductionRate = Mathf.Max(0f, currentBaseProductionRate); // Ensure not negative
            int amountGenerated = Mathf.RoundToInt(currentBaseProductionRate * currentGenerationMultiplier * Random.Range(0.8f, 1.2f));
            amountGenerated = Mathf.Max(amountGenerated, 0); // Ensure amount is not negative

            if (amountGenerated > 0)
            {
                AddCargoToTown(cargoType, amountGenerated);
            }
        }
        onTownDataChanged?.Invoke();
        Debug.Log($"{townName} generated supplies with demand-driven production and specific modifiers.");
    }

    private void ConsumeGoods()
    {
        bool hasUnmetEssentialNeeds = false;
        foreach (CargoDemand demand in townDemands)
        {
            int quantityToConsume = Mathf.RoundToInt(demand.consumptionRatePerPopulation * population);
            if (quantityToConsume <= 0) continue;

            if (availableGoods.ContainsKey(demand.cargoType))
            {
                int currentQuantity = availableGoods[demand.cargoType];
                int actualConsumed = Mathf.Min(currentQuantity, quantityToConsume);

                availableGoods[demand.cargoType] -= actualConsumed;

                if (demand.isEssential)
                {
                    if (availableGoods[demand.cargoType] < demand.idealQuantity * essentialGoodsThreshold)
                    {
                        hasUnmetEssentialNeeds = true;
                        Debug.LogWarning($"{townName}: Low on essential {demand.cargoType.cargoName}! Current: {availableGoods[demand.cargoType]}, Ideal Threshold: {demand.idealQuantity * essentialGoodsThreshold:F0}");
                    }
                }
            }
            else
            {
                if (demand.isEssential)
                {
                    hasUnmetEssentialNeeds = true;
                    Debug.LogWarning($"{townName}: Completely out of essential {demand.cargoType.cargoName}!");
                }
            }
            onGoodsAvailabilityChanged?.Invoke(demand.cargoType, availableGoods.ContainsKey(demand.cargoType) ? availableGoods[demand.cargoType] : 0);
        }

        // Excess goods depletion
        foreach (var cargoEntry in availableGoods.ToList())
        {
            Cargo cargoType = cargoEntry.Key;
            int currentQuantity = cargoEntry.Value;

            CargoDemand demand = townDemands.Find(d => d.cargoType == cargoType);

            // If no demand, or current quantity is significantly above ideal, deplete excess
            if (demand == null || currentQuantity > demand.idealQuantity * 1.5f)
            {
                int excessDepletion = Mathf.RoundToInt(currentQuantity * 0.05f); // Deplete 5% of excess
                if (excessDepletion > 0)
                {
                    RemoveCargoFromTown(cargoType, excessDepletion);
                }
            }
        }

        if (hasUnmetEssentialNeeds)
        {
            AdjustEconomy(-economyPoints * economyDecayRate * unmetNeedsEconomyPenaltyMultiplier);
            AdjustPopulation(Mathf.RoundToInt(-population * populationDecayRate * unmetNeedsPopulationPenaltyMultiplier));
            Debug.LogWarning($"{townName}: Economy and population suffered due to unmet essential needs!");
        }
        onTownDataChanged?.Invoke();
    }

    private void EvaluatePopulation()
    {
        if (economyPoints > maxEconomyPoints * 0.75f && population < maxPopulation)
        {
            AdjustPopulation(Mathf.RoundToInt(population * populationGrowthRate));
        }
        else if (economyPoints < minEconomyPoints * 1.5f && population > minPopulation)
        {
            AdjustPopulation(Mathf.RoundToInt(-population * populationDecayRate));
        }
    }

    private void EvaluatePlayerInvestments()
    {
        if (playerInvestmentAmount <= 0) return;

        float economyChange = economyPoints - _lastEconomyPoints;
        float economyChangePercentage = 0f;
        if (_lastEconomyPoints > 0)
        {
            economyChangePercentage = economyChange / _lastEconomyPoints;
        }

        float baseReturn = economyChangePercentage * baseInvestmentReturnRate;
        float actualReturnRate = baseReturn * playerReturnMultiplier;
        float payoutAmount = playerInvestmentAmount * actualReturnRate;

        if (Mathf.Abs(payoutAmount) > 0.01f)
        {
            if (_playerMoneyCollector != null)
            {
                _playerMoneyCollector.AddMoney(payoutAmount, $"Investment return from {townName}");
                Debug.Log($"Player investment in {townName} yielded {payoutAmount:C2}. (Economy change: {economyChangePercentage:P2})");
            }
            else
            {
                Debug.LogWarning($"PlayerMoneyCollector not assigned to PortTown '{townName}'. Cannot payout investment return.");
            }
        }

        _lastEconomyPoints = economyPoints;
        onTownDataChanged?.Invoke();
    }

    public void SetPlayerMoneyCollector(MoneyCollector playerMC)
    {
        _playerMoneyCollector = playerMC;
    }

    public void AddCargoToTown(Cargo cargoType, int quantity)
    {
        if (cargoType == null || quantity <= 0) return;

        if (availableGoods.ContainsKey(cargoType))
        {
            availableGoods[cargoType] += quantity;
        }
        else
        {
            availableGoods.Add(cargoType, quantity);
        }
        Debug.Log($"{townName} received {quantity} units of {cargoType.cargoName}. Total now: {availableGoods[cargoType]}");
        onGoodsAvailabilityChanged?.Invoke(cargoType, availableGoods[cargoType]);
        onTownDataChanged?.Invoke();
    }

    public bool RemoveCargoFromTown(Cargo cargoType, int quantity)
    {
        if (cargoType == null || quantity <= 0) return false;

        if (availableGoods.ContainsKey(cargoType) && availableGoods[cargoType] >= quantity)
        {
            availableGoods[cargoType] -= quantity;
            if (availableGoods[cargoType] == 0)
            {
                availableGoods.Remove(cargoType);
            }
            onGoodsAvailabilityChanged?.Invoke(cargoType, GetAvailableCargoQuantity(cargoType));
            onTownDataChanged?.Invoke();
            return true;
        }
        Debug.LogWarning($"{townName}: Tried to remove {quantity} {cargoType.cargoName}, but only {GetAvailableCargoQuantity(cargoType)} available.");
        return false;
    }

    public void InvestProfitIntoEconomy(float netProfit)
    {
        if (netProfit <= 0) return;

        float economyPointsGained = netProfit / currencyPerEconomyPoint;
        AdjustEconomy(economyPointsGained);

        Debug.Log($"{townName} invested profit: {netProfit:C2}. Converted to {economyPointsGained:F2} economy points.");
        onTownDataChanged?.Invoke();
    }

    public float BuyCargoFromPlayer(Cargo cargoType, int quantity)
    {
        if (cargoType == null || quantity <= 0) return 0f;

        float unitPrice = GetBuyPricePerUnit(cargoType);
        float totalCost = unitPrice * quantity;

        if (townMoneyCollector.RemoveMoney(totalCost, $"Purchasing {quantity} {cargoType.cargoName} from Player"))
        {
            AddCargoToTown(cargoType, quantity);
            AdjustEconomy(totalCost * 0.005f); // Minor direct boost for acquiring goods
            onTownDataChanged?.Invoke();
            return totalCost;
        }
        else
        {
            Debug.LogWarning($"{townName}: Not enough money to buy {quantity} units of {cargoType.cargoName}. Required: {totalCost:C2}, Current: {townMoneyCollector.currentBalance:C2}");
            return 0f;
        }
    }

    public float SellCargoToPlayer(Cargo cargoType, int quantity)
    {
        if (cargoType == null || quantity <= 0) return 0f;

        if (!availableGoods.ContainsKey(cargoType) || availableGoods[cargoType] < quantity)
        {
            Debug.LogWarning($"{townName}: Not enough {cargoType.cargoName} to sell. Requested: {quantity}, Available: {(availableGoods.ContainsKey(cargoType) ? availableGoods[cargoType] : 0)}");
            return 0f;
        }

        float unitPrice = GetSellPricePerUnit(cargoType);
        float totalRevenue = unitPrice * quantity;

        townMoneyCollector.AddMoney(totalRevenue, $"Selling {quantity} {cargoType.cargoName} to Player");
        RemoveCargoFromTown(cargoType, quantity);
        InvestProfitIntoEconomy(totalRevenue); // Direct investment of revenue into economy
        onTownDataChanged?.Invoke();
        return totalRevenue;
    }

    public int GetAvailableCargoQuantity(Cargo cargoType)
    {
        if (availableGoods.ContainsKey(cargoType))
        {
            return availableGoods[cargoType];
        }
        return 0;
    }

    public float GetSellPricePerUnit(Cargo cargoType)
    {
        if (cargoType == null) return 0f;

        float baseValue = cargoType.baseValuePerUnit;
        float currentPrice = baseValue * sellPriceMultiplier;

        CargoDemand demand = townDemands.Find(d => d.cargoType == cargoType);
        if (demand != null && demand.idealQuantity > 0)
        {
            int currentQty = GetAvailableCargoQuantity(cargoType);
            float supplyRatio = (float)currentQty / demand.idealQuantity;
            float priceAdjustmentFactor = 1f + ((1f - supplyRatio) * priceElasticity);
            currentPrice *= priceAdjustmentFactor;
        }

        currentPrice = Mathf.Clamp(currentPrice, baseValue * minPriceMultiplier, baseValue * maxPriceMultiplier);
        return currentPrice;
    }

    public float GetBuyPricePerUnit(Cargo cargoType)
    {
        if (cargoType == null) return 0f;

        float baseValue = cargoType.baseValuePerUnit;
        float currentPrice = baseValue * buyPriceMultiplier;

        CargoDemand demand = townDemands.Find(d => d.cargoType == cargoType);
        if (demand != null && demand.idealQuantity > 0)
        {
            int currentQty = GetAvailableCargoQuantity(cargoType);
            float supplyRatio = (float)currentQty / demand.idealQuantity;
            float priceAdjustmentFactor = 1f + ((1f - supplyRatio) * priceElasticity);
            currentPrice *= priceAdjustmentFactor;
        }

        currentPrice = Mathf.Clamp(currentPrice, baseValue * minPriceMultiplier, baseValue * maxPriceMultiplier);
        return currentPrice;
    }

    /// <summary>
    /// Allows the player to "invest" money into the town's economy.
    /// </summary>
    /// <param name="amount">The amount of money the player invests.</param>
    /// <param name="playerMoneyCollector">The player's MoneyCollector instance.</param>
    /// <returns>True if the investment was successful (player had enough money), false otherwise.</returns>
    public bool PlayerInvestInEconomy(float amount, MoneyCollector playerMoneyCollector)
    {
        if (amount <= 0)
        {
            Debug.LogWarning($"Player cannot invest 0 or negative amount.");
            return false;
        }

        if (playerMoneyCollector == null)
        {
            Debug.LogError("PlayerMoneyCollector is not provided for investment.");
            return false;
        }

        if (playerMoneyCollector.RemoveMoney(amount, $"Investment in {townName}"))
        {
            playerInvestmentAmount += amount;
            AdjustEconomy(amount * economyGrowthRate * 0.05f); // Small economy boost from player investment
            Debug.Log($"Player invested {amount:C2} in {townName}. Total investment now: {playerInvestmentAmount:C2}");
            onTownDataChanged?.Invoke();
            return true;
        }
        else
        {
            Debug.LogWarning($"Player does not have enough money to invest {amount:C2} in {townName}.");
            return false;
        }
    }
}
