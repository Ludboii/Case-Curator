# Case Curator — Codex Instructions

## Project

Unity 2D mobile CS2-style case opener and collection game.

Primary gameplay loop:
- Buy openable containers.
- Store them in Case Inventory.
- Open containers.
- Collect, inspect, sell, trade up, or donate skins.
- Complete containers and Museum collections.

## Architecture rules

- CaseData represents every openable container.
- CollectionData is source/grouping data and is not automatically openable.
- Case Inventory and Skin Inventory are separate.
- Do not create duplicate openables unless the balance sheet explicitly contains both.
- Preserve compatibility with SaveManager and existing saves.
- Historical collection progress must not depend on currently owning the item.

## Coding rules

- Use C# compatible with the Unity version in ProjectSettings.
- Do not add third-party packages without explicit approval.
- Do not rename serialized public fields without migration support.
- Do not modify scenes, prefabs, or ScriptableObject assets unless explicitly requested.
- Never replace a large working script merely to make a small change.
- Prefer targeted edits over full rewrites.
- Preserve existing public methods used by other scripts.
- Check all callers before changing a public method signature.
- Avoid repeated Instantiate/Destroy operations in frequently refreshed UI.
- Avoid SaveGame or PlayerPrefs.Save inside item-by-item loops.
- Batch inventory changes and UI refresh events.
- Add null checks for Inspector-assigned references.
- Do not use FindObjectOfType repeatedly during gameplay.

## Required workflow

Before editing:
1. Inspect all directly related scripts.
2. Search for every caller of methods being changed.
3. Explain the likely root cause.
4. List the files that will be modified.

After editing:
1. Summarize exactly what changed.
2. List Inspector setup required.
3. List possible save migration effects.
4. Report tests performed.
5. State anything that could not be tested outside Unity.

## Current terminology

Use:
- Case Curator
- Bronze Completion
- Silver Completion
- Gold Completion
- Diamond Completion
- Rare Special Vault
- The Museum
- Tradeups

Do not introduce:
- Case Catcher
- Normal Completion
- Rare Vault

## Completion rules

Bronze:
- Every normal skin opened once.
- Any one Rare Special item if the container has knives/gloves.

Silver:
- Every normal skin in its best possible wear.
- Rare Special not required.

Gold:
- Every normal skin as StatTrak, or Souvenir for souvenir containers.
- Rare Special not required.

Diamond:
- Every normal skin in its best possible wear and correct variant.
- Rare Special not required.

## Safety

- Do not delete save files, scenes, prefabs, or generated data without explicit approval.
- Do not run destructive Git commands.
- Do not commit directly unless requested.
- Keep changes reviewable and scoped.