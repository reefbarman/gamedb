# GameDB

GameDB is an open-source, schema-driven game-data editor and runtime library for Unity. Author tables in the Unity Editor, generate strongly typed C# accessors, load JSON at runtime, and automate local database changes through a public editor API.

## Requirements

- Unity `6000.5.4f1` or newer in the Unity 6.5 line
- Git when installing directly from GitHub

The package uses Unity's Newtonsoft JSON package and includes its required Unity module dependencies in `package.json`.

## Install with Unity Package Manager

In Unity, open **Window → Package Management → Package Manager**, select **Install package from git URL**, and enter:

```text
https://github.com/reefbarman/gamedb.git
```

For reproducible builds, install a release tag once published:

```text
https://github.com/reefbarman/gamedb.git#v1.0.0-preview.1
```

You can also add the dependency directly to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.reefbarman.gamedb": "https://github.com/reefbarman/gamedb.git#v1.0.0-preview.1"
  }
}
```

## Quick start

1. Open **Window → GameDB → Open Editor**.
2. Click **Create GameDB** and save the database under your project's `Assets` directory. GameDB creates a data `.json` file and a matching `.schema.json` file.
3. Set a valid C# **Scope Name**.
4. Create tables, fields, and rows, then click **Save GameDB**.
5. Click **Generate Classes** and select an output folder under `Assets`.
6. Put the database JSON under a `Resources` folder if you want to use the generated `Resources.Load` helper.

Each generated namespace is named `GameDB{ScopeName}`. For a scope named `Basic`, runtime loading looks like this:

```csharp
using GameDBBasic;
using UnityEngine;

public sealed class LoadGameData : MonoBehaviour
{
    private void Start()
    {
        var gameDB = new GameDB("Main");
        var error = gameDB.Load("GameDBs/basic");

        if (error != null)
        {
            Debug.LogException(error);
            return;
        }

        var sword = gameDB.ItemsTable.GetByKey(ItemsSchema.KeySword);
        Debug.Log($"{sword.DisplayNameVal}: {sword.DamageVal}");
    }
}
```

The generated load path is relative to a `Resources` folder and omits the `.json` extension.

## Schema format version

Every `.schema.json` file requires a root-level integer `"formatVersion": 1`. GameDB writes this marker when it creates or saves a database and validates it before loading any schema, data, editor state, or automation operation.

Unversioned, malformed, and newer schema formats are refused without rewriting either database file. A newer format means the project must be opened with a newer GameDB package. All schemas authored for this release must use format version `1`.

## Supported data

GameDB supports:

- strings, 32-bit integers, floats, and booleans
- colors and 2D/3D/4D vectors
- Unity object resource paths
- project enums
- references to rows in another table
- arrays of non-dictionary field types
- dictionaries with string or enum keys

Table references and schema changes are validated before the automation API saves them. Generated code should be regenerated after schema changes.

## Documentation

The package includes maintained Markdown documentation for:

- [editor authoring](Documentation~/editor-authoring.md), including tables, fields, enums, arrays, dictionaries, settings, and data-only builds;
- [runtime use and hot reload](Documentation~/runtime.md), including generated code, Play Mode editing, and localization;
- the [supported API reference](Documentation~/api-reference.md);
- [agent and editor automation](Documentation~/automation.md), including transactional CSV import/export;
- [legacy optional Google Sheets interoperability](Documentation~/google-sheets.md).

Start at [`Documentation~/index.md`](Documentation~/index.md). The supported workflows from the former GameDB 1.6 site have been rewritten for Unity 6.5; retired Free/Pro, binary/encrypted, and unshipped deployment-server workflows are explicitly excluded.

## Basic sample

Import **Basic GameDB** from the Package Manager's **Samples** tab. The sample includes a small `Categories` and `Items` database under `Resources/GameDBs` and instructions for loading it in the GameDB editor and generating its runtime classes.

## Agent and editor automation

The editor assembly exposes a transport-neutral API in:

```csharp
GameDBEditorLibrary.Automation.GameDBAutomationService
```

Agents can call this API through Coplay's existing Unity MCP `execute_code` capability; GameDB does not require a custom MCP server or a hosted service.

Bundled documentation is also agent-readable through stable IDs:

```csharp
var catalog = GameDBDocumentationService.ListDocuments();
var guide = GameDBDocumentationService.ReadDocument("index");
UnityEngine.Debug.Log(guide.Content);
```

Example inspection:

```csharp
using GameDBEditorLibrary.Automation;
using UnityEngine;

var result = GameDBAutomationService.Inspect("Assets/Resources/GameDBs/basic.json");
Debug.Log($"Success: {result.Success}, revision: {result.Snapshot?.Revision}");
```

Example guarded mutation:

```csharp
using GameDBEditorLibrary.Automation;

var inspected = GameDBAutomationService.Inspect("Assets/Resources/GameDBs/basic.json");
var result = GameDBAutomationService.AddRow(new GameDBRowRequest
{
    DatabasePath = "Assets/Resources/GameDBs/basic.json",
    TableName = "Items",
    RowKey = "Axe",
    Values = new System.Collections.Generic.Dictionary<string, object>
    {
        { "DisplayName", "Iron Axe" },
        { "Damage", 16L },
        { "Category", "Weapons" }
    },
    Options = new GameDBOperationOptions
    {
        ExpectedRevision = inspected.Snapshot.Revision,
        DryRun = true
    }
});
```

Use `DryRun` to validate a prospective change without writing. Renames, deletes, schema replacement, raw saves, database overwrite, generated-file overwrite, and CSV table replacement require `AllowDestructive = true`. See [`Documentation~/automation.md`](Documentation~/automation.md) for the complete contract.

## CSV and Google Sheets

Use `GameDBAutomationService.ExportCsv` and `ImportCsv` for supported one-table spreadsheet interchange. The RFC 4180 dialect uses a reserved `__key` column, invariant scalar and enum values, reversible formula-injection protection, explicit `Replace`/`Upsert` modes, revision guards, and transactional validation. Arrays and dictionaries are intentionally deferred. See the [CSV contract](Documentation~/automation.md#csv-import-and-export).

A legacy-compatible Google Apps Script remains as an **optional** interoperability tool. It requires deployment as a web app and its original protocol has no authentication. Do not expose it publicly without adding your own authorization checks. See [`Documentation~/google-sheets.md`](Documentation~/google-sheets.md).

## Legacy-compatible remote client APIs

The old editor upload, retrieval, and revision-promotion controls were removed. The runtime still contains warning-only obsolete remote-update client APIs for source compatibility, but this preview does not provide, host, or validate the old Go/AWS deployment server. Do not use this surface for new production work; it is planned for removal in GameDB `1.0.0`.

## Development

`TestProject~/` is the Unity 6.5 development project. The package is referenced locally from the repository root. EditMode tests live in `Tests/EditMode`.

Run the package-integrity checks from the repository root:

```bash
python3 Tools~/validate_package.py
git diff --check
```

Run the EditMode suite with a locally activated Unity installation. Replace `UNITY_PATH` if Unity is installed elsewhere:

```bash
UNITY_PATH="/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/MacOS/Unity"
"$UNITY_PATH" \
  -batchmode \
  -nographics \
  -projectPath "$PWD/TestProject~" \
  -runTests \
  -testPlatform EditMode \
  -assemblyNames GameDBLibrary.Tests \
  -testResults "$PWD/TestProject~/TestResults/editmode.xml" \
  -logFile "$PWD/TestProject~/Logs/editmode.log"
```

Do not add `-quit`; Unity's test runner exits after writing the results. GitHub Actions and hosted Unity license secrets are not required for package installation or this local workflow.

## License

GameDB is licensed under the [MIT License](LICENSE.md). Third-party notices are listed in [Third Party Notices.md](Third%20Party%20Notices.md).
