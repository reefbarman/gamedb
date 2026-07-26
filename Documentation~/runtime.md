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

The normal generated overload is:

```csharp
Exception Load(string path, bool notify = true)
```

It calls `Resources.Load<TextAsset>(path)` and imports the asset's text. If no `TextAsset` is found, it returns an `ArgumentException` containing the database name and path. It does not throw that lookup error. Invalid JSON, missing tables or fields, type mismatches, and other import failures are also returned as an `Exception` and logged through `db.Logger`.

Always check the return value. Import is not transactional across the whole database: if a later table fails, tables processed earlier may already contain new rows. `OnDBLoaded` is not invoked after a failed import.

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

`GameDBBase.OnDBLoaded` is a public `Action`. It runs synchronously after a successful `Load` or `Import` when `notify` is `true` (the default):

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

Subscribe before the first load if the same callback should populate initial caches. Pass `notify: false` only when the caller will update dependants itself. Import catches and returns deserialization exceptions, but an exception thrown by an `OnDBLoaded` subscriber is outside that catch and propagates from `Load` or `Import`.

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

Generated field properties are named `<FieldName>Val`. They return concrete values for strings, `int`, `float`, `bool`, project enums, `UnityEngine.Color`, and `UnityEngine.Vector2`/`Vector3`/`Vector4`:

```csharp
string label = sword.DisplayNameVal;
int damage = sword.DamageVal;
List<string> tags = sword.TagsVal;
```

All non-dictionary field types may be arrays and are exposed as `List<T>`. Dictionaries cannot be arrays. A dictionary is exposed as `Dictionary<TKey,TValue>`; keys are `string` or a configured enum, and values may be any supported non-dictionary field type. Returned lists and dictionaries are cached mutable objects owned by that row; treat them as read-only game data unless temporary local mutation is intentional.

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

Localization generation changes the runtime contract to:

```csharp
Exception Load(string path, string language, bool notify = true)
Exception Import(string json, string language, bool notify = true)
string LocalizationLanguage { get; }
```

`Load` reads the same Resources JSON format. Both methods set `LocalizationLanguage` before validating or importing the requested data, so the property keeps the requested value even when the method returns an error. A successful import loads only the field whose name exactly matches `language`. Generated localization rows expose:

```csharp
string text = row.TranslatedVal;
string language = row.LanguageVal;
```

`TranslatedVal` reads the current language field; ordinary per-language `<FieldName>Val` properties are not generated. Switching language requires another `Load(path, language)` or `Import(json, language)`, which replaces rows and normally invokes `OnDBLoaded`; reacquire localized rows and refresh displayed text there.

Language identifiers are plain strings. Current runtime code does not provide fallback languages, locale negotiation, pluralization, formatting, or integration with Unity's Localization package. If `language` does not match a field, the partial import can still report success but `TranslatedVal` has no backing value and throws when read. The editor/generator does not currently enforce a documented language-code format, so use stable field names that exactly match the strings passed at runtime.

Generate localization classes with **Generate for Unity** enabled. With Unity generation disabled, the generated class still has a private-set `LocalizationLanguage`, but no generated public overload sets it; that output does not expose a complete language-selection workflow.

## Intentionally unsupported surfaces

The supported generated Unity runtime path is JSON text loaded from `Resources` or supplied to `Import`.

- Binary, compressed, and encrypted GameDB build/load output was removed. Current generation emits no `BinaryGameDB` API.
- This package does not provide, host, or validate the old remote deployment server. The editor deployment UI was removed. Residual runtime remote-update client APIs remain only as warning-only obsolete source-compatibility shims and will be removed in GameDB 1.0.0; they are not a supported publishing/runtime workflow for new projects.
- GameDB provides synchronous object loading only through Unity-enabled `ObjectVal`/`GetObject()` Resources projections. Core-only output exposes value, GUID, and path data without a `UnityEngine.Object` API; the separately installed [optional Addressables adapter](addressables.md) loads valid non-Resources references asynchronously.
