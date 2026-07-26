# Editor authoring

Use the GameDB editor to create and maintain the schema and data files that are checked into your Unity project. For scripted or agent-driven changes, use the safer path-addressed API described in [Agent and editor automation](automation.md).

## Open or create a database

Open **Window → GameDB → Open Editor**.

- **Create GameDB** creates a data file such as `items.json` and an adjacent schema file named `items.schema.json`.
- **Add Existing GameDB** registers an existing data file. Its matching `.schema.json` file must be in the same directory.
- The **GameDB** dropdown lists registered project databases. Select one and click **Load GameDB**.

Keep both files under `Assets`. A `Resources` folder is recommended when generated runtime code will load the data with `Resources.Load`.

Every schema requires the root-level integer `"formatVersion": 3`. The editor checks the marker before loading tables or data. Missing, malformed, older, or newer versions are refused without rewriting either file.

Registered database paths and other editor preferences are project-scoped; they are not stored in the package installation.

## Scope and generated namespace

Set **GameDB Scope Name** before generating classes. The scope must be a valid C# identifier because generated types use the namespace `GameDB{ScopeName}`. For example, scope `Main` produces `GameDBMain`.

Use a stable scope. Changing it changes the generated namespace and therefore the code that consumes the database.

## Create tables and rows

A table key is either:

- **String** — enter a unique key when creating each row;
- **Enum** — choose one of the imported project enums, then choose an enum member for each row.

To add a table:

1. Enter a **Table Name**.
2. Select its **Key Type** and, for enum keys, its enum.
3. Click **Create Table**.
4. Expand the table and enter a key, then click **Create Key** to add a row.

Scope, table, and field names must be valid non-keyword C# identifiers. Row keys must produce distinct valid `Key<RowKey>` members after whitespace is removed. Generation also rejects generated accessor/type collisions and case-insensitive filename conflicts before writing any output. Avoid changing keys and schemas casually after game code depends on them, and regenerate classes after every schema change.

## Create and edit fields

Expand a table and click **Edit Table** to expose **Modify Schema**. Enter a field name, choose its type and options, then click **Create Field**.

Supported field types are:

| Type                                     | Editor and runtime behavior                                                                                                      |
| ---------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `String`, `Int`, `Float`, `Bool`         | Ordinary scalar values.                                                                                                          |
| `Color`, `Vector2`, `Vector3`, `Vector4` | Edited with Unity controls and exposed as Unity value types by generated accessors.                                              |
| `Enum`                                   | Uses a public enum imported from compiled project code.                                                                          |
| `Table Reference`                        | Selects a row in another table and generates both key and typed-row accessors.                                                   |
| `Unity Object`                           | Stores a canonical asset GUID and path for a main project asset beneath `Assets`.                                                |
| `Dictionary`                             | Uses string or enum keys and a supported scalar, enum, table-reference, or Unity value type. Dictionaries cannot also be arrays. |

All non-dictionary field types can be arrays. Click the small **E** button beside an array or dictionary value to open its editor. Change the size or add/remove entries, edit the values, then click **Save & Close**. **Close** discards edits made in that popup.

Schema edits initialize existing rows with the new field's default value. Deleting or renaming schema elements can invalidate generated code and dependent data; save and regenerate after the change. GameDB blocks deletion of a table while a direct table-reference field still targets it. The automation API performs broader reference validation and is recommended for complex or agent-driven refactors.

## Import project enums

GameDB only offers enums that have been imported into its project settings.

1. Let Unity compile the project enum. It must be public or nested public.
2. Open the **Configuration** tab.
3. Expand **Imported Enums**.
4. Click **+** or increase **Size**, then select the enum from the dropdown.
5. Return to the **GameDB** tab and use it as a table key, field type, or dictionary key/value type.

Removing an enum from this list does not rewrite existing schemas. Keep referenced enum types and member names stable, then regenerate classes after changes.

## Unity object fields

Unity object fields can reference textures, audio clips, prefabs, and other main Unity assets beneath `Assets`. The persisted value contains both the asset GUID and its current project path. Unity-enabled generated `ObjectVal`/`GetObject()` accessors load synchronously only when the referenced path is beneath exactly one case-sensitive `Resources` directory; load a valid non-Resources reference through the [optional Addressables integration](addressables.md).

A real **Save GameDB** operation resolves every non-empty scalar, array element, and dictionary value from its GUID and refreshes the stored path before writing. Moves and renames within, into, or out of `Resources` are normalized automatically. Missing GUIDs, subassets, scene objects, package assets, and unloadable assets block the save without changing the live database or either file. Dry runs and read-only operations do not refresh paths.

## Localization databases

Enable **Localization DB** before defining a localization schema. Localization tables use string fields and generated language-aware accessors. See [Runtime use](runtime.md) for the loading contract and generated API.

## Save and generate classes

- **Save GameDB** normalizes Unity-object references, validates the complete database, and writes both data and schema documents only when those steps succeed.
- **Generate Classes** writes strongly typed C# files under the selected `Assets` folder.

Generated files are derived output: do not hand-edit them. Generated database, table, and row classes are `partial`, so add game-specific members in separate files. Regenerate after changing the scope, tables, keys, fields, field types, enum definitions, or localization mode.

Generation validates the complete symbol and filename set before touching the destination. It writes a complete scope to staging and replaces the existing scope folder, preserving `.cs.meta` files for unchanged generated filenames and removing stale source and metadata for deleted tables. Any other hand-authored file inside the generated scope folder is also removed; keep extensions outside that folder. If generation targets an existing non-empty scope folder through `GameDBAutomationService`, it requires explicit destructive authorization.

## Build data-only JSON

The **GameDB** tab contains the supported **Build GameDB** foldout outside Play Mode.

1. Select and load a database in the **GameDB** tab.
2. Expand **Build GameDB** below the loader controls.
3. Choose a **Build Location** under `Assets`.
4. Click **Build**.

Build saves the source database first, then writes only its data JSON to the selected project folder and refreshes the Asset Database. It does not produce a schema, binary, compressed, or encrypted artifact.

The old upload, retrieve, and revision-promotion editor controls were removed because this package does not ship or support their deployment server.

## Project settings and recovery

GameDB stores editor state in:

```text
ProjectSettings/GameDBSettings.json
```

It includes registered database paths, imported enums, code-generation and build paths, and Google Sheets configuration. Review it before committing because Google Sheets URLs can be sensitive.

If the file is missing, GameDB creates it. If it cannot be parsed, GameDB logs a warning, restores defaults, and rewrites a valid settings file. You can close Unity and delete or repair this file to reset the GameDB editor workspace; database and schema files under `Assets` are unaffected.

## Play Mode editing

Schema editing and class generation are disabled in Play Mode, but data can be edited and reloaded into a registered runtime instance. See [Runtime use](runtime.md#play-mode-editing-and-hot-reload) for the complete workflow and the `OnDBLoaded` recaching contract.
