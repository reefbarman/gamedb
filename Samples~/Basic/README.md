# Basic GameDB sample

This sample provides a dependency-free database with:

- a `Categories` table;
- an `Items` table;
- string, Int32, exact signed Int64, finite Double, string-array, and table-reference fields;
- two example items.

## Open and edit

1. Open **Window → GameDB → Open Editor**.
2. Click **Add Existing GameDB**.
3. Select `Assets/Samples/GameDB/<version>/Basic/Resources/GameDBs/basic.json`.
4. Edit and save the database normally.

The exact sample path includes the installed package version chosen by Unity.

## Generate runtime classes

1. With the sample database loaded, click **Generate Classes**.
2. Choose a folder under `Assets`, for example `Assets/Generated/GameDB`.
3. Wait for Unity to compile the generated scripts.

The scope is `Basic`, so generated types use the `GameDBBasic` namespace.

## Load the sample

Attach this pattern to a MonoBehaviour after generating the classes:

```csharp
using GameDBBasic;
using UnityEngine;

public sealed class BasicGameDBExample : MonoBehaviour
{
    private void Start()
    {
        var gameDB = new GameDB("Basic Sample");
        var error = gameDB.Load("GameDBs/basic");
        if (error != null)
        {
            Debug.LogException(error);
            return;
        }

        var sword = gameDB.ItemsTable.GetByKey(ItemsSchema.KeySword);
        var category = sword.CategoryVal;

        Debug.Log($"{sword.DisplayNameVal} deals {sword.DamageVal} damage.");
        Debug.Log($"Stable ID: {sword.StableIdVal}");
        Debug.Log($"Precision scale: {sword.PrecisionScaleVal:G17}");
        Debug.Log($"Category: {category.DisplayNameVal}");
    }
}
```

`StableId` is encoded as a JSON integer token and demonstrates exact Int64 values outside JavaScript's safe range `[-9007199254740991, 9007199254740991]`; JavaScript consumers need lossless integer parsing for those values. `PrecisionScale` demonstrates finite Double precision and range beyond Single, and `G17` displays canonical round-trip text. Negative Double zero is normalized to zero. Generated code is intentionally not included in the package sample. Generate it into your project so it matches any schema edits you make.
