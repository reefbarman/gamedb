# GameDB editor automation

`GameDBEditorLibrary.Automation.GameDBAutomationService` is a public, transport-neutral editor API. It can be called from editor tooling, tests, or an existing Unity automation bridge such as Coplay MCP's `execute_code` command.

The API has no dependency on Coplay, MCP, HTTP, or a hosted GameDB service.

## Read bundled documentation

Agents can discover and read package documentation through stable IDs without locating the PackageCache directory:

```csharp
var catalog = GameDBDocumentationService.ListDocuments();
var guide = GameDBDocumentationService.ReadDocument("automation");
```

`ListDocuments()` returns IDs, titles, and package-relative paths. `ReadDocument(id)` only reads entries from that fixed catalog and returns the Markdown content in memory. Start with the `index` document.

## Path contract

Database and output paths must:

- begin with `Assets/`;
- resolve inside the current Unity project's `Assets` directory;
- use a `.json` data filename rather than a `.schema.json` filename;
- identify a database whose schema is stored beside it as `<name>.schema.json`.

Attempts to use rooted paths or `..` traversal outside `Assets` fail without writing.

## Read operations

```csharp
GameDBListResult ListDatabases(string searchDirectory = "Assets");
GameDBAutomationResult Load(string databasePath);
GameDBAutomationResult Inspect(string databasePath);
GameDBQueryResult Query(GameDBQueryRequest request);
GameDBAutomationResult Validate(string databasePath);
GameDBExportResult ExportJson(string databasePath);
```

`Load` is an alias for `Inspect`. Their results include a stable snapshot of tables, fields, rows, validation issues, and a SHA-256 revision token.

### Query API

`Query` is a read-only, transport-oriented projection API. It loads one database snapshot without saving files, importing assets, refreshing the Asset Database, or invoking saved callbacks.

```csharp
var page = GameDBAutomationService.Query(new GameDBQueryRequest
{
    DatabasePath = path,
    Tables = new List<GameDBQueryTableProjection>
    {
        new GameDBQueryTableProjection
        {
            TableName = "Items",
            RowKeys = new List<string> { "Sword", "Axe" },
            FieldNames = new List<string> { "Name", "Power" },
            Predicates = new List<GameDBQueryPredicate>
            {
                new GameDBQueryPredicate
                {
                    Kind = GameDBQueryPredicateKind.NumericRange,
                    FieldName = "Power",
                    Minimum = 10L,
                    Maximum = 20L
                }
            }
        }
    },
    Limit = 100
});
```

`GameDBQueryRequest.Tables` is required and must contain at least one uniquely named table projection. Names and selectors are exact and case-sensitive. An empty or `null` `RowKeys` list selects every row in that table; an empty or `null` `FieldNames` list projects every field. Blank, duplicate, or unknown table/row/field selectors return a structured `InvalidRequest` failure. Predicates may use fields that are not projected. Every returned row exposes its key separately through `GameDBQueryRowResult.Key`; `Values` contains only projected fields. Each projection still produces a table envelope and projected field metadata when no row from that table appears on the current page.

Predicates in one table projection are **AND-combined**. Query does not support OR, NOT, nested expressions, joins, arbitrary sorting, total counts, or per-table limits.

| Predicate       | Compatible field shape                                               | Payload and behavior                                                                                                                                |
| --------------- | -------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Equals`        | Non-collection scalar fields                                         | Uses `Value` and compares normalized wire values. `null` is accepted only for scalar `tableRef` and matches an unset reference.                     |
| `Contains`      | Scalar `string`, non-reference arrays, or dictionary keys            | Uses `Value`. Strings use case-sensitive ordinal substring matching; arrays use exact element equality; dictionaries test key presence, not values. |
| `NumericRange`  | Scalar `int` or `float`                                              | Uses inclusive `Minimum`/`Maximum`; at least one bound is required. `int` bounds must fit `Int32`; `float` bounds must be finite `Single` values.   |
| `ReferencesRow` | Scalar/array `tableRef`, or a dictionary whose values are `tableRef` | Uses a non-empty row-key `Value`; the target row must exist. It matches any referenced value in the scalar, array, or dictionary-value field.       |

`Equals` does not accept arrays or dictionaries. `Contains` validates array values against the element type; table-reference arrays use `ReferencesRow` instead. Enum values and enum dictionary keys must be declared member names. Color and vector values use their documented strings; Query parses and emits vector components with invariant culture. `Equals`, `Contains`, and `ReferencesRow` accept only `Value`; `NumericRange` accepts only `Minimum` and/or `Maximum`, and rejects a minimum greater than its maximum.

#### Ordering and global pagination

Tables and rows are evaluated in ordinal `(table name, row key)` order. Result table envelopes, rows, and projected field metadata use ordinal name order rather than request order. Dictionary entries are inserted by ordinal normalized key; callers that require map traversal order independent of runtime dictionary behavior should sort the keys with `StringComparer.Ordinal`.

`Limit` defaults to `100`, accepts `1` through `1000`, and applies globally across all projected tables. `ReturnedRowCount` is the page's total row count. When `HasMore` is true, pass `NextCursor` unchanged as the next request's `Cursor`. The opaque cursor is bound to the resolved database path, database revision, canonical projection/predicates, and an authenticated global matching-row offset. It may be reused with a different `Limit`, but a different database/query or altered offset returns `InvalidCursor` and any database revision change returns `StaleCursor`. Cursors survive Unity domain reloads but expire when the editor session ends. A cursor is a continuation token, not an authentication or authorization credential.

#### Query result values

`GameDBQueryRowResult.Values` contains JSON-compatible CLR shapes suitable for transport; it is not serialized JSON.

| Field shape             | Query value                                                         |
| ----------------------- | ------------------------------------------------------------------- |
| `int` / `float`         | boxed `long` / boxed `double`                                       |
| `bool`                  | boxed `bool`                                                        |
| `string`, `unityObject` | `string`                                                            |
| `tableRef`              | row-key `string`, or `null` when unset                              |
| enum, color, vector     | normalized `string`; vector formatting is invariant                 |
| array                   | `List<object>` preserving stored element order                      |
| dictionary              | `Dictionary<string, object>` with normalized keys and scalar values |

This differs from `GameDBSnapshot`, returned by `Inspect`/`Load`: snapshot row dictionaries are detached model values and may contain runtime CLR objects such as `GameDBLibrary.Color` or vector instances. Query normalizes the projected values into the transport-oriented shapes above.

#### Query failures

Expected failures are reported through `Success`, `GameDBQueryFailureKind`, `Message`, and `Errors`, not exceptions. Failure kinds are `InvalidRequest`, `InvalidPath`, `LoadFailed`, `RecoveryRequired`, `InvalidCursor`, `StaleCursor`, and `EvaluationFailed`. Each `GameDBQueryError` can identify its `Code`, `Message`, zero-based `ProjectionIndex`/`PredicateIndex`, `TableName`, and `FieldName`; indexes are `-1` when not applicable. `Revision` is populated after a database snapshot is loaded, and recovery failures populate `RecoveryArtifacts`. Failed queries return no projected tables or partial rows.

## Write operations

```csharp
GameDBAutomationResult Create(GameDBCreateRequest request);
GameDBAutomationResult Save(GameDBSaveRequest request);
GameDBBatchResult ApplyBatch(GameDBBatchRequest request);

GameDBAutomationResult AddTable(GameDBTableRequest request);
GameDBAutomationResult RenameTable(GameDBRenameRequest request);
GameDBAutomationResult DeleteTable(GameDBDeleteRequest request);

GameDBAutomationResult AddField(GameDBFieldRequest request);
GameDBAutomationResult ReplaceField(GameDBFieldRequest request);
GameDBAutomationResult RenameField(GameDBRenameRequest request);
GameDBAutomationResult DeleteField(GameDBDeleteRequest request);

GameDBAutomationResult AddRow(GameDBRowRequest request);
GameDBAutomationResult UpdateRow(GameDBRowRequest request);
GameDBAutomationResult SetValue(GameDBValueRequest request);
GameDBAutomationResult RenameRow(GameDBRenameRequest request);
GameDBAutomationResult DeleteRow(GameDBDeleteRequest request);

GameDBAutomationResult GenerateCSharp(GameDBGenerateRequest request);
```

Every operation is path-addressed and loads an isolated model. It does not depend on whichever database is selected in the GameDB editor window.

## Operation options

Single-operation requests use `GameDBOperationOptions`:

```csharp
new GameDBOperationOptions
{
    DryRun = true,
    AllowDestructive = false,
    ExpectedRevision = inspected.Snapshot.Revision
}
```

### Batch operations

`ApplyBatch` loads one database, applies an ordered list of mutation commands to a detached model, validates the complete prospective model, and saves at most once. The transaction is all-or-nothing: if any operation fails, no staged operation is committed. Creation, raw save, reads, JSON export, C# generation, querying, and CSV side effects are not batch operations.

```csharp
var inspected = GameDBAutomationService.Inspect(path);
var preview = GameDBAutomationService.ApplyBatch(new GameDBBatchRequest
{
    DatabasePath = path,
    Operations = new List<GameDBBatchOperation>
    {
        new GameDBBatchOperation
        {
            Kind = GameDBBatchOperationKind.AddTable,
            Table = new GameDBBatchTableOperation { TableName = "Items" }
        },
        new GameDBBatchOperation
        {
            Kind = GameDBBatchOperationKind.AddField,
            Field = new GameDBBatchFieldOperation
            {
                TableName = "Items",
                FieldName = "Damage",
                FieldType = FieldType.@int
            }
        },
        new GameDBBatchOperation
        {
            Kind = GameDBBatchOperationKind.AddRow,
            Row = new GameDBBatchRowOperation
            {
                TableName = "Items",
                RowKey = "Sword",
                Values = new Dictionary<string, object> { { "Damage", 14L } }
            }
        }
    },
    Options = new GameDBBatchOptions
    {
        DryRun = true,
        ExpectedRevision = inspected.Snapshot.Revision
    }
});
```

Each `GameDBBatchOperation` must set a non-`Unspecified` `Kind` and contain exactly one matching payload:

| Operation kinds                           | Payload property | Payload type                 |
| ----------------------------------------- | ---------------- | ---------------------------- |
| `AddTable`                                | `Table`          | `GameDBBatchTableOperation`  |
| `RenameTable`, `RenameField`, `RenameRow` | `Rename`         | `GameDBBatchRenameOperation` |
| `DeleteTable`, `DeleteField`, `DeleteRow` | `Delete`         | `GameDBBatchDeleteOperation` |
| `AddField`, `ReplaceField`                | `Field`          | `GameDBBatchFieldOperation`  |
| `AddRow`, `UpdateRow`                     | `Row`            | `GameDBBatchRowOperation`    |
| `SetValue`                                | `Value`          | `GameDBBatchValueOperation`  |

Batch authorization is an explicit operation-kind allowlist, not the single-operation `AllowDestructive` boolean. Add every destructive kind the batch may execute to `GameDBBatchOptions.AllowedDestructiveOperations`. The destructive kinds are `RenameTable`, `DeleteTable`, `ReplaceField`, `RenameField`, `DeleteField`, `RenameRow`, and `DeleteRow`. Authorization applies to dry runs as well.

`GameDBBatchResult.FailedOperationIndex` is the zero-based operation index for malformed payloads, authorization failures, and command failures. It is `-1` for whole-batch revision, validation, load, recovery, and commit failures. Snapshots/revisions returned by pre-commit transaction failures are prospective and were not committed. After a successful preview, rerun a non-dry batch against the latest inspected revision.

`CommitStatus` distinguishes `DryRun`, `Saved`, `NoChanges`, serialization/validation failure, disk conflict, persistence failure, unknown persistence state, and `PostSavePending`. Before replaying a failed batch, inspect `CommitStatus`, `FilesCommitted`, and `RecoveryArtifacts`. `PostSavePending` means the data/schema pair is committed while Unity imports or the saved callback still need retrying. `PersistenceStateUnknown` means publication is ambiguous and requires recovery rather than replay. Load-time `RecoveryRequired` failures also populate `RecoveryArtifacts`. `PostSaveErrors` contains import/callback failures.

### Dry runs

`DryRun = true` performs path, schema, value, reference, batch, and generation validation and returns the prospective snapshot without writing files, creating output directories, importing assets, invoking saved callbacks, or refreshing the Asset Database.

### Revision guards

Set `ExpectedRevision` to a revision returned by `Inspect`. The mutation fails if another editor or agent has changed the database since that inspection.

Revisions are computed from normalized schema and data JSON, so formatting-only differences do not create false conflicts.

### Destructive authorization

Set `AllowDestructive = true` for operations that can remove, rename, reset, or overwrite data:

- database overwrite and raw `Save` replacement;
- table, field, and row rename/delete;
- `ReplaceField`, which resets that field in every row to its new default;
- C# generation into an existing non-empty scope folder.

Authorization does not bypass path containment, type validation, revision checks, or reference integrity.

## Values and type arguments

Request values use JSON-compatible CLR shapes:

| GameDB type                         | Request value                                                                  |
| ----------------------------------- | ------------------------------------------------------------------------------ |
| `string`, `unityObject`, `tableRef` | `string` (`null` is accepted only for table references)                        |
| `int`                               | any integral numeric value within `Int32` range; JSON normally supplies `long` |
| `float`                             | a finite numeric value within `Single` range                                   |
| `bool`                              | `bool`                                                                         |
| `enum`                              | declared member name as `string`                                               |
| `color`                             | hex string such as `"#FF8000"`                                                 |
| `vector2`                           | comma-separated string such as `"1.5,2.5"`                                     |
| `vector3`                           | comma-separated string such as `"1,2,3"`                                       |
| `vector4`                           | comma-separated string such as `"1,2,3,4"`                                     |
| array                               | `List<object>` containing values of the scalar wire type                       |
| dictionary                          | `Dictionary<string, object>`                                                   |

All array elements and dictionary entries are validated. Dictionary fields cannot be arrays or contain nested dictionary values.

- Enum type arguments use a public project's reflection full type name.
- Table-reference type arguments use the target table name.
- Dictionaries use `GameDBDictionaryTypeDefinition` for their key/value definitions.

## Reference behavior

- Renaming a table updates direct and dictionary-value table-reference definitions.
- Renaming a row updates direct, array, and dictionary-value references to that row.
- Deleting a referenced table or row is rejected; update or remove the referencing fields/values first.
- Validation reports missing referenced tables and rows with table, field, and row coordinates.

## Coplay `execute_code` example

The exact wrapper depends on the Coplay tool version, but the executed editor C# can call GameDB directly:

```csharp
using GameDBEditorLibrary.Automation;
using UnityEngine;

var path = "Assets/Resources/GameDBs/basic.json";
var inspected = GameDBAutomationService.Inspect(path);
if (!inspected.Success)
{
    Debug.LogError(inspected.Message);
    return;
}

var result = GameDBAutomationService.SetValue(new GameDBValueRequest
{
    DatabasePath = path,
    TableName = "Items",
    RowKey = "Sword",
    FieldName = "Damage",
    Value = 14L,
    Options = new GameDBOperationOptions
    {
        ExpectedRevision = inspected.Snapshot.Revision,
        DryRun = true
    }
});

Debug.Log($"{result.Success}: {result.Message}");
```

Inspect `result.Issues` when an operation fails validation. After a successful dry run, repeat with `DryRun = false` and the latest revision.
