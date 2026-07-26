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

## Schema format contract

Every schema root requires a positive JSON integer `formatVersion`. The current and only supported value is `4`:

```json
{
  "formatVersion": 4,
  "tables": {},
  "scope": "Main",
  "localizationDB": false
}
```

GameDB validates this value before hydrating tables or data. Missing, null, string, fractional, non-positive, out-of-range, older, and newer values are rejected. These failures leave both database files unchanged and produce actionable load messages through Inspect/Validate/general mutations, `LoadFailed` through Batch/Query/CSV, or a failed raw Save result. Runtime loading of non-Resources Unity-object references is documented separately in the [optional Addressables guide](addressables.md); automation continues to exchange the transport-neutral `{guid,path}` value.

`GameDBSaveRequest.SchemaJson` must include `"formatVersion": 4`, including for new files and dry runs. Supplying unversioned schema JSON is an error. `ExportJson` returns canonical versioned schema JSON suitable for a later guarded Save.

## Read operations

```csharp
GameDBListResult ListDatabases(string searchDirectory = "Assets");
GameDBAutomationResult Load(string databasePath);
GameDBAutomationResult Inspect(string databasePath);
GameDBQueryResult Query(GameDBQueryRequest request);
GameDBCsvExportResult ExportCsv(GameDBCsvExportRequest request);
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

| Predicate       | Compatible field shape                                                         | Payload and behavior                                                                                                                                                                                  |
| --------------- | ------------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Equals`        | Non-collection scalar fields                                                   | Uses `Value` and compares normalized wire values. `null` is accepted only for scalar `tableRef`. A `unityObject` operand must be canonical; empty matches empty and non-empty values compare by GUID. |
| `Contains`      | Scalar `string`, non-reference arrays, Unity-object arrays, or dictionary keys | Uses `Value`. Strings use case-sensitive ordinal substring matching; ordinary arrays use exact element equality; Unity-object arrays use canonical GUID identity; dictionaries test key presence.     |
| `NumericRange`  | Scalar `int`, `long`, `float`, or `double`                                     | Uses inclusive `Minimum`/`Maximum`; at least one bound is required. Integer bounds stay exact in Int32/Int64 space; floating bounds must be finite Single/Double values.                              |
| `ReferencesRow` | Scalar/array `tableRef`, or a dictionary whose values are `tableRef`           | Uses a non-empty row-key `Value`; the target row must exist. It matches any referenced value in the scalar, array, or dictionary-value field.                                                         |

`Equals` does not accept arrays or dictionaries. `Contains` validates array values against the element type; table-reference arrays use `ReferencesRow` instead. Enum values and enum dictionary keys must be declared member names. Color and vector values use their documented strings; Query parses and emits vector components with invariant culture. `Equals`, `Contains`, and `ReferencesRow` accept only `Value`; `NumericRange` accepts only `Minimum` and/or `Maximum`, and rejects a minimum greater than its maximum.

#### Ordering and global pagination

Tables and rows are evaluated in ordinal `(table name, row key)` order. Result table envelopes, rows, and projected field metadata use ordinal name order rather than request order. Dictionary entries are inserted by ordinal normalized key; callers that require map traversal order independent of runtime dictionary behavior should sort the keys with `StringComparer.Ordinal`.

`Limit` defaults to `100`, accepts `1` through `1000`, and applies globally across all projected tables. `ReturnedRowCount` is the page's total row count. When `HasMore` is true, pass `NextCursor` unchanged as the next request's `Cursor`. The opaque cursor is bound to the resolved database path, database revision, canonical projection/predicates, and an authenticated global matching-row offset. It may be reused with a different `Limit`, but a different database/query or altered offset returns `InvalidCursor` and any database revision change returns `StaleCursor`. Cursors survive Unity domain reloads but expire when the editor session ends. A cursor is a continuation token, not an authentication or authorization credential.

#### Query result values

`GameDBQueryRowResult.Values` contains JSON-compatible CLR shapes suitable for transport; it is not serialized JSON.

| Field shape         | Query value                                                              |
| ------------------- | ------------------------------------------------------------------------ |
| `int` / `long`      | boxed `long`                                                             |
| `float` / `double`  | boxed `double`                                                           |
| `bool`              | boxed `bool`                                                             |
| `string`            | `string`                                                                 |
| `unityObject`       | `Dictionary<string, object>` with exact string `guid` and `path` entries |
| `tableRef`          | row-key `string`, or `null` when unset                                   |
| enum, color, vector | normalized `string`; vector formatting is invariant                      |
| array               | `List<object>` preserving stored element order                           |
| dictionary          | `Dictionary<string, object>` with normalized keys and scalar values      |

This differs from `GameDBSnapshot`, returned by `Inspect`/`Load`: snapshot row dictionaries are detached model values and may contain runtime CLR objects such as `GameDBLibrary.Color` or vector instances. Query normalizes the projected values into the transport-oriented shapes above.

#### Query failures

Expected failures are reported through `Success`, `GameDBQueryFailureKind`, `Message`, and `Errors`, not exceptions. Failure kinds are `InvalidRequest`, `InvalidPath`, `LoadFailed`, `RecoveryRequired`, `InvalidCursor`, `StaleCursor`, and `EvaluationFailed`. Each `GameDBQueryError` can identify its `Code`, `Message`, zero-based `ProjectionIndex`/`PredicateIndex`, `TableName`, and `FieldName`; indexes are `-1` when not applicable. `Revision` is populated after a database snapshot is loaded, and recovery failures populate `RecoveryArtifacts`. Failed queries return no projected tables or partial rows.

### CSV import and export

`ExportCsv` and `ImportCsv` are the supported spreadsheet interchange path. Each request addresses one existing table and returns or accepts CSV text in memory; the API does not read or write a separate `.csv` file.

```csharp
var exported = GameDBAutomationService.ExportCsv(new GameDBCsvExportRequest
{
    DatabasePath = path,
    TableName = "Items"
});

var imported = GameDBAutomationService.ImportCsv(new GameDBCsvImportRequest
{
    DatabasePath = path,
    TableName = "Items",
    CsvText = exported.CsvText,
    Mode = GameDBCsvImportMode.Replace,
    Options = new GameDBOperationOptions
    {
        AllowDestructive = true,
        ExpectedRevision = exported.Revision,
        DryRun = true
    }
});
```

#### Dialect

- The delimiter is comma and the quote character is `"`. Quoted cells use doubled quotes and may contain commas, quotes, CRLF, or LF, following RFC 4180.
- Export is deterministic: fields and rows use ordinal name/key order, records use CRLF, and cells are quoted only when needed. Import accepts CRLF or LF and one leading UTF-8 BOM character.
- The exact decoded first header is `__key`. A schema field named `__key` is rejected as a reserved collision.
- Remaining decoded headers are exact, case-sensitive field names. Empty, duplicate, and unknown columns are rejected. Whitespace is data and is never trimmed.
- Every record must have the header width. Blank records are rejected rather than skipped. Error coordinates distinguish 1-based logical record, physical line, and column numbers, including records with multiline cells.
- Tables containing any array or dictionary field are rejected for the entire operation. Collection cell encoding is not part of this dialect.

#### Formula-injection protection

Export protects every header, row key, and value cell before RFC quoting. If the decoded text starts with apostrophe, `=`, `+`, `-`, `@`, tab, CR, or LF, export prefixes one apostrophe. Import removes one apostrophe only when the following character is in that same set. This transform is reversible: stored `=SUM(A1:A2)` exports as `'=SUM(A1:A2)`, while stored `'=literal` exports as `''=literal` and both import without ambiguity. Negative numbers are protected for spreadsheet safety and parsed normally after decoding.

#### CSV scalar values

| GameDB shape                    | Export/import text                                                                                                        |
| ------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| string key                      | Exact non-empty, non-whitespace-only text except reserved `~not-set~`                                                     |
| enum key                        | Exact declared enum member name                                                                                           |
| `string`                        | Exact text; empty is valid                                                                                                |
| `unityObject`                   | Compact canonical JSON; unassigned is `{"guid":"","path":""}` and raw paths or malformed objects are rejected             |
| `bool`                          | `true` or `false`; import is case-insensitive                                                                             |
| `int`                           | Invariant signed decimal in `Int32` range; fractions, thousands separators, and overflow are rejected                     |
| `long`                          | Invariant signed decimal in the full `Int64` range; fractions, exponent syntax, separators, and overflow are rejected     |
| `float`                         | Invariant round-trip finite `Single`; decimal and scientific input are accepted, while NaN/infinity/overflow are rejected |
| `double`                        | Invariant finite `Double`; decimal/scientific input is accepted and canonical output uses `G17`                           |
| enum                            | Exact declared member name                                                                                                |
| `tableRef`                      | Referenced row key; empty means unset; literal `~not-set~` is rejected                                                    |
| `color`                         | Canonical hex string; import accepts 6 or 8 hex digits with optional `#` or `0x`                                          |
| `vector2`, `vector3`, `vector4` | Exact component count with invariant finite `Single` values; canonical output is comma-separated and therefore quoted     |

Empty numeric, boolean, enum, color, and vector cells are invalid. Double negative zero is normalized to positive zero. CSV preserves the full signed Int64 range; JavaScript consumers of JSON need lossless integer parsing outside ±9,007,199,254,740,991. Whole-document validation runs after every parsed row is staged, so forward table references within an import can succeed while missing references or references broken by replacement roll back the complete operation.

#### Replace and upsert

- `Upsert` updates only columns present in the CSV for existing keys, adds missing keys with current schema defaults for omitted columns, and leaves rows absent from the CSV unchanged.
- `Replace` replaces the table's complete row set while preserving its schema. It requires every scalar field column and `Options.AllowDestructive = true`, including during a dry run. Rows absent from the CSV are deleted.
- Duplicate CSV row keys are rejected. Enum-backed table keys must be declared members.
- Both modes parse and type-check the complete CSV, execute one `GameDBDocument` transaction, validate the complete prospective database, and save at most once. Any parse, command, revision, value, or reference failure leaves the loaded document and files unchanged.

`GameDBCsvExportResult` reports `Revision`, `CsvText`, `RowCount`, validation `Issues`, structured `Errors`, and recovery artifacts. `GameDBCsvImportResult` additionally reports the mode, dry-run flag, before/after revisions, prospective snapshot, imported row count, `GameDBCsvCommitStatus`, file/post-save/recovery facts, and `GameDBCsvFailureKind`. Each `GameDBCsvError` can identify `Code`, `Message`, 1-based `RecordNumber`/`LineNumber`/`ColumnNumber`, `ColumnName`, `RowKey`, and `FieldName`; coordinates are `-1` when an error is not tied to one CSV cell.

## Write operations

```csharp
GameDBAutomationResult Create(GameDBCreateRequest request);
GameDBAutomationResult Save(GameDBSaveRequest request);
GameDBBatchResult ApplyBatch(GameDBBatchRequest request);
GameDBCsvImportResult ImportCsv(GameDBCsvImportRequest request);

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

Every operation is path-addressed and loads an isolated model. It does not depend on whichever database is selected in the GameDB editor window. A real editor save normalizes scalar, array, and dictionary Unity-object paths from their GUIDs before persistence; read operations and dry runs remain pure and do not resolve assets or refresh paths.

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
- C# generation into an existing non-empty scope folder;
- CSV `Replace`, which replaces one table's complete row set.

Authorization does not bypass path containment, type validation, revision checks, or reference integrity.

## Values and type arguments

Request values use JSON-compatible CLR shapes:

| GameDB type          | Request value                                                                                           |
| -------------------- | ------------------------------------------------------------------------------------------------------- |
| `string`, `tableRef` | `string` (`null` is accepted only for table references)                                                 |
| `unityObject`        | exact `Dictionary<string, object>` with string `guid` and `path` entries                                |
| `int`                | any integral numeric value within `Int32` range; JSON normally supplies `long`                          |
| `long`               | an integral CLR value exactly representable as signed `Int64`; floating values and strings are rejected |
| `float`              | a finite numeric value within `Single` range                                                            |
| `double`             | a finite numeric value normalized to `System.Double`; negative zero becomes positive zero               |
| `bool`               | `bool`                                                                                                  |
| `enum`               | declared member name as `string`                                                                        |
| `color`              | hex string such as `"#FF8000"`                                                                          |
| `vector2`            | comma-separated string such as `"1.5,2.5"`                                                              |
| `vector3`            | comma-separated string such as `"1,2,3"`                                                                |
| `vector4`            | comma-separated string such as `"1,2,3,4"`                                                              |
| array                | `List<object>` containing values of the scalar wire type                                                |
| dictionary           | `Dictionary<string, object>`                                                                            |

All array elements and dictionary entries are validated. Numeric values use the same Int64/Double normalization in scalar, array, and dictionary positions. Unity-object values use the same exact canonical object in those positions. Dictionary fields cannot be arrays or contain nested dictionary values.

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
