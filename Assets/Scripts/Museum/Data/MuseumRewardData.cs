using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MuseumContainerReward
{
    public CaseData container;

    [Min(1)]
    public int amount = 1;
}

/// <summary>
/// Reusable reward bundle for Museum Staircase milestones and future Museum
/// activities. Applying the reward remains the responsibility of a gameplay
/// service; UI scripts should only display this asset.
/// </summary>
[CreateAssetMenu(
    fileName = "MuseumRewardData",
    menuName = "Case Curator/Museum/Museum Reward")]
public class MuseumRewardData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable reward ID used by logs and future validation tools.")]
    public string rewardId;

    public string displayName;

    [TextArea(1, 4)]
    public string description;

    public Sprite icon;

    [Header("Currency and Progress")]
    [Min(0f)] public float gold;
    [Min(0)] public int diamonds;
    [Min(0)] public int xp;

    [Header("Containers / Presents")]
    public List<MuseumContainerReward> containerRewards =
        new List<MuseumContainerReward>();

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : name;

    public bool HasAnyReward
    {
        get
        {
            if (gold > 0f || diamonds > 0 || xp > 0)
                return true;

            if (containerRewards == null)
                return false;

            for (int i = 0; i < containerRewards.Count; i++)
            {
                MuseumContainerReward reward = containerRewards[i];

                if (reward != null &&
                    reward.container != null &&
                    reward.amount > 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void OnValidate()
    {
        rewardId = rewardId != null ? rewardId.Trim() : "";
        displayName = displayName != null ? displayName.Trim() : "";
        gold = Mathf.Max(0f, gold);
        diamonds = Mathf.Max(0, diamonds);
        xp = Mathf.Max(0, xp);

        if (containerRewards == null)
            containerRewards = new List<MuseumContainerReward>();

        for (int i = 0; i < containerRewards.Count; i++)
        {
            MuseumContainerReward reward = containerRewards[i];

            if (reward != null)
                reward.amount = Mathf.Max(1, reward.amount);
        }
    }
}
