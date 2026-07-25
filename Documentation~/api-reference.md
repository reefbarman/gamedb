# GameDB API reference

This is a curated reference for the supported public C# surface in the current GameDB package for Unity 6.5. It distinguishes hand-written runtime APIs, schema-generated APIs, and editor-only APIs. A C# declaration being `public` does not by itself make it a supported consumer contract; implementation scaffolding and retained compatibility types are identified separately below.

## Assemblies and namespaces

| Assembly                   | Availability                                                               | Consumer namespaces                                                     |
| -------------------------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------- |
| `GameDBLibrary`            | Runtime and Editor; auto-referenced                                        | `GameDBLibrary`, `GameDBLibraryUnity`, plus the global `Row` base class |
| `GameDBEditorLibrary`      | Unity Editor only (`includePlatforms: Editor`); references `GameDBLibrary` | `GameDBEditorLibrary`, `GameDBEditorLibrary.Automation`                 |
| Generated project assembly | Wherever the generated `.cs` files are placed under `Assets/`              | `GameDB{ScopeName}`                                                     |

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

When C# is generated without Unity-specific accessors, color and vector fields return these types from `GameDBLibrary`.

| Type      | Public shape                                                                                                                 | Parsing/formatting                                                                                                                                                                      |
| --------- | ---------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Color`   | `byte r/g/b/a { get; set; }`; `string Hex { get; set; }`; `Color(string hex)`; `Color(byte r, byte g, byte b, byte a = 255)` | Accepts `#RRGGBB`, `RRGGBB`, `0xRRGGBB`, and 8-digit equivalents. Invalid length/digits can throw parsing or range exceptions. `ToString()` returns `Hex`; alpha is omitted when `255`. |
| `Vector2` | `float x/y { get; set; }`; numeric and `string` constructors                                                                 | String form is comma-separated and parsed with the current culture. Missing/invalid components can throw. `ToString()` emits comma-separated components.                                |
| `Vector3` | `float x/y/z { get; set; }`; numeric and `string` constructors                                                               | Same semantics with three components.                                                                                                                                                   |
| `Vector4` | `float x/y/z/w { get; set; }`; numeric and `string` constructors                                                             | Same semantics with four components.                                                                                                                                                    |

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

| Schema field                                   | Generated value                                                                                        |
| ---------------------------------------------- | ------------------------------------------------------------------------------------------------------ |
| `string`, `int`, `float`, `bool`, project enum | Scalar type, or `List<T>` for an array                                                                 |
| `color`, `vector2/3/4` with Unity loading      | `UnityEngine.Color` / `UnityEngine.Vector2/3/4`                                                        |
| `color`, `vector2/3/4` without Unity loading   | `GameDBLibrary.Color` / `Vector2/3/4`                                                                  |
| `unityObject` with Unity loading               | `<FieldName>PathVal` (`string`) and `<FieldName>ObjectVal` (`UnityEngine.Object`); arrays become lists |
| `unityObject` without Unity loading            | `<FieldName>PathVal` (`string`) only                                                                   |
| `tableRef`                                     | `TableReferenceAccessor<TKey, TRow>`; arrays become lists of accessors                                 |
| dictionary                                     | `Dictionary<TKey, TValue>`; table-reference and Unity-object values may themselves be accessor objects |

Field getters cache their converted accessor/value for the lifetime of the generated row. Generated lists and dictionaries are mutable cached objects owned by that row; treat them as read-only game data. Loading new data replaces rows, so callers should reacquire row, list, dictionary, and table-reference values after import/reload rather than assuming an old object updates in place.

`UnityObjectAccessor.GetObject()` derives a `Resources` path from the stored asset path and calls `Resources.Load`. A malformed path that lacks the expected `Resources` segment or extension can throw; a valid path with no matching object returns `null`.

### Generated schema constants

```csharp
public static class <TableName>Schema
{
    public static string TableName;
    public static string Field<FieldName>;
    public static <TKey> Key<RowKeyWithoutWhitespace>;
}
```

These are mutable public static fields in the current generator, not `const` or `readonly`. Treat them as generated constants and do not assign to them. Row-key member names have whitespace removed. Generation validates scope, table, and field identifiers, but it does not separately validate or de-duplicate generated row-key member names after whitespace removal; inspect compiler output for collisions or invalid row-key identifiers.

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
    public static GameDBAutomationResult Validate(string databasePath);
    public static GameDBExportResult ExportJson(string databasePath);

    public static GameDBAutomationResult Create(GameDBCreateRequest request);
    public static GameDBAutomationResult Save(GameDBSaveRequest request);
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

`Load` is an alias for `Inspect` and reports `Operation == "inspect"`. Expected bad input, invalid paths, conflicts, validation failures, and caught implementation exceptions are represented by `Success == false` and `Message`; inspect `Issues` for structured validation details. Callers should not rely on exceptions for normal failure handling.

`Inspect` can return `Success == true` with non-empty validation issues; use `Validate` when validity must determine success. `ExportJson` can return `Success == false` while still supplying serialized data/schema JSON and issues. Early failures generally have no snapshot/issues, while validation-blocked operations can return a prospective snapshot and populated issues.

Mutations support `DryRun`, `ExpectedRevision`, and `AllowDestructive` through `GameDBOperationOptions`. Results report operation/path/message, before/after revisions, a snapshot, validation issues, and changed paths. Result and snapshot properties have public getters with `internal` setters and are service-produced values. They are not deeply immutable: exposed lists/dictionaries remain mutable, row-value snapshots are shallow, and values may use runtime CLR objects rather than the original JSON wire representation.

Request DTOs include `GameDBCreateRequest`, `GameDBSaveRequest`, `GameDBTableRequest`, `GameDBRenameRequest`, `GameDBDeleteRequest`, `GameDBFieldRequest`, `GameDBDictionaryTypeDefinition`, `GameDBRowRequest`, `GameDBValueRequest`, and `GameDBGenerateRequest`. Result DTOs include `GameDBAutomationResult`, `GameDBExportResult`, `GameDBListResult`, `GameDBSnapshot`, table/field/row snapshots, and `GameDBValidationIssue`.

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

The current stable IDs are `index`, `readme`, `editor-authoring`, `runtime`, `api-reference`, `automation`, `google-sheets`, `basic-sample`, and `changelog`.

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
Utils.UrlCombine(...);
JsonPatch.Patch(...);       // used by legacy revision patching
GameDBEditor.RegisterRevisionPromotionCallback(...);
```

The package does not provide, host, authenticate, or validate the historical Go/AWS GameDB deployment server. These APIs assume its response and JSON-patch protocol. `ImportFromServer` falls back to importing the built-in JSON after a remote error and reports through its callback/logging; callers must repeatedly call the returned `RequestUpdater.Update()` from an update loop to advance Unity web requests. Cache and patch operations perform local file I/O and can return or log network, parsing, reflection, or file errors.

There are no `[Obsolete]` attributes on this surface in the current source, so the unsupported status is documentation-level and does not produce compiler warnings. A separately secured and tested protocol-compatible service is the caller's responsibility.

## Public declarations that are not supported consumer contracts

The runtime/editor assemblies expose additional declarations because generated code, serialization, reflection, UI components, or legacy internals need them. They are not documented as stable application APIs:

- generated-code infrastructure such as `FieldBase`, `DictionaryType`, `IDataAccessor`, `DataAccessor<T>`, scalar accessor classes, and `DictionaryAccessor<,>`; generated files depend on this ABI, but applications should use generated typed properties instead of constructing these types directly;
- exposed infrastructure such as `IGameDB`, `Singleton<T>`, and `GameDBEditorInvoker`; these are not recommended consumer extension points (`GameDBEditorInvoker` is a reflection bridge used by Unity-enabled generated constructors);
- generic helpers such as `JsonHelper`, `Utils` outside the retained remote use noted above, and constructors/protected storage on row/table bases;
- editor model, data-source, component, settings, exporter, request, and UI helper types;
- `GameDBEditorWindow.ShowWindow`, which is primarily the implementation of **Window > GameDB > Open Editor**.

Some of these declarations may remain binary/source compatible incidentally, but the package currently has no API marker attributes or explicit compatibility policy that makes them supported. If external consumers already depend on one, treat that dependency as an ambiguity to resolve before changing the declaration.

## Known ambiguities and caveats

- Support intent is inferred from generated templates, package documentation, XML comments, tests, and usage; it is not enforced by a dedicated public-API analyzer or assembly facade.
- No current public API is compiler-deprecated with `[Obsolete]`, including the unsupported remote client.
- `Row` and `GameDBEditorWindow` are public in the global namespace, while most APIs use explicit GameDB namespaces.
- Generated-code infrastructure is a required compile-time ABI for generated files even where it is not a recommended direct consumer API.
- Generated schema members are mutable static fields even though callers should treat them as constants.
- Generated identifiers are derived from scope, table, field, and row names. Regeneration can therefore change the C# API after schema/name changes; row-key static-member normalization is not fully validated by generation.
- Supported package documents are exposed to editor agents through stable catalog IDs; use `ListDocuments()` rather than hard-coding PackageCache paths.
