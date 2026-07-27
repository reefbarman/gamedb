# Optional Addressables integration

GameDB can load GUID-backed Unity-object values and GameDB JSON `TextAsset` data through Unity Addressables without making Addressables a dependency of the GameDB package. The integration is compiled only when a supported Addressables package is installed.

Referenced assets and database JSON intentionally use different ownership contracts: asset loads return a retained lease, while database loads copy text and release their temporary handle before the database imports it.

## Install

Install GameDB and Addressables as independent Unity Package Manager packages:

1. Install GameDB from its Git URL.
2. Install `com.unity.addressables` `2.9.1` or a compatible release below `4.0.0` only in projects that need Addressables loading.

GameDB's `package.json` intentionally does not declare Addressables. When Addressables is absent, the optional runtime and test assemblies are omitted from compilation and the core package remains usable.

If your gameplay code is in an assembly definition, add explicit references to:

- the generated assembly containing your GameDB row types;
- `GameDBLibrary.Addressables`.

The optional assembly is auto-referenced for code in Unity's predefined assemblies.

## Configure assets

A GameDB `unityObject` value stores the asset GUID and current project path. The Addressables adapter always uses the GUID as its key; it does not use the visible Addressables address or silently fall back to the path.

For every Addressables group containing assets loaded through GameDB:

1. Open **Window → Asset Management → Addressables → Groups**.
2. Select the group's **Content Packing & Loading** schema.
3. Keep **Include GUIDs in Catalog** enabled.
4. Build Addressables content for the target player before testing or shipping.

Assets loaded through Addressables must be outside `Resources`; Unity moves an asset out of `Resources` when it becomes Addressable. The next real GameDB save resolves the same GUID and transactionally refreshes its persisted path.

Changing an asset's visible Addressables address does not affect GameDB loading while its GUID remains in the catalog.

## Load database JSON

Generated Unity databases accept any `IGameDBDataLoader`. Use the optional singleton with an explicit Addressables key:

```csharp
using GameDBLibraryAddressables;

await db.LoadAsync(
    "main-database",
    AddressablesGameDBDataLoader.Instance,
    cancellationToken: destroyCancellationToken);
```

For this loader, `location` is the key passed directly to `Addressables.LoadAssetAsync<TextAsset>`. It may be a configured visible address or GUID supplied by the caller; GameDB does not reinterpret a Resources path or fall back between address, GUID, path, and label.

The loader copies `TextAsset.text` while its handle is valid, releases that handle exactly once, then returns the JSON to GameDB's atomic importer. The caller receives no lease because database rows do not retain the `TextAsset` or its dependencies. Transport failures are wrapped by generated `LoadAsync` in `GameDBDataLoadException`; JSON/import failures retain their concrete exception type.

Cancellation before import preserves the previously committed database. If an Addressables operation was acquired, GameDB releases its valid owned handle exactly once; early release does not guarantee that the underlying provider I/O is physically aborted.

## Load and release referenced assets

The generated `<Field>Val` property remains a `GameDBLibrary.UnityObjectReference`. Import the optional namespace and load the requested Unity type:

```csharp
using GameDBLibraryAddressables;
using UnityEngine;

public sealed class ItemIcon : MonoBehaviour
{
    [SerializeField]
    private UnityEngine.UI.RawImage m_image;

    private AddressableAssetLease<Texture2D> m_iconLease;

    public async Awaitable SetItemAsync(GameDBMain.Items item,
        System.Threading.CancellationToken cancellationToken)
    {
        var nextLease = await item.IconVal.LoadAddressableAsync<Texture2D>(
            cancellationToken);
        var previousLease = m_iconLease;
        m_iconLease = nextLease;
        m_image.texture = nextLease.Asset;
        previousLease?.Dispose();
    }

    private void OnDestroy()
    {
        m_iconLease?.Dispose();
    }
}
```

Each successful call returns a new `AddressableAssetLease<T>` that owns exactly one Addressables load reference. Keep the lease alive for at least as long as any code or instantiated object depends on the loaded asset or its dependencies. When replacing a loaded asset, acquire the next lease before swapping it into use and disposing the previous owner, as shown above. Dispose the current lease when its lifetime ends.

- `Dispose()` is idempotent and releases the owned handle once.
- Accessing `Asset` after disposal throws `ObjectDisposedException`.
- An empty GameDB reference returns a lease whose `Asset` is `null` and which owns no handle.
- GameDB does not cache Addressables loads. Repeated calls return independent leases.
- A GameDB data reload does not invalidate an existing lease. New loads use the reference on the newly acquired row.
- The returned Unity `Awaitable` is single-await, matching Unity's pooled-awaitable contract.

For array fields, load each `UnityObjectReference` from `<Field>Val` as needed. For dictionary values, call the existing Unity-object accessor's `GetValue()` and then call `LoadAddressableAsync<T>()` on that reference.

## Resources or Addressables

For database JSON, use the generated default `LoadAsync(path)` for Resources data or pass `AddressablesGameDBDataLoader.Instance` with an explicit Addressables key.

Use one loading transport for each referenced asset:

- Keep an asset beneath exactly one case-sensitive `Resources` directory when synchronous generated `<Field>ObjectVal` or `GetObject()` access is appropriate.
- Put the asset outside `Resources`, mark it Addressable, and call `LoadAddressableAsync<T>()` when asynchronous delivery and explicit lifetime ownership are required.

A valid non-Resources reference causes the synchronous `ObjectVal`/`GetObject()` path to throw an actionable `InvalidOperationException`; it does not return `null` or guess another transport.

## Failures and cancellation

Both Addressables loaders switch to Unity's main thread and poll through Unity `Awaitable` frame waits. They do not use `AsyncOperationHandle.Task`, so the adapter does not depend on an API unavailable on WebGL.

Cancellation throws `OperationCanceledException`. If an Addressables operation was acquired, GameDB releases its valid owned handle exactly once; early release does not guarantee that provider I/O is physically aborted.

For database JSON, generated `LoadAsync` wraps non-cancellation acquisition failures in `GameDBDataLoadException`. For `LoadAddressableAsync<T>` referenced-asset loads, other failures throw `AddressableAssetLoadException`, which exposes:

- `AssetGuid`;
- `AssetPath`;
- `RequestedType`;
- the Addressables operation failure as `InnerException` when available.

For a player-only failure, check all of the following:

- the persisted GUID still identifies the intended main asset;
- the asset is Addressable;
- **Include GUIDs in Catalog** is enabled for its group;
- Addressables content was built for the active target;
- the requested generic type matches the asset;
- the built catalog and bundles are available to the player.

GameDB never falls back from GUID to visible address or project path, because that would make editor and player identity behavior diverge.
