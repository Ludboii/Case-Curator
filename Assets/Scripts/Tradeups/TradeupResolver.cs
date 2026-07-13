using System;
using System.Collections.Generic;
using UnityEngine;

#region Tradeup Data Classes

[Serializable]
public class CovertTradeupPoolMapping
{
    [Tooltip("The collection/source used by the Covert input skins.")]
    public CollectionData sourceCollection;

    [Tooltip(
        "The case whose Rare Special drop pool should be used " +
        "when this collection wins the source roll.")]
    public CaseData rareSpecialCase;
}

[Serializable]
public class TradeupValidationResult
{
    public bool isValid;
    public string errorMessage;

    public int requiredInputCount;
    public Rarity inputRarity;
    public Rarity outputRarity;

    public bool isStatTrak;
    public bool isCovertTradeup;

    public static TradeupValidationResult Invalid(string message)
    {
        return new TradeupValidationResult
        {
            isValid = false,
            errorMessage = message
        };
    }
}

[Serializable]
public class TradeupSourceChance
{
    public CollectionData source;
    public string sourceApiId;
    public string sourceName;

    public int inputCount;

    [Range(0f, 1f)]
    public float probability;
}

[Serializable]
public class TradeupOutcomeChance
{
    public CollectionData source;
    public string sourceApiId;
    public string sourceName;

    public SkinData skin;

    [Range(0f, 1f)]
    public float probability;
}

[Serializable]
public class TradeupPreview
{
    public TradeupValidationResult validation =
        new TradeupValidationResult();

    public int inputCount;
    public Rarity inputRarity;
    public Rarity outputRarity;

    public bool isStatTrak;
    public bool isCovertTradeup;

    public double averageInputFloat;
    public float totalInputMarketValue;

    public List<TradeupSourceChance> sourceChances =
        new List<TradeupSourceChance>();

    public List<TradeupOutcomeChance> possibleOutcomes =
        new List<TradeupOutcomeChance>();
}

[Serializable]
public class TradeupExecutionResult
{
    public bool success;
    public string errorMessage;

    public TradeupPreview preview;
    public TradeupOutcomeChance rolledOutcome;
    public InventoryItem outputItem;
    public TradeupHistorySaveData historyRecord;

    public static TradeupExecutionResult Failed(
        string message,
        TradeupPreview preview = null)
    {
        return new TradeupExecutionResult
        {
            success = false,
            errorMessage = message,
            preview = preview
        };
    }
}

#endregion

[DisallowMultipleComponent]
public class TradeupResolver : MonoBehaviour
{
    public static TradeupResolver Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private GameDatabase database;

    [Header("Covert → Rare Special Pools")]
    [Tooltip(
        "Each Covert skin collection must point to the case that contains " +
        "its Rare Special knife/glove pool.")]
    [SerializeField]
    private List<CovertTradeupPoolMapping> covertTradeupPools =
        new List<CovertTradeupPoolMapping>();

    [Header("Rules")]
    [SerializeField, Min(1)]
    private int standardInputCount = 10;

    [SerializeField, Min(1)]
    private int covertInputCount = 5;

    [SerializeField]
    private bool rejectFavoritedItems = true;

    [SerializeField]
    private bool logCompletedTradeups = true;

    private sealed class WeightedSkinCandidate
    {
        public SkinData skin;
        public float weight;
    }

    private GameDatabase ActiveDatabase
    {
        get
        {
            if (database != null)
                return database;

            if (SaveManager.Instance != null)
                return SaveManager.Instance.database;

            return null;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "Duplicate TradeupResolver found. Destroying: " +
                gameObject.name);

            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #region Validation

    public TradeupValidationResult ValidateInputs(
        IReadOnlyList<InventoryItem> inputs)
    {
        if (inputs == null || inputs.Count == 0)
            return TradeupValidationResult.Invalid(
                "No tradeup inputs selected.");

        GameDatabase activeDatabase = ActiveDatabase;

        if (activeDatabase == null)
            return TradeupValidationResult.Invalid(
                "TradeupResolver has no GameDatabase.");

        InventoryItem firstItem = inputs[0];

        if (firstItem == null || firstItem.skin == null)
            return TradeupValidationResult.Invalid(
                "The first selected item is invalid.");

        Rarity inputRarity = firstItem.skin.rarity;

        if (inputRarity == Rarity.RareSpecial)
            return TradeupValidationResult.Invalid(
                "Rare Special items cannot be used in tradeups.");

        bool covertTradeup = inputRarity == Rarity.Covert;

        int requiredCount = covertTradeup
            ? covertInputCount
            : standardInputCount;

        if (inputs.Count != requiredCount)
        {
            return TradeupValidationResult.Invalid(
                $"This tradeup requires exactly {requiredCount} inputs.");
        }

        if (!TryGetOutputRarity(
                inputRarity,
                out Rarity outputRarity))
        {
            return TradeupValidationResult.Invalid(
                $"There is no tradeup rarity above {inputRarity}.");
        }

        bool statTrakTradeup = firstItem.statTrak;

        HashSet<string> usedInstanceIds =
            new HashSet<string>();

        HashSet<CollectionData> usedSources =
            new HashSet<CollectionData>();

        for (int i = 0; i < inputs.Count; i++)
        {
            InventoryItem item = inputs[i];

            if (item == null || item.skin == null)
            {
                return TradeupValidationResult.Invalid(
                    $"Input slot {i + 1} contains an invalid item.");
            }

            if (string.IsNullOrWhiteSpace(item.instanceId))
            {
                return TradeupValidationResult.Invalid(
                    $"Input slot {i + 1} has no instance ID.");
            }

            if (!usedInstanceIds.Add(item.instanceId))
            {
                return TradeupValidationResult.Invalid(
                    "The same inventory item was selected more than once.");
            }

            if (InventoryManager.Instance != null &&
                InventoryManager.Instance.GetItemByInstanceId(
                    item.instanceId) == null)
            {
                return TradeupValidationResult.Invalid(
                    "One or more selected items are no longer owned.");
            }

            if (rejectFavoritedItems && item.favorite)
            {
                return TradeupValidationResult.Invalid(
                    "Favorited items cannot be used in tradeups.");
            }

            if (item.souvenir)
            {
                return TradeupValidationResult.Invalid(
                    "Souvenir items cannot be used in tradeups.");
            }

            if (item.isVanilla || item.skin.isVanilla)
            {
                return TradeupValidationResult.Invalid(
                    "Vanilla items cannot be used as tradeup inputs.");
            }

            if (item.skin.rarity != inputRarity)
            {
                return TradeupValidationResult.Invalid(
                    "All tradeup inputs must have the same rarity.");
            }

            if (item.statTrak != statTrakTradeup)
            {
                return TradeupValidationResult.Invalid(
                    "Normal and StatTrak items cannot be mixed.");
            }

            if (item.statTrak && !item.skin.canBeStatTrak)
            {
                return TradeupValidationResult.Invalid(
                    "A selected StatTrak item has invalid skin data.");
            }

            if (item.skin.collectionData == null)
            {
                return TradeupValidationResult.Invalid(
                    $"{SkinDisplayUtility.GetDisplayName(item.skin)} " +
                    "has no CollectionData source.");
            }

            usedSources.Add(item.skin.collectionData);
        }

        foreach (CollectionData source in usedSources)
        {
            List<WeightedSkinCandidate> candidates =
                GetOutputCandidates(
                    source,
                    outputRarity,
                    statTrakTradeup,
                    covertTradeup);

            if (candidates.Count == 0)
            {
                string sourceName = GetSourceName(source);

                if (covertTradeup)
                {
                    return TradeupValidationResult.Invalid(
                        $"{sourceName} has no configured Rare Special " +
                        "tradeup pool.");
                }

                return TradeupValidationResult.Invalid(
                    $"{sourceName} has no valid " +
                    $"{outputRarity} tradeup outputs.");
            }
        }

        return new TradeupValidationResult
        {
            isValid = true,
            errorMessage = "",
            requiredInputCount = requiredCount,
            inputRarity = inputRarity,
            outputRarity = outputRarity,
            isStatTrak = statTrakTradeup,
            isCovertTradeup = covertTradeup
        };
    }

    private bool TryGetOutputRarity(
        Rarity inputRarity,
        out Rarity outputRarity)
    {
        switch (inputRarity)
        {
            case Rarity.Consumer:
                outputRarity = Rarity.Industrial;
                return true;

            case Rarity.Industrial:
                outputRarity = Rarity.MilSpec;
                return true;

            case Rarity.MilSpec:
                outputRarity = Rarity.Restricted;
                return true;

            case Rarity.Restricted:
                outputRarity = Rarity.Classified;
                return true;

            case Rarity.Classified:
                outputRarity = Rarity.Covert;
                return true;

            case Rarity.Covert:
                outputRarity = Rarity.RareSpecial;
                return true;

            default:
                outputRarity = inputRarity;
                return false;
        }
    }

    #endregion

    #region Preview

    public TradeupPreview BuildPreview(
        IReadOnlyList<InventoryItem> inputs)
    {
        TradeupValidationResult validation =
            ValidateInputs(inputs);

        TradeupPreview preview = new TradeupPreview
        {
            validation = validation
        };

        if (!validation.isValid)
            return preview;

        preview.inputCount = inputs.Count;
        preview.inputRarity = validation.inputRarity;
        preview.outputRarity = validation.outputRarity;
        preview.isStatTrak = validation.isStatTrak;
        preview.isCovertTradeup =
            validation.isCovertTradeup;

        Dictionary<CollectionData, int> sourceInputCounts =
            new Dictionary<CollectionData, int>();

        double totalFloat = 0d;
        float totalInputValue = 0f;

        for (int i = 0; i < inputs.Count; i++)
        {
            InventoryItem item = inputs[i];

            totalFloat += item.floatValue;
            totalInputValue += GetItemValue(item);

            CollectionData source =
                item.skin.collectionData;

            if (!sourceInputCounts.ContainsKey(source))
                sourceInputCounts[source] = 0;

            sourceInputCounts[source]++;
        }

        preview.averageInputFloat =
            totalFloat / inputs.Count;

        preview.totalInputMarketValue =
            totalInputValue;

        foreach (KeyValuePair<CollectionData, int> pair
                 in sourceInputCounts)
        {
            CollectionData source = pair.Key;
            int sourceInputCount = pair.Value;

            float sourceProbability =
                sourceInputCount / (float)inputs.Count;

            preview.sourceChances.Add(
                new TradeupSourceChance
                {
                    source = source,
                    sourceApiId = GetSourceId(source),
                    sourceName = GetSourceName(source),
                    inputCount = sourceInputCount,
                    probability = sourceProbability
                });

            AddSourceOutcomesToPreview(
                preview,
                source,
                sourceProbability);
        }

        preview.sourceChances.Sort(
            (a, b) =>
                b.probability.CompareTo(a.probability));

        preview.possibleOutcomes.Sort(
            (a, b) =>
                b.probability.CompareTo(a.probability));

        return preview;
    }

    private void AddSourceOutcomesToPreview(
        TradeupPreview preview,
        CollectionData source,
        float sourceProbability)
    {
        List<WeightedSkinCandidate> candidates =
            GetOutputCandidates(
                source,
                preview.outputRarity,
                preview.isStatTrak,
                preview.isCovertTradeup);

        if (candidates.Count == 0)
            return;

        float totalWeight = 0f;

        for (int i = 0; i < candidates.Count; i++)
        {
            totalWeight += Mathf.Max(
                0f,
                candidates[i].weight);
        }

        bool useEqualWeights = totalWeight <= 0f;

        if (useEqualWeights)
            totalWeight = candidates.Count;

        for (int i = 0; i < candidates.Count; i++)
        {
            WeightedSkinCandidate candidate =
                candidates[i];

            if (candidate == null ||
                candidate.skin == null)
            {
                continue;
            }

            float candidateWeight = useEqualWeights
                ? 1f
                : Mathf.Max(0f, candidate.weight);

            float withinSourceProbability =
                candidateWeight / totalWeight;

            preview.possibleOutcomes.Add(
                new TradeupOutcomeChance
                {
                    source = source,
                    sourceApiId = GetSourceId(source),
                    sourceName = GetSourceName(source),
                    skin = candidate.skin,
                    probability =
                        sourceProbability *
                        withinSourceProbability
                });
        }
    }

    #endregion

    #region Execution

    public TradeupExecutionResult ExecuteTradeup(
        List<InventoryItem> inputs)
    {
        TradeupPreview preview =
            BuildPreview(inputs);

        if (!preview.validation.isValid)
        {
            return TradeupExecutionResult.Failed(
                preview.validation.errorMessage,
                preview);
        }

        if (InventoryManager.Instance == null)
        {
            return TradeupExecutionResult.Failed(
                "InventoryManager is missing.",
                preview);
        }

        if (SaveManager.Instance == null)
        {
            return TradeupExecutionResult.Failed(
                "SaveManager is missing.",
                preview);
        }

        TradeupOutcomeChance rolledOutcome =
            RollOutcome(preview);

        if (rolledOutcome == null ||
            rolledOutcome.skin == null)
        {
            return TradeupExecutionResult.Failed(
                "The tradeup output roll failed.",
                preview);
        }

        // Confirm all inputs still exist immediately before mutation.
        for (int i = 0; i < inputs.Count; i++)
        {
            InventoryItem ownedItem =
                InventoryManager.Instance.GetItemByInstanceId(
                    inputs[i].instanceId);

            if (ownedItem == null)
            {
                return TradeupExecutionResult.Failed(
                    "One or more inputs are no longer in the inventory.",
                    preview);
            }

            if (ownedItem.favorite &&
                rejectFavoritedItems)
            {
                return TradeupExecutionResult.Failed(
                    "One or more inputs became favorited.",
                    preview);
            }
        }

        int destinationStorage =
            DetermineOutputStorage(inputs);

        InventoryItem outputItem =
            CreateOutputItem(
                rolledOutcome.skin,
                preview,
                destinationStorage);

        if (outputItem == null)
        {
            return TradeupExecutionResult.Failed(
                "Could not create the tradeup output.",
                preview);
        }

        HashSet<string> consumedIds =
            new HashSet<string>();

        for (int i = 0; i < inputs.Count; i++)
            consumedIds.Add(inputs[i].instanceId);

        int removedCount =
            InventoryManager.Instance.RemoveItemsByInstanceIds(
                consumedIds);

        if (removedCount != consumedIds.Count)
        {
            RestoreMissingInputs(inputs);

            return TradeupExecutionResult.Failed(
                "The tradeup transaction could not remove every input. " +
                "Removed items were restored.",
                preview);
        }

        InventoryManager.Instance.AddItem(outputItem);

        InventoryItem addedOutput =
            InventoryManager.Instance.GetItemByInstanceId(
                outputItem.instanceId);

        if (addedOutput == null)
        {
            RestoreMissingInputs(inputs);

            return TradeupExecutionResult.Failed(
                "The output could not be added. Inputs were restored.",
                preview);
        }

        TradeupHistorySaveData history =
            BuildHistoryRecord(
                inputs,
                preview,
                rolledOutcome,
                addedOutput);

        SaveManager.Instance.RecordCompletedTradeup(
            history);

        if (logCompletedTradeups)
        {
            Debug.Log(
                $"Tradeup completed: {inputs.Count}x " +
                $"{preview.inputRarity} → " +
                $"{SkinDisplayUtility.GetDisplayName(addedOutput.skin)}. " +
                $"Float: {addedOutput.floatValue:0.0000000000}. " +
                $"Value: {addedOutput.marketValue:0.##}");
        }

        return new TradeupExecutionResult
        {
            success = true,
            errorMessage = "",
            preview = preview,
            rolledOutcome = rolledOutcome,
            outputItem = addedOutput,
            historyRecord = history
        };
    }

    private TradeupOutcomeChance RollOutcome(
        TradeupPreview preview)
    {
        if (preview == null ||
            preview.possibleOutcomes == null ||
            preview.possibleOutcomes.Count == 0)
        {
            return null;
        }

        float totalProbability = 0f;

        for (int i = 0;
             i < preview.possibleOutcomes.Count;
             i++)
        {
            totalProbability += Mathf.Max(
                0f,
                preview.possibleOutcomes[i].probability);
        }

        if (totalProbability <= 0f)
            return null;

        float roll =
            UnityEngine.Random.Range(
                0f,
                totalProbability);

        float current = 0f;

        for (int i = 0;
             i < preview.possibleOutcomes.Count;
             i++)
        {
            TradeupOutcomeChance outcome =
                preview.possibleOutcomes[i];

            current += Mathf.Max(
                0f,
                outcome.probability);

            if (roll <= current)
                return outcome;
        }

        return preview.possibleOutcomes[
            preview.possibleOutcomes.Count - 1];
    }

    private InventoryItem CreateOutputItem(
        SkinData outputSkin,
        TradeupPreview preview,
        int storageIndex)
    {
        if (outputSkin == null ||
            preview == null)
        {
            return null;
        }

        InventoryItem output = new InventoryItem
        {
            instanceId = Guid.NewGuid().ToString(),
            skin = outputSkin,
            favorite = false,
            souvenir = false,
            statTrak =
                preview.isStatTrak &&
                outputSkin.canBeStatTrak,
            storageIndex = storageIndex,
            isVanilla = outputSkin.isVanilla
        };

        if (outputSkin.isVanilla)
        {
            output.floatValue = -1d;
            output.patternId = -1;
            output.patternTier = PatternTier.None;
        }
        else
        {
            double normalizedAverage =
                Clamp01(preview.averageInputFloat);

            double minimumFloat =
                outputSkin.minFloat;

            double maximumFloat =
                outputSkin.maxFloat;

            if (maximumFloat < minimumFloat)
            {
                double temporary = minimumFloat;
                minimumFloat = maximumFloat;
                maximumFloat = temporary;
            }

            output.floatValue =
                minimumFloat +
                normalizedAverage *
                (maximumFloat - minimumFloat);

            output.floatValue = Clamp(
                output.floatValue,
                minimumFloat,
                maximumFloat);

            output.patternId =
                UnityEngine.Random.Range(0, 1001);

            output.patternTier =
                PatternResolver.ResolveTier(
                    outputSkin,
                    output.patternId);
        }

        output.marketValue =
            PriceCalculator.GetPrice(output);

        return output;
    }

    private int DetermineOutputStorage(
        IReadOnlyList<InventoryItem> inputs)
    {
        Dictionary<int, int> storageCounts =
            new Dictionary<int, int>();

        for (int i = 0; i < inputs.Count; i++)
        {
            int storageIndex =
                Mathf.Max(0, inputs[i].storageIndex);

            if (!storageCounts.ContainsKey(storageIndex))
                storageCounts[storageIndex] = 0;

            storageCounts[storageIndex]++;
        }

        int highestCount = -1;
        List<int> tiedStorages = new List<int>();

        foreach (KeyValuePair<int, int> pair
                 in storageCounts)
        {
            if (pair.Value > highestCount)
            {
                highestCount = pair.Value;
                tiedStorages.Clear();
                tiedStorages.Add(pair.Key);
            }
            else if (pair.Value == highestCount)
            {
                tiedStorages.Add(pair.Key);
            }
        }

        if (tiedStorages.Count == 0)
            return InventoryManager.Instance.ActiveStorageIndex;

        int activeStorage =
            InventoryManager.Instance.ActiveStorageIndex;

        if (tiedStorages.Contains(activeStorage))
            return activeStorage;

        return tiedStorages[0];
    }

    private void RestoreMissingInputs(
        IReadOnlyList<InventoryItem> inputs)
    {
        if (InventoryManager.Instance == null ||
            inputs == null)
        {
            return;
        }

        for (int i = 0; i < inputs.Count; i++)
        {
            InventoryItem input = inputs[i];

            if (input == null ||
                string.IsNullOrWhiteSpace(input.instanceId))
            {
                continue;
            }

            if (InventoryManager.Instance.GetItemByInstanceId(
                    input.instanceId) != null)
            {
                continue;
            }

            InventoryManager.Instance.AddItem(input);
        }
    }

    #endregion

    #region Output Pools

    private List<WeightedSkinCandidate> GetOutputCandidates(
        CollectionData source,
        Rarity outputRarity,
        bool statTrak,
        bool covertTradeup)
    {
        if (covertTradeup)
        {
            return GetCovertOutputCandidates(
                source,
                statTrak);
        }

        return GetStandardOutputCandidates(
            source,
            outputRarity,
            statTrak);
    }

    private List<WeightedSkinCandidate>
        GetStandardOutputCandidates(
            CollectionData source,
            Rarity outputRarity,
            bool statTrak)
    {
        List<WeightedSkinCandidate> candidates =
            new List<WeightedSkinCandidate>();

        GameDatabase activeDatabase =
            ActiveDatabase;

        if (activeDatabase == null ||
            activeDatabase.allSkins == null ||
            source == null)
        {
            return candidates;
        }

        HashSet<SkinData> addedSkins =
            new HashSet<SkinData>();

        for (int i = 0;
             i < activeDatabase.allSkins.Count;
             i++)
        {
            SkinData skin =
                activeDatabase.allSkins[i];

            if (skin == null)
                continue;

            if (!SameCollection(
                    skin.collectionData,
                    source))
            {
                continue;
            }

            if (skin.rarity != outputRarity)
                continue;

            if (skin.isVanilla)
                continue;

            if (statTrak && !skin.canBeStatTrak)
                continue;

            if (!addedSkins.Add(skin))
                continue;

            candidates.Add(
                new WeightedSkinCandidate
                {
                    skin = skin,
                    weight = 1f
                });
        }

        return candidates;
    }

    private List<WeightedSkinCandidate>
        GetCovertOutputCandidates(
            CollectionData source,
            bool statTrak)
    {
        List<WeightedSkinCandidate> candidates =
            new List<WeightedSkinCandidate>();

        CaseData rareSpecialCase =
            GetRareSpecialCaseForSource(source);

        if (rareSpecialCase == null ||
            rareSpecialCase.dropPool == null)
        {
            return candidates;
        }

        Dictionary<SkinData, float> weightBySkin =
            new Dictionary<SkinData, float>();

        for (int i = 0;
             i < rareSpecialCase.dropPool.Count;
             i++)
        {
            WeightedDrop drop =
                rareSpecialCase.dropPool[i];

            if (drop == null ||
                drop.skin == null)
            {
                continue;
            }

            SkinData skin = drop.skin;

            if (skin.rarity != Rarity.RareSpecial)
                continue;

            if (statTrak && !skin.canBeStatTrak)
                continue;

            if (!weightBySkin.ContainsKey(skin))
                weightBySkin[skin] = 0f;

            weightBySkin[skin] +=
                Mathf.Max(0f, drop.weight);
        }

        foreach (KeyValuePair<SkinData, float> pair
                 in weightBySkin)
        {
            candidates.Add(
                new WeightedSkinCandidate
                {
                    skin = pair.Key,
                    weight = pair.Value
                });
        }

        return candidates;
    }

    private CaseData GetRareSpecialCaseForSource(
        CollectionData source)
    {
        if (source == null ||
            covertTradeupPools == null)
        {
            return null;
        }

        for (int i = 0;
             i < covertTradeupPools.Count;
             i++)
        {
            CovertTradeupPoolMapping mapping =
                covertTradeupPools[i];

            if (mapping == null ||
                mapping.sourceCollection == null ||
                mapping.rareSpecialCase == null)
            {
                continue;
            }

            if (SameCollection(
                    mapping.sourceCollection,
                    source))
            {
                return mapping.rareSpecialCase;
            }
        }

        return null;
    }

    private bool SameCollection(
        CollectionData first,
        CollectionData second)
    {
        if (first == second)
            return true;

        if (first == null || second == null)
            return false;

        if (!string.IsNullOrWhiteSpace(first.apiId) &&
            !string.IsNullOrWhiteSpace(second.apiId))
        {
            return string.Equals(
                first.apiId,
                second.apiId,
                StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(
            first.collectionName,
            second.collectionName,
            StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Save History

    private TradeupHistorySaveData BuildHistoryRecord(
        IReadOnlyList<InventoryItem> inputs,
        TradeupPreview preview,
        TradeupOutcomeChance outcome,
        InventoryItem output)
    {
        TradeupHistorySaveData history =
            new TradeupHistorySaveData
            {
                tradeupId = Guid.NewGuid().ToString(),
                completedUtcTicks = DateTime.UtcNow.Ticks,

                inputRarity = preview.inputRarity,
                outputRarity = preview.outputRarity,
                inputCount = inputs.Count,

                statTrak = preview.isStatTrak,
                covertToRareSpecial =
                    preview.isCovertTradeup,

                averageInputFloat =
                    preview.averageInputFloat,

                totalInputMarketValue =
                    preview.totalInputMarketValue,

                outputSkinApiId =
                    output.skin != null
                        ? output.skin.apiId
                        : "",

                outputInstanceId =
                    output.instanceId,

                outputFloat =
                    output.isVanilla
                        ? -1d
                        : output.floatValue,

                outputPatternId =
                    output.isVanilla
                        ? -1
                        : output.patternId,

                outputPatternTier =
                    output.isVanilla
                        ? PatternTier.None
                        : output.patternTier,

                outputMarketValue =
                    output.marketValue
            };

        for (int i = 0; i < inputs.Count; i++)
        {
            InventoryItem input = inputs[i];

            history.inputInstanceIds.Add(
                input.instanceId ?? "");

            history.inputSkinApiIds.Add(
                input.skin != null
                    ? input.skin.apiId ?? ""
                    : "");

            history.inputSourceApiIds.Add(
                input.skin != null
                    ? GetSourceId(
                        input.skin.collectionData)
                    : "");
        }

        return history;
    }

    #endregion

    #region Helpers

    private float GetItemValue(
        InventoryItem item)
    {
        if (item == null || item.skin == null)
            return 0f;

        if (item.marketValue <= 0f)
        {
            item.marketValue =
                PriceCalculator.GetPrice(item);
        }

        return item.marketValue;
    }

    private string GetSourceId(
        CollectionData source)
    {
        if (source == null)
            return "";

        if (!string.IsNullOrWhiteSpace(source.apiId))
            return source.apiId;

        return source.collectionName ?? "";
    }

    private string GetSourceName(
        CollectionData source)
    {
        if (source == null)
            return "Unknown Source";

        if (!string.IsNullOrWhiteSpace(
                source.collectionName))
        {
            return source.collectionName;
        }

        return !string.IsNullOrWhiteSpace(source.apiId)
            ? source.apiId
            : "Unknown Source";
    }

    private static double Clamp01(
        double value)
    {
        return Clamp(value, 0d, 1d);
    }

    private static double Clamp(
        double value,
        double minimum,
        double maximum)
    {
        if (value < minimum)
            return minimum;

        if (value > maximum)
            return maximum;

        return value;
    }

    #endregion
}