# GameDB API reference

This is a curated reference for the supported public C# surface in the current GameDB package for Unity 6.5. It distinguishes hand-written runtime APIs, schema-generated APIs, and editor-only APIs. A C# declaration being `public` does not by itself make it a supported consumer contract; implementation scaffolding and retained compatibility types are identified separately below.

## Assemblies and namespaces

| Assembly                     | Availability                                                                              | Consumer namespaces                                                     |
| ---------------------------- | ----------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `GameDBLibrary`              | Runtime and Editor; auto-referenced                                                       | `GameDBLibrary`, `GameDBLibraryUnity`, plus the global `Row` base class |
| `GameDBLibrary.Addressables` | Optional runtime assembly; compiled only with supported separately installed Addressables | `GameDBLibraryAddressables`                                             |
| `GameDBEditorLibrary`        | Unity Editor only (`includePlatforms: Editor`); references `GameDBLibrary`                | `GameDBEditorLibrary`, `GameDBEditorLibrary.Automation`                 |
| Generated project assembly   | Wherever the generated `.cs` files are placed under `Assets/`                             | `GameDB{ScopeName}`                                                     |

Do not reference `GameDBEditorLibrary` from a player/runtime assembly. Generated code references `GameDBLibrary`; generation with the Unity loader enabled also emits Unity-specific value access and a `Resources.Load` helper.

## Hand-written runtime API

### `GameDBLibrary.GameDBBase`

Generated `GameDB{ScopeName}.GameDB` classes derive from this type. Applications normally construct the generated class rather than subclassing `GameDBBase` themselves.

| Member                                                                            | Behavior                                                                                                                                                            |
| --------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Action OnDBLoaded`                                                               | Invoked after a successful import when `notify` is `true`. It is a public delegate field; use `+=`/`-=` when subscribing.                                           |
| `string ScopeName { get; }`                                                       | Scope fixed by the generated constructor.                                                                                                                           |
| `string Name { get; }`                                                            | Instance name passed to the generated `GameDB` constructor.                                                                                                         |
| `Logger Logger { get; set; }`                                                     | Controls internal logging. Unity-enabled generated classes install a generated Unity-console logger.                                                                |
| `Exception Import(string jsonData, bool notify = true)`                           | Imports all fields from data JSON. Returns `null` on success or the caught exception on failure.                                                                    |
| `Exception Import(string jsonData, string[] columImportList, bool notify = true)` | Imports only named fields. Returns `null` on success or the caught exception on failure. The parameter name is spelled `columImportList` in the current binary API. |

Import failures are logged through `Logger` and returned, not rethrown by these methods. `OnDBLoaded` is not invoked after a failed import. Missing rows/fields, malformed JSON, duplicate JSON properties, and incompatible values can produce the returned exception. Import is not database-wide transactional: if a later table fails, tables deserialized earlier in the same call may already have been replaced. `OnDBLoaded` runs synchronously outside the deserialization catch, so an exception thrown by a subscriber propagates from `Import`/generated `Load` rather than being returned.

The protected constructor and protected `Tables` property exist for generated subclasses:

```csharp
protected GameDBBase(string dbName, string scopeName);
protected Dictionary<string, TableBase> Tables { get; }
```

They are generated-code extension points, not a recommended alternative to generated typed access.

### `GameDBLibrary.Logger`

```csharp
public class Logger
{
    public virtual void Log(string message);
    public virtual void LogError(string message);
    public virtual void LogException(Exception e);
}
```

Assign a subclass to `GameDBBase.Logger` to redirect logs. The base implementation writes to `System.Console`. Unity-enabled generated code supplies a nested `UnityLogger` that writes to `UnityEngine.Debug`.

### Row and table bases

These bases are part of the generated runtime contract.

| Type/member                                 | Behavior                                                                                                                                                                                |
| ------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Row`                                       | Public global-namespace class from `GameDBLibrary`; generated row classes derive from it. Its accessor-cache methods support generated properties and are not normally called directly. |
| `GameDBLibrary.RowBase.Name`                | The row key as a string.                                                                                                                                                                |
| `object RowBase.GetValue(string field)`     | Returns the deserialized backing value; throws `KeyNotFoundException` if the field was not imported or does not exist. Prefer generated typed properties.                               |
| `GameDBLibrary.TableBase.Name`              | Table name.                                                                                                                                                                             |
| `RowBase TableBase.GetByKeyRaw(string key)` | Untyped lookup used by generated table-reference accessors; throws `KeyNotFoundException` for a missing key. Prefer generated `GetByKey`/`TryGetByKey`.                                 |

`Row`, `RowBase`, and `TableBase` expose constructors and protected storage primarily so generated classes can inherit from them. Direct mutation of their protected dictionaries is not a supported data-editing API.

### Runtime value types

When C# is generated without Unity-specific accessors, color and vector fields return these types from `GameDBLibrary`. Unity-object fields use the same core reference type in both generation modes.

| Type                   | Public shape                                                                                                                 | Parsing/formatting                                                                                                                                                                      |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Color`                | `byte r/g/b/a { get; set; }`; `string Hex { get; set; }`; `Color(string hex)`; `Color(byte r, byte g, byte b, byte a = 255)` | Accepts `#RRGGBB`, `RRGGBB`, `0xRRGGBB`, and 8-digit equivalents. Invalid length/digits can throw parsing or range exceptions. `ToString()` returns `Hex`; alpha is omitted when `255`. |
| `Vector2`              | `float x/y { get; set; }`; numeric and `string` constructors                                                                 | String form is invariant-culture comma-separated components. Missing, invalid, or non-finite components can throw. `ToString()` emits invariant round-trip components.                  |
| `Vector3`              | `float x/y/z { get; set; }`; numeric and `string` constructors                                                               | Same semantics with three components.                                                                                                                                                   |
| `Vector4`              | `float x/y/z/w { get; set; }`; numeric and `string` constructors                                                             | Same semantics with four components.                                                                                                                                                    |
| `UnityObjectReference` | read-only `Guid`, `Path`, and `IsEmpty`; value equality                                                                      | Contains either two empty strings or a lowercase 32-character asset GUID plus a main-asset path beneath `Assets/`. The wire object has exactly lowercase `guid` and `path` keys.        |

When Addressables is installed separately, `GameDBLibraryAddressables` exposes:

```csharp
Awaitable<AddressableAssetLease<T>> LoadAddressableAsync<T>(
    this UnityObjectReference reference,
    CancellationToken cancellationToken = default)
    where T : UnityEngine.Object;
```

`AddressableAssetLease<T>` has read-only `Asset` and `IsDisposed` properties and an idempotent `Dispose()`. Every successful call owns one load reference until its lease is disposed; accessing `Asset` after disposal throws `ObjectDisposedException`. Expected Addressables failures throw `AddressableAssetLoadException` with `AssetGuid`, `AssetPath`, `RequestedType`, and an underlying `InnerException` when available. See the [optional Addressables contract](addressables.md).

Unity conversions are extension methods in `GameDBLibraryUnity.TypeHelpers`:

```csharp
UnityEngine.Color   ToUnityColor(this GameDBLibrary.Color color);
UnityEngine.Vector2 ToUnityVector(this GameDBLibrary.Vector2 vec);
UnityEngine.Vector3 ToUnityVector(this GameDBLibrary.Vector3 vec);
UnityEngine.Vector4 ToUnityVector(this GameDBLibrary.Vector4 vec);
```

### Shared schema enums

These `GameDBLibrary` enums appear in editor automation requests/snapshots and are also consumed by generated schema infrastructure:

```csharp
public enum FieldType
{
    @bool, color, dictionary, @enum, @float, @int, @string,
    tableRef, unityObject, vector2, vector3, vector4
}

public enum KeyType { @enum, @string }
```

Enum member names are part of the serialized schema/code-generation contract. Use the enum members rather than relying on their current numeric ordinals.

### Schema file format

Editor-authored `.schema.json` files require the root-level JSON integer `"formatVersion": 3`; it is independent of the package's SemVer version. GameDB validates it before hydrating schema tables or data. Missing, malformed, older, and newer values fail the editor/document load without rewriting either file.

`GameDBAutomationService.Save` applies the same rule to `GameDBSaveRequest.SchemaJson`, including new-file and dry-run requests. Expected format failures are returned through the operation's normal failure result: general operations expose the actionable `Message`, while Batch, Query, and CSV classify load failures through their existing failure kinds and error codes.

### Table references

Generated table-reference properties expose the accessor rather than only the referenced row:

```csharp
public class TableReferenceAccessor<TKey, TRow>
{
    public bool IsSet();
    public TKey GetKey();
    public TRow GetValue();
}
```

`IsSet()` distinguishes an unset reference from a set reference. `GetKey()` returns the typed string/enum key, and `GetValue()` returns the referenced generated row. During accessor construction, a set but missing target is resolved through the generated table and can surface reflection or key-lookup exceptions. Editor automation rejects broken references before saving, but runtime JSON imported from another source can still be invalid.

## Generated runtime API

Generate classes after schema changes. Concrete names and property types are schema-dependent, and generated files state that they must not be edited manually.

For scope `MyData`, all generated types are in:

```csharp
namespace GameDBMyData
```

### Generated database class

```csharp
public class GameDB : GameDBBase
{
    public GameDB(string name);
    public <TableName>Table <TableName>Table { get; }
}
```

The constructor creates each generated table. When Unity loading is included, it also registers the runtime database with the editor in `UNITY_EDITOR` and installs a Unity logger.

Unity-loader generation adds:

```csharp
public Exception Load(string path, bool notify = true);
```

`path` is relative to a Unity `Resources` folder and omits the file extension. `Load` returns an `ArgumentException` if no `TextAsset` is found; otherwise it returns the result of `Import`. It does not throw expected load/import failures itself.

A localization database instead adds:

```csharp
public string LocalizationLanguage { get; private set; }
public Exception Load(string path, string language, bool notify = true);
public Exception Import(string json, string language, bool notify = true);
```

These methods set `LocalizationLanguage` before validating or importing the requested data, so the property keeps the requested value even when the method returns an error. A successful import loads only the requested language column. Localization rows expose `TranslatedVal` and `LanguageVal`. If `language` does not match a schema field, partial import can still return success, but reading `TranslatedVal` then fails because no backing value was imported. There is no built-in fallback or locale negotiation.

If `IncludeUnityLoader` is `false`, no generated `Load` method, Unity logger, editor registration, or Unity-specific value conversion is emitted; call the inherited `Import` methods with JSON text.

### Generated table class

For every table:

```csharp
public class <TableName>Table : TableBase
{
    public <TableName> GetByKey(<TKey> key);
    public bool TryGetByKey(<TKey> key, out <TableName> row);
    public Dictionary<<TKey>, <TableName>> GetRows();
}
```

`TKey` is `string` or the configured project enum.

| Member        | Missing-key and allocation semantics                                                                                                               |
| ------------- | -------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GetByKey`    | Uses dictionary indexing and throws `KeyNotFoundException` when absent.                                                                            |
| `TryGetByKey` | Returns `false` and sets `row` to `null` when absent or not of the generated row type.                                                             |
| `GetRows`     | Returns a newly allocated dictionary snapshot containing the current generated row objects. Mutating that dictionary does not mutate the database. |

### Generated row class and field properties

```csharp
public class <TableName> : Row
{
    public <TableName>(string key, GameDB gameDB);
    public <T> <FieldName>Val { get; }
}
```

Generated scalar/array return shapes are:

| Schema field                                   | Generated value                                                                                                              |
| ---------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------- |
| `string`, `int`, `float`, `bool`, project enum | Scalar type, or `List<T>` for an array                                                                                       |
| `color`, `vector2/3/4` with Unity loading      | `UnityEngine.Color` / `UnityEngine.Vector2/3/4`                                                                              |
| `color`, `vector2/3/4` without Unity loading   | `GameDBLibrary.Color` / `Vector2/3/4`                                                                                        |
| `unityObject`, all outputs                     | `<FieldName>Val` (`UnityObjectReference`), `<FieldName>GuidVal`, and `<FieldName>PathVal`; arrays become corresponding lists |
| `unityObject`, Unity-enabled only              | additionally `<FieldName>ObjectVal` (`UnityEngine.Object`), or `List<UnityEngine.Object>` for arrays                         |
| `tableRef`                                     | `TableReferenceAccessor<TKey, TRow>`; arrays become lists of accessors                                                       |
| dictionary                                     | `Dictionary<TKey, TValue>`; table-reference and Unity-object values may themselves be accessor objects                       |

Field getters cache their converted accessor/value for the lifetime of the generated row. Generated lists and dictionaries are mutable cached objects owned by that row; treat them as read-only game data. Loading new data replaces rows, so callers should reacquire row, list, dictionary, and table-reference values after import/reload rather than assuming an old object updates in place.

The core `UnityObjectAccessor` returns the canonical reference through `GetValue()` and exposes `GetGuid()` and `GetPath()`. `GameDBLibraryUnity.UnityObjectAccessor` additionally exposes `GetObject()`, which loads references beneath exactly one case-sensitive `Resources` directory. Empty returns `null`; a valid non-Resources reference throws `InvalidOperationException`, and malformed wire values are rejected before accessor construction. Unity-object dictionary values remain accessor objects rather than parallel generated dictionaries.

### Generated schema constants

```csharp
public static class <TableName>Schema
{
    public const string TableName = "<TableName>";
    public const string Field<FieldName> = "<FieldName>";
    public const string Key<StringRowKey> = "<row key>";
    public static readonly <TEnum> Key<EnumRowKey> = <TEnum>.<member>;
}
```

Generation validates row-key members, generated accessors/types, and case-insensitive filename collisions before writing. String schema members are constants; enum-key members are typed `static readonly` values.

## Editor-only API

### Automation service (recommended)

Namespace: `GameDBEditorLibrary.Automation`

`GameDBAutomationService` is the supported transport-neutral API for deterministic, path-addressed inspection and mutation. It does not depend on the currently selected database in the editor window.

```csharp
public static class GameDBAutomationService
{
    public static GameDBListResult ListDatabases(string searchDirectory = "Assets");
    public static GameDBAutomationResult Load(string databasePath);
    public static GameDBAutomationResult Inspect(string databasePath);
    public static GameDBQueryResult Query(GameDBQueryRequest request);
    public static GameDBCsvExportResult ExportCsv(GameDBCsvExportRequest request);
    public static GameDBAutomationResult Validate(string databasePath);
    public static GameDBExportResult ExportJson(string databasePath);

    public static GameDBAutomationResult Create(GameDBCreateRequest request);
    public static GameDBAutomationResult Save(GameDBSaveRequest request);
    public static GameDBBatchResult ApplyBatch(GameDBBatchRequest request);
    public static GameDBCsvImportResult ImportCsv(GameDBCsvImportRequest request);
    public static GameDBAutomationResult AddTable(GameDBTableRequest request);
    public static GameDBAutomationResult RenameTable(GameDBRenameRequest request);
    public static GameDBAutomationResult DeleteTable(GameDBDeleteRequest request);
    public static GameDBAutomationResult AddField(GameDBFieldRequest request);
    public static GameDBAutomationResult ReplaceField(GameDBFieldRequest request);
    public static GameDBAutomationResult RenameField(GameDBRenameRequest request);
    public static GameDBAutomationResult DeleteField(GameDBDeleteRequest request);
    public static GameDBAutomationResult AddRow(GameDBRowRequest request);
    public static GameDBAutomationResult UpdateRow(GameDBRowRequest request);
    public static GameDBAutomationResult SetValue(GameDBValueRequest request);
    public static GameDBAutomationResult RenameRow(GameDBRenameRequest request);
    public static GameDBAutomationResult DeleteRow(GameDBDeleteRequest request);
    public static GameDBAutomationResult GenerateCSharp(GameDBGenerateRequest request);
}
```

`Load` is an alias for `Inspect` and reports `Operation == "inspect"`. Expected bad input, invalid paths, conflicts, validation failures, and caught implementation exceptions are represented by `Success == false` and `Message`; general automation operations expose structured validation details through `Issues`, while Query uses `GameDBQueryResult.FailureKind` and `Errors`. Callers should not rely on exceptions for normal failure handling.

`Inspect` can return `Success == true` with non-empty validation issues; use `Validate` when validity must determine success. `ExportJson` can return `Success == false` while still supplying serialized data/schema JSON and issues. Exported schema JSON includes the required current `formatVersion` and can be supplied to a later guarded `Save`. Early failures generally have no snapshot/issues, while validation-blocked operations can return a prospective snapshot and populated issues.

#### Query API

`Query` accepts a `GameDBQueryRequest` containing one or more exact `GameDBQueryTableProjection` values. Each projection selects rows and fields and may contain AND-combined typed `GameDBQueryPredicate` values. Results use deterministic ordinal table/row/field ordering and a global `Limit`; continuation uses an opaque database-, revision-, and query-bound cursor. `GameDBQueryResult` reports structured `GameDBQueryFailureKind` and `GameDBQueryError` values and returns projected rows as normalized JSON-compatible CLR shapes rather than the model CLR values exposed by `GameDBSnapshot`. Unity-object projections contain both `guid` and `path`; equality and array membership match empty-to-empty or non-empty references by ordinal GUID.

Query request types are `GameDBQueryRequest`, `GameDBQueryTableProjection`, `GameDBQueryPredicate`, and `GameDBQueryPredicateKind`. Result types are `GameDBQueryResult`, `GameDBQueryTableResult`, `GameDBQueryRowResult`, `GameDBQueryError`, and `GameDBQueryFailureKind`. See the [Query API contract](automation.md#query-api) for projection, predicate/type compatibility, ordering, global pagination, cursor, failure, and wire-value semantics.

#### CSV API

`ExportCsv` and `ImportCsv` exchange one existing table as in-memory RFC 4180 CSV. The reserved first column is `__key`; fields and rows use ordinal ordering; scalar and enum values use invariant canonical text; Unity-object cells use compact canonical JSON; and exported headers, keys, and values receive reversible formula-injection escaping. Raw paths and malformed or partial Unity-object values are rejected. Arrays and dictionaries are deliberately unsupported by the current CSV dialect. `GameDBCsvImportMode.Upsert` permits partial field columns, while `Replace` requires every scalar field plus destructive authorization and replaces the table's complete row set.

Request types are `GameDBCsvExportRequest`, `GameDBCsvImportRequest`, and `GameDBCsvImportMode`. Result types are `GameDBCsvExportResult`, `GameDBCsvImportResult`, `GameDBCsvError`, `GameDBCsvFailureKind`, and `GameDBCsvCommitStatus`. Import uses `GameDBOperationOptions` for dry runs, revision guards, and replace authorization. See the [CSV import and export contract](automation.md#csv-import-and-export) for the dialect, scalar/empty-cell matrix, transaction behavior, formula escaping, and 1-based error coordinates.

Single mutations support `DryRun`, `ExpectedRevision`, and `AllowDestructive` through `GameDBOperationOptions`. Results report operation/path/message, before/after revisions, a snapshot, validation issues, and changed paths. Result and snapshot properties have public getters with `internal` setters and are service-produced values. They are not deeply immutable: exposed lists/dictionaries remain mutable, row-value snapshots are shallow, and values may use runtime CLR objects rather than the original JSON wire representation.

`ApplyBatch` uses `GameDBBatchRequest` and `GameDBBatchOptions` to apply ordered `GameDBBatchOperation` values with one database load, revision check, whole-model validation, and save. `GameDBBatchOperationKind` is the discriminant for table, rename, delete, field, row, and value payload DTOs. `AllowedDestructiveOperations` is an explicit kind allowlist. `GameDBBatchResult` adds `FailureKind`, `FailedOperationIndex`, `DeniedOperationKind`, `CommitStatus`, and structured file/post-save/recovery state so callers do not need to parse messages or blindly retry a partially published save.

Request DTOs include `GameDBCreateRequest`, `GameDBSaveRequest`, `GameDBTableRequest`, `GameDBRenameRequest`, `GameDBDeleteRequest`, `GameDBFieldRequest`, `GameDBDictionaryTypeDefinition`, `GameDBRowRequest`, `GameDBValueRequest`, `GameDBGenerateRequest`, `GameDBBatchRequest`, `GameDBBatchOptions`, `GameDBBatchOperation`, its six payload DTOs, `GameDBQueryRequest`, `GameDBQueryTableProjection`, `GameDBQueryPredicate`, `GameDBCsvExportRequest`, and `GameDBCsvImportRequest`. Result DTOs include `GameDBAutomationResult`, `GameDBBatchResult`, `GameDBQueryResult`, `GameDBQueryTableResult`, `GameDBQueryRowResult`, `GameDBQueryError`, `GameDBCsvExportResult`, `GameDBCsvImportResult`, `GameDBCsvError`, `GameDBExportResult`, `GameDBListResult`, `GameDBSnapshot`, table/field/row snapshots, and `GameDBValidationIssue`.

See [GameDB editor automation](automation.md) for the path contract, request DTO/value shapes, destructive-operation rules, revision semantics, reference integrity, dry runs, and examples. Those details are intentionally not duplicated here.

### Bundled documentation service

Namespace: `GameDBEditorLibrary.Automation`

```csharp
public static class GameDBDocumentationService
{
    public static GameDBDocumentationCatalog ListDocuments();
    public static GameDBDocumentationResult ReadDocument(string documentId);
}
```

`ListDocuments` returns copies of entries from a fixed package catalog. `ReadDocument` is case-insensitive by ID, resolves the installed package root through Unity Package Manager, and reads only catalogued package-relative paths. Missing/unknown IDs, package resolution failures, path containment failures, and file errors return `Success == false` with `Message` rather than escaping as exceptions.

Returned DTOs are read-only to consumers (`internal` setters):

| Type                         | Readable properties                                                    |
| ---------------------------- | ---------------------------------------------------------------------- |
| `GameDBDocumentationCatalog` | `Success`, `Message`, `Documents`                                      |
| `GameDBDocumentationEntry`   | `Id`, `Title`, `RelativePath`                                          |
| `GameDBDocumentationResult`  | `Success`, `DocumentId`, `Title`, `RelativePath`, `Content`, `Message` |

The current stable IDs are `index`, `readme`, `editor-authoring`, `runtime`, `addressables`, `api-reference`, `automation`, `basic-sample`, and `changelog`.

### Stateful editor facade

Namespace: `GameDBEditorLibrary`

`GameDBEditor` predates the automation service and operates on the singleton database selected/loaded in the GameDB editor. Use it for existing editor integrations and callbacks; prefer `GameDBAutomationService` for new scripted data changes.

```csharp
public static bool LoadGameDB(string gameDBPath);
public static bool SaveGameDB();
public static void AddRowToTable(
    string table,
    string key,
    Dictionary<string, object> data);
public static void RegisterSavedGameDBCallback(Action<string> onSaved);
```

- Paths accepted by this facade are relative to `Application.dataPath`, unlike automation's `Assets/...` contract.
- `LoadGameDB` and `SaveGameDB` catch file/model exceptions, log them to the Unity Console, and return `false`.
- `AddRowToTable` requires a database to be loaded first. Invalid field/value data can throw; it does not save automatically.
- `RegisterSavedGameDBCallback` adds a callback and exposes no matching unregister method. The callback receives the saved scope name.

`GameDBEditor.AddRuntimeDB`, `Init`, `OnGUI`, and `Update` are public for generated/editor-window plumbing and are not supported application entry points.

## Retained legacy remote client APIs

The following runtime surface remains public for source compatibility but is **unsupported for new production use** in this Unity 6.5 package:

```csharp
GameDBBase.ImportFromServer(...);
Remote.GetLatestDeployment(...);
RequestUpdater.OnUpdate;
RequestUpdater.Update();
WebRequestHelper.StartRequest(...);
WebRequestHelper.StartPostRequest(...);
WebRequestHelper.CreateForm();
RequestMethod;              // POST, GET
IDownloadHandler;
ServerResponse.HandleBasicResponse(...);
GameDBLibraryUnity.UnityForm;
Utils.GetChecksum(...);
GameDBEditor.RegisterRevisionPromotionCallback(...);
```

The package does not provide, host, authenticate, or validate the historical Go/AWS GameDB deployment server. These APIs assume its response and JSON-patch protocol. `ImportFromServer` falls back to importing the built-in JSON after a remote error and reports through its callback/logging; callers must repeatedly call the returned `RequestUpdater.Update()` from an update loop to advance Unity web requests. Cache and patch operations perform local file I/O and can return or log network, parsing, reflection, or file errors.

Each type or member listed above is marked with warning-only `[Obsolete]` and is planned for removal in GameDB `1.0.0`. The internal `GameDBLibraryUnity.UnityWebRequestTransport` implementation is obsolete on the same schedule. `GameDBEditor.RegisterRevisionPromotionCallback` remains only as a no-op source-compatibility shim because the revision-promotion UI was removed. A separately secured and tested protocol-compatible service is the caller's responsibility.

`Utils.UrlCombine(...)` and `JsonPatch.Patch(...)` remain non-obsolete generic helpers. They are not part of the deprecated remote client contract.

## Public declarations that are not supported consumer contracts

The runtime/editor assemblies expose additional declarations because generated code, serialization, reflection, UI components, or legacy internals need them. They are not documented as stable application APIs:

- generated-code infrastructure such as `FieldBase`, `DictionaryType`, `IDataAccessor`, `DataAccessor<T>`, scalar accessor classes, and `DictionaryAccessor<,>`; generated files depend on this ABI, but applications should use generated typed properties instead of constructing these types directly;
- exposed infrastructure such as `IGameDB`, `Singleton<T>`, and `GameDBEditorInvoker`; these are not recommended consumer extension points (`GameDBEditorInvoker` is a reflection bridge used by Unity-enabled generated constructors);
- generic helpers such as `JsonHelper`, most of `Utils`, and constructors/protected storage on row/table bases; `Utils.UrlCombine` and `JsonPatch.Patch` are retained non-obsolete helpers, while `Utils.GetChecksum` is part of the deprecated remote surface;
- editor model, data-source, component, settings, exporter, request, and UI helper types;
- `GameDBEditorWindow.ShowWindow`, which is primarily the implementation of **Window > GameDB > Open Editor**.

Some of these declarations may remain binary/source compatible incidentally, but the package currently has no API marker attributes or explicit compatibility policy that makes them supported. If external consumers already depend on one, treat that dependency as an ambiguity to resolve before changing the declaration.

## Known ambiguities and caveats

- Support intent is inferred from generated templates, package documentation, XML comments, tests, and usage; it is not enforced by a dedicated public-API analyzer or assembly facade.
- The unsupported legacy remote client types and members are compiler-deprecated with warning-only `[Obsolete]` and are planned for removal in GameDB `1.0.0`.
- `Row` and `GameDBEditorWindow` are public in the global namespace, while most APIs use explicit GameDB namespaces.
- Generated-code infrastructure is a required compile-time ABI for generated files even where it is not a recommended direct consumer API.
- Generated schema string members are `const`; enum-key members are `static readonly`. Generated database, table, and row classes are `partial`, while schema classes remain static.
- Generated identifiers are derived from scope, table, field, and row names. Regeneration can therefore change the C# API after schema/name changes, but generation validates the complete symbol and case-insensitive filename set before replacing output.
- Supported package documents are exposed to editor agents through stable catalog IDs; use `ListDocuments()` rather than hard-coding PackageCache paths.
