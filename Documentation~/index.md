# GameDB documentation

GameDB is a schema-driven game-data editor, runtime library, C# generator, and editor automation API for Unity 6.5. Schema format 4 includes exact signed Int64 and finite Double fields across scalar, array, dictionary, Query, CSV, and generated-code workflows.

## Start here

- [Repository and package overview](../README.md)
- [Editor authoring](editor-authoring.md)
- [Runtime use and hot reload](runtime.md)
- [Optional Addressables integration](addressables.md)
- [Supported API reference](api-reference.md)
- [Agent and editor automation](automation.md), including transactional CSV import/export
- [Basic sample](../Samples~/Basic/README.md)
- [Changelog](../CHANGELOG.md)

## Editor workflow

1. Open **Window → GameDB → Open Editor**.
2. Create a database or add an existing `.json` file with its adjacent `.schema.json` file.
3. Set a valid C# scope name.
4. Create tables, fields, and rows, then save. Unity-object values use GUID-backed references and are path-normalized on real saves.
5. Generate C# classes under `Assets`.
6. Put database data and synchronously loaded Unity assets under `Resources`; use the [optional Addressables adapter](addressables.md) for asynchronous non-Resources asset loading.

Import **Basic GameDB** from the Package Manager's **Samples** tab for a small working database.

## Agent access

Agents executing editor C# can retrieve these documents without locating Unity's PackageCache manually:

```csharp
using GameDBEditorLibrary.Automation;
using UnityEngine;

var catalog = GameDBDocumentationService.ListDocuments();
foreach (var document in catalog.Documents)
{
    Debug.Log($"{document.Id}: {document.Title}");
}

var automation = GameDBDocumentationService.ReadDocument("automation");
Debug.Log(automation.Content);
```

Stable document IDs are returned by `ListDocuments()`. `ReadDocument()` only accepts IDs from that catalog and cannot read arbitrary project or package files.

## Legacy documentation coverage

The supported workflows from the former GameDB 1.6 documentation have been rewritten for this Unity 6.5 package rather than copied verbatim. The editor, generated runtime API, Play Mode hot reload, localization, settings, CSV interchange, and supported public APIs are covered by the guides above.

The old Free/Pro split, dedicated prefab type, binary/encrypted output, and Go/AWS deployment-server instructions are intentionally not carried forward. The editor server controls were removed; warning-only obsolete runtime remote clients remain temporarily for source compatibility, but this package does not ship or support the old hosted backend. The **Build GameDB** data-only JSON command remains supported in the GameDB tab and is documented in [Editor authoring](editor-authoring.md#build-data-only-json).
