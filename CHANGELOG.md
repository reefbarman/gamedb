# Changelog

<!-- markdownlint-disable MD024 -->

All notable changes to GameDB are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.0.0-preview.3] - 2026-07-28

### Added

- Added atomic editor-automation batches with ordered mutation operations, whole-batch dry runs and validation, revision guards, explicit destructive-operation allowlists, indexed failures, and structured commit outcomes.
- Added deterministic read-only editor-automation queries with table, row, and field projections; AND-combined typed predicates; ordinal global pagination; opaque revision- and query-bound cursors; structured failures; and JSON-compatible wire-shaped result values, including canonical Unity-object projections compared by GUID identity.
- Added transactional per-table CSV import/export with an RFC 4180 dialect, reserved `__key` column, invariant scalar and enum values, compact canonical JSON for Unity-object cells, reversible formula-injection protection, explicit replace/upsert modes, revision guards, structured cell coordinates, and whole-document rollback. Tables containing collection fields remain unsupported by CSV.
- Added generated Unity `LoadAsync` overloads backed by asynchronous Resources loading or a transport-neutral `IGameDBDataLoader`, with cancellation, per-database overlap rejection, atomic all-table publication, preservation of prior rows and localization state on failure, and post-commit `OnDBLoaded` notification.
- Added generated localization fallback chains with exact ordered validation and first-wins deduplication, sparse per-row resolution, empty-string translation preservation, immutable published chain metadata, `ResolvedLanguageVal`, actionable terminal misses, and language-aware core-only imports.
- Added an optional package-gated Addressables runtime assembly that can acquire GameDB JSON `TextAsset` data by an explicit Addressables key and load GUID-backed Unity-object references. Database loads copy JSON and release their temporary handle before atomic import; referenced-asset loads return deterministic disposable leases. Addressables remains absent from GameDB's package dependencies and generated code.
- Added exact signed Int64 (`long`) and finite Double (`double`) field types across editor authoring, runtime hydration, arrays, dictionary values, automation, precision-safe Query predicates/cursors, invariant scalar CSV (`Int64`/`G17`), and generated C# APIs; Double rejects non-finite values and normalizes negative zero.

### Changed

- Added an internal document, command, transaction, and conflict-aware persistence core shared by editor automation operations.
- Moved the supported data-only **Build GameDB** control into the main GameDB editor tab.
- Hardened C# generation: generated database, table, and row types are now `partial`; schema strings are `const`; enum keys are `static readonly`; generated names are validated before writing; and complete scope output is staged and replaced so removed tables cannot leave stale source files.
- Made runtime vector strings invariant-culture and reject non-finite components so editor automation and persisted data use deterministic wire values.
- Made CSV the sole supported spreadsheet interchange path.
- Advanced the current-only schema contract to format version `4` for exact Int64 and finite Double values; editor, runtime, document, and automation loads refuse missing, malformed, older, or newer formats before hydrating or rewriting database files.
- Replaced path-string Unity-object values with strict `{guid,path}` references. Real editor saves refresh paths from GUID identity for scalar, array, and dictionary values and accept any loadable main project asset under `Assets`; synchronous object projections remain Resources-only.
- Expanded generated Unity-object scalar and array APIs with value, GUID, and path projections in every output plus object projections in Unity-enabled output; dictionary values remain accessor objects.
- Made generated arrays, dictionaries, and table row maps recursively read-only and reference-stable within each atomic publication. Retained rows and table references now resolve against their original snapshot after reload, and runtime import validates direct, array, and dictionary row references before publication.
- Format version `4` table schemas now require the table-level `key` object; missing or malformed keys fail validation instead of falling back to string-key defaults.
- Made editor automation report structured commit status, committed paths, pending post-save work, persistence errors, and recovery artifacts consistently across create, save, and command mutations.

### Removed

- Removed the legacy server-management, upload, retrieval, and revision-promotion editor UI.
- Removed the legacy Google Sheets editor workflow, settings, Apps Script, and documentation after CSV replaced it.
- Removed the unsupported legacy remote/deployment runtime client APIs (`Remote`, `RequestUpdater`, `WebRequestHelper`, `ServerResponse`, `IDownloadHandler`, `RequestMethod`, `UnityForm`, `GameDBBase.ImportFromServer`, and `Utils.GetChecksum`) and the no-op `GameDBEditor.RegisterRevisionPromotionCallback`. Generated local/custom-loader and Resources load paths are unchanged.
- Removed the direct `com.unity.modules.imgui` and `com.unity.modules.unitywebrequest` package dependencies; UIElements remains the only direct Unity module dependency.

## [1.0.0-preview.1] - 2026-07-24

### Added

- Initial `com.reefbarman.gamedb` Unity Package Manager package for Unity 6000.5.4f1.
- Runtime JSON game-data APIs, editor authoring, schema-driven C# generation, table references, dictionaries, localization, remote-update client APIs, and optional Google Sheets interoperability under the MIT license.
- Transport-neutral editor automation API with path containment, dry runs, destructive-operation gates, revision checks, reference integrity, JSON export, and C# generation.
- Basic UPM sample and package documentation for editor, runtime, automation, and Google Sheets workflows.

### Changed

- Replaced the legacy JSON parser with Unity's maintained Newtonsoft JSON package.
- Modernized the Unity integration and package layout for Unity 6.5.

### Removed

- Legacy Free/Pro feature split, old project files, vendored promise/JSON sources, binary/encrypted build output, and the unshipped legacy deployment server workflow.

[Unreleased]: https://github.com/reefbarman/gamedb/compare/v1.0.0-preview.3...HEAD
[1.0.0-preview.3]: https://github.com/reefbarman/gamedb/compare/v1.0.0-preview.1...v1.0.0-preview.3
[1.0.0-preview.1]: https://github.com/reefbarman/gamedb/releases/tag/v1.0.0-preview.1
