using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MuseumPresentTierCardUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text tierText;
    [SerializeField] private TMP_Text fragmentsText;
    [SerializeField] private TMP_Text presentsText;
    [SerializeField] private TMP_Text openedText;
    [SerializeField] private TMP_Text rewardRangeText;
    [SerializeField] private Button assembleButton;
    [SerializeField] private Button openButton;

    private MuseumPresentTier tier;
    private MuseumPresentDeskUI owner;

    private void Awake()
    {
        ResolveReferences();
    }

    public void Setup(
        MuseumPresentTier presentTier,
        MuseumPresentDeskUI desk)
    {
        ResolveReferences();
        tier = presentTier;
        owner = desk;

        if (assembleButton != null)
        {
            assembleButton.onClick.RemoveListener(HandleAssemble);
            assembleButton.onClick.AddListener(HandleAssemble);
        }

        if (openButton != null)
        {
            openButton.onClick.RemoveListener(HandleOpen);
            openButton.onClick.AddListener(HandleOpen);
        }

        Refresh();
    }

    public void Refresh()
    {
        MuseumPresentService service = MuseumPresentService.Instance;

        if (service == null)
            return;

        MuseumPresentTierConfig config = service.GetTierConfig(tier);
        int fragments = service.GetFragments(tier);
        int presents = service.GetPresents(tier);
        int opened = service.GetPresentsOpened(tier);
        int cost = service.GetFragmentsPerPresent(tier);

        if (iconImage != null)
        {
            iconImage.sprite = config.icon;
            iconImage.enabled = config.icon != null;
            iconImage.preserveAspect = true;
        }

        if (tierText != null)
            tierText.text = config.DisplayName;

        if (fragmentsText != null)
            fragmentsText.text = $"Fragments: {fragments:N0} / {cost:N0}";

        if (presentsText != null)
            presentsText.text = $"Presents: {presents:N0}";

        if (openedText != null)
            openedText.text = $"Opened: {opened:N0}";

        if (rewardRangeText != null)
        {
            IReadOnlyList<MuseumPresentContainerDrop> pool =
                MuseumPresentOpeningService.GetOrCreate()
                    .GetResolvedPool(tier);
            int validDrops = CountValidDrops(pool);

            rewardRangeText.text =
                $"Container drop: 1 from {validDrops:N0} possible\n" +
                $"Gold {config.minimumGold:0.##}–{config.maximumGold:0.##}\n" +
                $"XP {config.minimumXP:N0}–{config.maximumXP:N0}\n" +
                $"Diamonds {config.minimumDiamonds:N0}–{config.maximumDiamonds:N0}";
        }

        if (assembleButton != null)
            assembleButton.interactable = fragments >= cost;

        if (openButton != null)
        {
            IReadOnlyList<MuseumPresentContainerDrop> pool =
                MuseumPresentOpeningService.GetOrCreate()
                    .GetResolvedPool(tier);

            openButton.interactable =
                presents > 0 && CountValidDrops(pool) > 0;
        }
    }

    private static int CountValidDrops(
        IReadOnlyList<MuseumPresentContainerDrop> pool)
    {
        int count = 0;

        if (pool == null)
            return count;

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] != null && pool[i].IsValid)
                count++;
        }

        return count;
    }

    private void HandleAssemble()
    {
        if (owner != null)
            owner.Assemble(tier);
    }

    private void HandleOpen()
    {
        if (owner != null)
            owner.OpenPresent(tier);
    }

    private void ResolveReferences()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null)
                continue;

            string objectName = text.gameObject.name.ToLowerInvariant();

            if (tierText == null && objectName.Contains("tier"))
                tierText = text;
            else if (fragmentsText == null && objectName.Contains("fragment"))
                fragmentsText = text;
            else if (presentsText == null && objectName.Contains("present"))
                presentsText = text;
            else if (openedText == null && objectName.Contains("opened"))
                openedText = text;
            else if (rewardRangeText == null && objectName.Contains("reward"))
                rewardRangeText = text;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            string objectName = button.gameObject.name.ToLowerInvariant();

            if (assembleButton == null && objectName.Contains("assemble"))
                assembleButton = button;
            else if (openButton == null && objectName.Contains("open"))
                openButton = button;
        }
    }

    private void OnDestroy()
    {
        if (assembleButton != null)
            assembleButton.onClick.RemoveListener(HandleAssemble);

        if (openButton != null)
            openButton.onClick.RemoveListener(HandleOpen);
    }
}
