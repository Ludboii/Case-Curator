using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CaseShopUI : MonoBehaviour
{
    [Header("Database")]
    public GameDatabase database;

    [Header("Card Spawning")]
    public Transform caseGridContent;
    public CaseShopCardUI caseCardPrefab;
    public CaseShopRankDividerUI rankDividerPrefab;

    [Header("Rank Divider Layout")]
    public int fallbackCardsPerRow = 3;
    public Vector2 fallbackCardCellSize = new Vector2(500f, 160f);
    public Vector2 fallbackCardSpacing = new Vector