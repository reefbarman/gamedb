# Runtime use

GameDB generates a strongly typed JSON runtime API for Unity 6.5. Generate classes with **Generate for Unity** enabled, keep generated files under `Assets`, and regenerate them after any schema, scope, table, field, key, enum, or localization-mode change.

## Generated code

For scope `Main`, generated types use the `GameDBMain` namespace:

- `GameDB` derives from `GameDBLibrary.GameDBBase`.
- Each table `Items` produces row class `Items`, table class `ItemsTable`, and static schema class `ItemsSchema`.
- `GameDB.ItemsTable` exposes the generated table.
- Generated `GameDB`, row, and table classes are `partial`, so game code can extend them in separate files without editing generated output.
- `ItemsSchema.TableName`, `ItemsSchema.Field<FieldName>`, and string-key `ItemsSchema.Key<RowKey>` members are `const string`; enum-key members are `static readonly` values of the configured enum type.

Generated files are derived output and must not be edited by hand. Generation validates scope, table, field, row-key, member, type, and case-insensitive filename collisions before writing. It stages a complete scope and replaces the existing scope directory, preserving `.cs.meta` files for unchanged generated filenames while deleting stale files for removed tables.

## Construct and load a database

The generated constructor is:

```csharp
var db = new GameDBMain.GameDB("Main runtime");
```

The argument becomes `db.Name`. It identifies this instance in the GameDB editor's Play Mode **Runtime GameDB** list; it does not select a file or change the generated schema. `db.ScopeName` is fixed to the scope used during generation (`"Main"` in this example).

Put the data `.json` file anywhere below an `Assets/**/Resources` directory. Pass a Resources-relative path without an extension:

```csharp
using GameDBMain;
using UnityEngine;

var db = new GameDB("Main runtime");
var error = db.Load("GameDBs/main");
if (error != null)
{
    Debug.LogException(error);
    return;
}
```

The generated synchronous overload remains:

```csharp
Exception Load(string path, bool notify = true)
```

It calls `Resources.Load<TextAsset>(path)` and imports the asset's text. If no `TextAsset` is found, it returns an `ArgumentException` containing the database name and path. It does not throw that lookup error. Invalid JSON, missing tables or fields, type mismatches, and other import failures are also returned as an `Exception` and logged through `db.Logger`.

Generated Unity databases also expose asynchronous Resources loading and an explicit transport-neutral overload:

```csharp
Awaitable LoadAsync(string path, bool notify = true,
    CancellationToken cancellationToken = default)

Awaitable LoadAsync(string location, IGameDBDataLoader loader,
    bool notify = true,
    CancellationToken cancellationToken = default)
```

The default overload uses `Resources.LoadAsync<TextAsset>`. The loader overload treats `location` as opaque and lets the supplied loader interpret it. `LoadAsync` completes normally on success and throws on transport, overlap, or import failure; cancellation throws `OperationCanceledException`. Transport failures are wrapped once in `GameDBDataLoadException`, whose `Location`, `LoaderType`, and `InnerException` identify the failed acquisition. JSON/import failures retain their concrete exception type.

All supported imports and loads are database-atomic. GameDB stages every table before publication: success replaces every table's rows together, while transport failure, cancellation, malformed JSON, missing data, or a later-table failure preserves every previous table and emits no notification. Generated table objects remain stable across reloads, but row objects are replaced on success. Import, publication, notification, and supported runtime observation are main-thread-oriented; this contract does not make GameDB reads thread-safe against concurrent background-thread access.

```csharp
try
{
    await db.LoadAsync("GameDBs/main", cancellationToken: destroyCancellationToken);
}
catch (OperationCanceledException)
{
    // The previously committed database is still active.
}
catch (Exception exception)
{
    Debug.LogException(exception);
}
```

`Resources.LoadAsync` has no abort/release API. Cancelling the returned GameDB operation stops waiting and prevents import, but Unity's underlying Resources request may still finish. GameDB does not call `Resources.UnloadAsset` on the shared `TextAsset`; normal Resources cleanup controls its lifetime. Unity `Awaitable` values are pooled and single-await—await each `LoadAsync` invocation exactly once.

## Import JSON directly

`GameDBBase` exposes synchronous JSON import overloads:

```csharp
Exception Import(string jsonData, bool notify = true)
Exception Import(string jsonData, string[] columImportList, bool notify = true)
```

The second overload imports only the named fields from every generated table. Accessing a generated property for a field that was not imported fails because that row has no backing value for it. The list contains field names, not table-qualified names; use generated schema fields where possible:

```csharp
var error = db.Import(json, new[]
{
    ItemsSchema.FieldDisplayName,
    ItemsSchema.FieldDamage
});
```

A successful load or import replaces each table's row objects. It does not mutate previously returned row instances in place.

## Load notifications and recaching

`GameDBBase.OnDBLoaded` is a public `Action`. It runs synchronously after a successful `Load`, `LoadAsync`, or `Import` when `notify` is `true` (the default):

```csharp
Items sword = null;

void CacheGameData()
{
    sword = db.ItemsTable.GetByKey(ItemsSchema.KeySword);
}

db.OnDBLoaded += CacheGameData;

var error = db.Load("GameDBs/main");
if (error != null)
{
    Debug.LogException(error);
}
```

Subscribe before the first load if the same callback should populate initial caches. Pass `notify: false` only when the caller will update dependants itself. Synchronous import catches and returns deserialization exceptions; async load throws them. An exception thrown by an `OnDBLoaded` subscriber propagates after the new data has committed from synchronous or asynchronous paths.

Each database instance permits one supported load/import mutation at a time, including transport acquisition and notification. A concurrent or reentrant operation is rejected before starting its transport; synchronous methods return `InvalidOperationException`, while async methods throw it. Reads continue to observe the last committed rows until the next complete publication.

`LoadAsync` switches back to Unity's main thread before JSON staging, publication, and notification. Async transport does not make JSON parsing or row hydration a background operation, so a large database can still incur a main-thread cost after delivery completes.

After every notified reload, reacquire and recache:

- rows returned by `GetByKey`, `TryGetByKey`, or `GetRows`;
- table-reference accessors and their referenced rows;
- generated array and dictionary values;
- derived indexes, lookup maps, and gameplay objects built from database values.

Generated scalar, array, dictionary, table-reference, and Unity-object accessors are cached on each row. A reload creates replacement rows with fresh caches, but code holding an old row continues to see that old row's cached data.

## Tables, rows, and schemas

For a string-key table, the generated API is:

```csharp
Items GetByKey(string key)
bool TryGetByKey(string key, out Items row)
Dictionary<string, Items> GetRows()
```

An enum-key table uses its configured enum instead of `string`.

```csharp
var sword = db.ItemsTable.GetByKey(ItemsSchema.KeySword);

if (db.ItemsTable.TryGetByKey("Shield", out var shield))
{
    Debug.Log(shield.DisplayNameVal);
}

foreach (var pair in db.ItemsTable.GetRows())
{
    Debug.Log($"{pair.Key}: {pair.Value.Name}");
}
```

- `GetByKey` throws `KeyNotFoundException` when the key is absent.
- `TryGetByKey` returns `false` and sets `row` to `null` when absent.
- `GetRows` creates a new dictionary containing the current row objects. Mutating that dictionary does not add or remove database rows.
- Every row inherits `Name`, which is its key represented as a string.

Generated field properties are named `<FieldName>Val`. They return concrete values for strings, `int`, `long`, `float`, `double`, `bool`, project enums, `UnityEngine.Color`, and `UnityEngine.Vector2`/`Vector3`/`Vector4`:

```csharp
string label = sword.DisplayNameVal;
int damage = sword.DamageVal;
long stableId = sword.StableIdVal;
double precisionScale = sword.PrecisionScaleVal;
List<string> tags = sword.TagsVal;
```

All non-dictionary field types may be arrays and are exposed as `List<T>`. Dictionaries cannot be arrays. A dictionary is exposed as `Dictionary<TKey,TValue>`; keys are `string` or a configured enum, and values may be any supported non-dictionary field type. Generated numeric shapes include `long`, `double`, `List<long>`, `List<double>`, `Dictionary<TKey,long>`, and `Dictionary<TKey,double>`.

Runtime JSON represents `long` with an integer token and preserves the full signed Int64 range exactly. General JavaScript parsers can lose precision outside ±9,007,199,254,740,991 unless configured for lossless integers. `double` accepts finite JSON numbers, preserves Double precision, rejects NaN/infinities/overflow, and normalizes negative zero to positive zero. Scalar, array, and dictionary values follow the same rules. Returned lists and dictionaries are cached mutable objects owned by that row; treat them as read-only game data unless temporary local mutation is intentional.

Unity-object scalar and array fields have parallel generated projections: `<Field>Val` returns `UnityObjectReference` values, `<Field>GuidVal` returns GUID strings, and `<Field>PathVal` returns paths. Unity-enabled generation additionally emits `<Field>ObjectVal`; core-only generation contains no `UnityEngine.Object` member.

## Table references

A table-reference property exposes the accessor, not the referenced row directly:

```csharp
TableReferenceAccessor<string, Categories> categoryRef = sword.CategoryVal;

if (categoryRef.IsSet())
{
    string categoryKey = categoryRef.GetKey();
    Categories category = categoryRef.GetValue();
    Debug.Log(category.DisplayNameVal);
}
```

For enum-key target tables, `GetKey()` returns that enum type. An unset reference has `IsSet() == false`; do not rely on `GetKey()` or `GetValue()` unless it is set. Invalid non-empty reference keys fail accessor construction, generally when the generated property is first read; table-reference targets are not validated by the runtime JSON import itself.

Arrays of references are `List<TableReferenceAccessor<TKey,TRow>>`. Dictionary table-reference values are also `TableReferenceAccessor<TKey,TRow>` objects; call `GetValue()` for the row. Reacquire all references after a reload.

## Unity object fields

A persisted Unity-object value is the exact JSON object:

```json
{"guid":"0123456789abcdef0123456789abcdef","path":"Assets/Game/Resources/Items/Sword.prefab"}
```

The unassigned value is `{"guid":"","path":""}`. Plain strings, `null`, half-empty references, missing or extra keys, malformed GUIDs, package paths, path traversal, and paths that do not identify an asset file beneath `Assets` are rejected during import.

For a scalar field named `Icon`, Unity generation produces:

```csharp
UnityObjectReference value = row.IconVal;
string guid = row.IconGuidVal;
string path = row.IconPathVal;
UnityEngine.Object asset = row.IconObjectVal;
```

`IconObjectVal` is emitted only for Unity-enabled generation. For a reference beneath exactly one case-sensitive `Resources` directory, it extracts the extensionless Resources path and calls `Resources.Load`, equivalent to `Resources.Load("Items/Sword")` for the example above. An empty reference returns `null`; a valid non-Resources reference throws an actionable transport error instead of returning an ambiguous missing asset. Cast a loaded result to the expected Unity type.

Unity-object arrays expose corresponding `List<UnityObjectReference>`, `List<string>` GUID/path, and Unity-only `List<UnityEngine.Object>` projections. Each projection has its own row-cache entry. Unity-object dictionary values remain `UnityObjectAccessor` objects: use `GetValue()` for the canonical reference, `GetGuid()`, `GetPath()`, and—on `GameDBLibraryUnity.UnityObjectAccessor`—`GetObject()`.

### Optional Addressables loading

When Addressables is installed separately, the optional `GameDBLibrary.Addressables` assembly adds `LoadAddressableAsync<T>()` as an extension over the existing `UnityObjectReference`; generated row classes do not change. It loads by GUID and returns `AddressableAssetLease<T>`, whose disposal releases the owned Addressables handle exactly once. Retain the lease for the complete lifetime of the asset and its dependencies rather than caching only `lease.Asset`.

See [Optional Addressables integration](addressables.md) for package/asmdef setup, **Include GUIDs in Catalog**, content builds, array/dictionary use, cancellation, and failure diagnostics.

## Play Mode editing and hot reload

A generated Unity `GameDB` registers itself with the editor when constructed in Play Mode. To edit and push runtime data:

1. Enter Play Mode and ensure the relevant generated `GameDB` instance has been constructed and loaded.
2. Open **Window → GameDB → Open Editor**.
3. Select the project database asset and the runtime instance by its constructor `Name`.
4. Click **Load GameDB**. In Play Mode this loads the asset's schema but copies current data from the selected runtime instance into the editor.
5. Edit row data. Schema editing and class generation are disabled in Play Mode.
6. Click **Reload In-Game** to serialize the editor's current data and import it into that runtime instance.
7. Click **Save GameDB** separately if the edits should also be written to the project `.json` and `.schema.json` files.

**Reload In-Game** uses the normal import path with notifications enabled. Use `OnDBLoaded` to rebuild every runtime cache as described above. This workflow is explicit; GameDB does not watch the JSON asset or automatically reload it when the file changes.

## Localization databases

Localization remains supported by the current editor and Unity generator. Enable **Localization DB** before defining the schema. Localization fields are authored as strings; each field name conventionally represents a language identifier such as `en` or `fr`, and each row represents a localization key.

Localization generation adds language-aware import and optional ordered fallbacks:

```csharp
Exception Import(string json, string language, bool notify = true)
Exception Import(string json, string language,
    IReadOnlyList<string> fallbackLanguages, bool notify = true)
string LocalizationLanguage { get; }
IReadOnlyList<string> LocalizationLanguageChain { get; }
```

Unity-enabled output adds matching Resources `Load` and Resources/custom-`IGameDBDataLoader` `LoadAsync` overloads. The primary language is always first; fallbacks retain caller order, and exact ordinal duplicates are removed with the first occurrence winning. Null, empty, whitespace-only, and unknown identifiers fail before transport or publication. GameDB does not trim, case-fold, negotiate, or infer parent locales.

The generator builds the known-language set from the ordinal union of scalar string fields across localization tables. Language field names must also be valid generated C# identifiers. Tables may declare different subsets, and externally produced row JSON may omit language fields. Loading hydrates only the normalized chain, and each row selects its first present language field. A present empty or whitespace-only string is authored content and stops fallback; fallback occurs only when the field is absent. The current editor writes dense rows, so sparse external JSON does not round-trip losslessly through Play Mode editor loading. Generated rows expose:

```csharp
string text = row.TranslatedVal;
string primary = row.LanguageVal;
string resolved = row.ResolvedLanguageVal;
```

`LocalizationLanguage` and `LanguageVal` remain the requested primary. `LocalizationLanguageChain` is the immutable effective chain, while `ResolvedLanguageVal` identifies the field used for that row. If none of the selected fields is present, `TranslatedVal` and `ResolvedLanguageVal` throw a `KeyNotFoundException` naming the row, table, and attempted chain.

The selection metadata and every staged table publish together immediately before notification. Validation, load/import failure, overlap, or cancellation preserves the previous chain and rows and emits no notification. **Reload In-Game** reuses the active normalized chain and sparse staging behavior; select a language with `Load` or `Import` before using it. Switching language requires another language-aware `Load` or `Import`; reacquire localized rows and refresh displayed text from `OnDBLoaded`.

Core-only generation includes the language-aware `Import` API and metadata but no Unity APIs. Unity-enabled generation additionally provides Resources loading, editor registration, Unity logging, and `LoadAsync`. Locale negotiation, pluralization, formatting, and Unity Localization integration remain outside GameDB.

## Intentionally unsupported surfaces

The supported generated Unity runtime path is JSON text loaded from `Resources`, acquired through an explicit `IGameDBDataLoader`, or supplied to `Import`.

- Binary, compressed, and encrypted GameDB build/load output was removed. Current generation emits no `BinaryGameDB` API.
- This package does not provide, host, or validate the old remote deployment server. The editor deployment UI was removed. Residual runtime remote-update client APIs remain only as warning-only obsolete source-compatibility shims and will be removed in GameDB 1.0.0; they are not a supported publishing/runtime workflow for new projects.
- GameDB provides synchronous object loading only through Unity-enabled `ObjectVal`/`GetObject()` Resources projections. Core-only output exposes value, GUID, and path data without a `UnityEngine.Object` API; the separately installed [optional Addressables adapter](addressables.md) loads valid non-Resources references and GameDB JSON asynchronously.
- The warning-only obsolete `ImportFromServer` remote/deployment shim is outside the supported async-loading concurrency contract. Its eventual callback uses atomic `Import`, but an outstanding unsupported remote request has no freshness guarantee against newer supported loads. Do not use it for new projects.
