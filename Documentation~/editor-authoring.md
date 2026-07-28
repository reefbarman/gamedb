# Editor authoring

Use the GameDB editor to create and maintain the schema and data files that are checked into your Unity project. For scripted or agent-driven changes, use the safer path-addressed API described in [Agent and editor automation](automation.md).

## Open or create a database

Open **Window → GameDB → Open Editor**.

- **Create** creates a data file such as `items.json` and an adjacent schema file named `items.schema.json`, then opens it in a document tab.
- **Open** opens an existing data file. Its matching `.schema.json` file must be in the same directory.
- Use **Settings → Register Database** to retain a database in project settings without opening it.
- Multiple databases can be open at once. Use document tabs to switch, reorder, or close them; each tab retains its selected table, row, search text, and sort state.

Keep both files under `Assets`. A `Resources` folder is recommended when generated runtime code will load the data with `Resources.Load`.

Every schema requires the root-level integer `"formatVersion": 4`. The editor checks the marker before loading tables or data. Missing, malformed, older, or newer versions are refused without rewriting either file.

Registered database paths and other editor preferences are project-scoped; they are not stored in the package installation.

## Scope and generated namespace

Set **GameDB Scope Name** before generating classes. The scope must be a valid C# identifier because generated types use the namespace `GameDB{ScopeName}`. For example, scope `Main` produces `GameDBMain`.

Use a stable scope. Changing it changes the generated namespace and therefore the code that consumes the database.

## Create tables and rows

A table key is either:

- **String** — enter a unique key when creating each row;
- **Enum** — choose one of the imported project enums, then choose an enum member for each row.

To add a table:

1. In the inspector's **Table** section, enter its **Name**.
2. Select its **Key type** and, for enum keys, enter the fully qualified imported enum type.
3. Click **Add**.
4. Select the table, enter a key in the **Row** section, then click **Add**.

The center grid is virtualized and supports search and stable column sorting. Selection follows the row key across filtering and sorting.

Scope, table, and field names must be valid non-keyword C# identifiers. Row keys must produce distinct valid `Key<RowKey>` members after whitespace is removed. Generation also rejects generated accessor/type collisions and case-insensitive filename conflicts before writing any output. Avoid changing keys and schemas casually after game code depends on them, and regenerate classes after every schema change.

## Create and edit fields

Select a table, then use the inspector's **Fields** section. Enter a field name, choose its type and type argument when required, then click **Add**. Select an existing field to rename, replace its type, or delete it.

Supported field types are:

| Type                                     | Editor and runtime behavior                                                                                                      |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `String`, `Int`, `Float`, `Bool`         | Ordinary scalar values; `Int` is signed 32-bit and `Float` is finite Single precision.                                           |
| `Long`, `Double`                         | Signed 64-bit integers and finite Double-precision values.                                                                       |
| `Color`, `Vector2`, `Vector3`, `Vector4` | Edited with Unity controls and exposed as Unity value types by generated accessors.                                              |
| `Enum`                                   | Uses a public enum imported from compiled project code.                                                                          |
| `Table Reference`                        | Selects a row in another table and generates both key and typed-row accessors.                                                   |
| `Unity Object`                           | Stores a canonical asset GUID and path for a main project asset beneath `Assets`.                                                |
| `Dictionary`                             | Uses string or enum keys and a supported scalar, enum, table-reference, or Unity value type. Dictionaries cannot also be arrays. |

All non-dictionary field types can be arrays. `Long` accepts the complete signed Int64 range without floating-point coercion. `Double` rejects NaN and infinities and normalizes negative zero to positive zero. Arrays and dictionary values use the same rules as scalar fields. Activate an array or dictionary cell to open the collection editor. Add or remove entries, edit values with the same typed controls used by scalar cells, then click **Apply**. **Cancel** discards modal edits; **Reload Current** restores the latest document value.

Schema edits initialize existing rows with the new field's default value. Deleting or renaming schema elements can invalidate generated code and dependent data; save and regenerate after the change. GameDB blocks deletion of a table while a direct table-reference field still targets it. The automation API performs broader reference validation and is recommended for complex or agent-driven refactors.

## Import project enums

GameDB only offers enums that have been imported into its project settings.

1. Let Unity compile the project enum. It must be public or nested public.
2. Open **Settings**.
3. Enable the enum under **Imported enum types**. Previously configured but currently unresolved enum names remain visible so they can be retained or removed deliberately.
4. Click **Save Settings**.
5. Use the enum as a table key, field type, or dictionary key/value type.

Removing an enum from this list does not rewrite existing schemas. Keep referenced enum types and member names stable, then regenerate classes after changes.

## Unity object fields

Unity object fields can reference textures, audio clips, prefabs, and other main Unity assets beneath `Assets`. The persisted value contains both the asset GUID and its current project path. Unity-enabled generated `ObjectVal`/`GetObject()` accessors load synchronously only when the referenced path is beneath exactly one case-sensitive `Resources` directory; load a valid non-Resources reference through the [optional Addressables integration](addressables.md).

A real **Save GameDB** operation resolves every non-empty scalar, array element, and dictionary value from its GUID and refreshes the stored path before writing. Moves and renames within, into, or out of `Resources` are normalized automatically. Missing GUIDs, subassets, scene objects, package assets, and unloadable assets block the save without changing the live database or either file. Dry runs and read-only operations do not refresh paths.

## Localization databases

Enable **Localization DB** before defining a localization schema. Each scalar string field is an exact, case-sensitive language identifier and must also be a valid generated C# identifier; the generated known-language set is the union across all localization tables, so tables may intentionally support different subsets. Arrays and non-string fields are rejected during generation. The current editor writes every declared field for each row, using an empty string as authored content, so editor-authored per-row fallback normally comes from a language absent from that table's schema. Sparse per-row JSON from external tooling is supported at runtime but does not round-trip losslessly through Play Mode editor loading. See [Runtime use](runtime.md#localization-databases) for fallback ordering, loading, metadata, and row accessor behavior.

## Save and generate classes

- **Save** normalizes Unity-object references, validates the complete database, and writes both data and schema documents only when those steps succeed.
- Configure **Export path** in **Settings**, then click **Generate** in the document toolbar to write strongly typed C# files under that `Assets` folder.

Generated files are derived output: do not hand-edit them. Generated database, table, and row classes are `partial`, so add game-specific members in separate files. Regenerate after changing the scope, tables, keys, fields, field types, enum definitions, or localization mode.

Generation validates the complete symbol and filename set before touching the destination. It writes a complete scope to staging and replaces the existing scope folder, preserving `.cs.meta` files for unchanged generated filenames and removing stale source and metadata for deleted tables. Any other hand-authored file inside the generated scope folder is also removed; keep extensions outside that folder. When the editor targets an existing non-empty scope folder it asks for explicit replacement confirmation. `GameDBAutomationService` requires `Options.AllowDestructive` for the same operation.

## Build data-only JSON

1. Open **Settings** and configure a **Build path** under `Assets`.
2. Open the database document.
3. Click **Build** in the document toolbar.

Build saves the source database first, then writes only its data JSON to the selected project folder and refreshes the Asset Database. It does not produce a schema, binary, compressed, or encrypted artifact. Build refuses to overwrite an existing output file; choose an empty output directory or remove the obsolete artifact deliberately.

The old upload, retrieve, and revision-promotion editor controls were removed because this package does not ship or support their deployment server.

## Project settings and recovery

GameDB stores editor state in:

```text
ProjectSettings/GameDBSettings.json
```

It includes registered database paths, imported enums, and code-generation and build paths.

If the file is missing, GameDB uses stable defaults and writes it when settings first change. If it cannot be parsed, the settings panel reports the error and preserves the malformed file instead of overwriting it. Close Unity and repair or delete the file to reset project settings; database and schema files under `Assets` are unaffected.

## Play Mode editing

Schema editing, Save, Generate, and Build are disabled in Play Mode. Select a registered **Runtime GameDB**, click **Load Runtime Data**, edit data-only cells/rows/collections, then click **Reload In-Game** to publish the draft to that same runtime target. See [Runtime use](runtime.md#play-mode-editing-and-hot-reload) for the complete workflow and the `OnDBLoaded` recaching contract.
