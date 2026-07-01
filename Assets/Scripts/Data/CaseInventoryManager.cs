using System;
using System.Collections.Generic;
using UnityEngine;

public class CaseInventoryManager : MonoBehaviour
{
    public static CaseInventoryManager Instance { get; private set; }

    public event Action OnCaseInventoryChanged;

    [SerializeField]
    private List<CaseInventoryEntry> cases = new List<CaseInventoryEntry>();

    public IReadOnlyList<CaseInventoryEntry> Cases => cases;

    public int TotalCaseCount
    {
        get
        {
            int total = 0;

            foreach (CaseInventoryEntry entry in cases)
            {
                if (entry == null)
                    continue;

                total += entry.amount;
            }

            return total;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int GetAmount(CaseData caseData)
    {
        CaseInventoryEntry entry = GetEntry(caseData);

        if (entry == null)
            return 0;

        return entry.amount;
    }

    public bool HasCases(CaseData caseData, int amount)
    {
        if (caseData == null)
            return false;

        if (amount <= 0)
            return true;

        return GetAmount(caseData) >= amount;
    }

    public void AddCases(CaseData caseData, int amount)
    {
        if (caseData == null)
        {
            Debug.LogWarning("CaseInventoryManager: Tried to add null case.");
            return;
        }

        if (amount <= 0)
            return;

        CaseInventoryEntry entry = GetEntry(caseData);

        if (entry == null)
        {
            entry = new CaseInventoryEntry
            {
                caseData = caseData,
                amount = 0
            };

            cases.Add(entry);
        }

        entry.amount += amount;

        OnCaseInventoryChanged?.Invoke();

        Debug.Log($"Added {amount}x {caseData.caseName}. Owned: {entry.amount}");
    }

    public bool RemoveCases(CaseData caseData, int amount)
    {
        if (caseData == null)
            return false;

        if (amount <= 0)
            return true;

        CaseInventoryEntry entry = GetEntry(caseData);

        if (entry == null)
            return false;

        if (entry.amount < amount)
            return false;

        entry.amount -= amount;

        if (entry.amount <= 0)
            cases.Remove(entry);

        OnCaseInventoryChanged?.Invoke();

        return true;
    }

    public CaseInventoryEntry GetEntry(CaseData caseData)
    {
        if (caseData == null)
            return null;

        foreach (CaseInventoryEntry entry in cases)
        {
            if (entry == null || entry.caseData == null)
                continue;

            if (entry.caseData == caseData)
                return entry;

            if (!string.IsNullOrWhiteSpace(caseData.apiId) &&
                entry.caseData.apiId == caseData.apiId)
                return entry;
        }

        return null;
    }

    public void ReplaceCaseInventory(List<CaseInventoryEntry> loadedCases)
    {
        cases.Clear();

        if (loadedCases != null)
        {
            foreach (CaseInventoryEntry entry in loadedCases)
            {
                if (entry == null || entry.caseData == null)
                    continue;

                if (entry.amount <= 0)
                    continue;

                cases.Add(entry);
            }
        }

        OnCaseInventoryChanged?.Invoke();

        Debug.Log($"Case inventory loaded. Total cases: {TotalCaseCount}");
    }

    public void ClearCaseInventory()
    {
        cases.Clear();
        OnCaseInventoryChanged?.Invoke();
    }
}