# Changelog

<!-- markdownlint-disable MD024 -->

All notable changes to GameDB are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Added atomic editor-automation batches with ordered mutation operations, whole-batch dry runs and validation, revision guards, explicit destructive-operation allowlists, indexed failures, and structured commit outcomes.
- Added deterministic read-only editor-automation queries with table, row, and field projections; AND-combined typed predicates; ordinal global pagination; opaque revision- and query-bound cursors; structured failures; and JSON-compatible wire-shaped result values.

### Changed

- Added an internal document, command, transaction, and conflict-aware persistence core shared by editor automation operations.
- Moved the supported data-only **Build GameDB** control into the main GameDB editor tab.
- Hardened C# generation: generated database, table, and row types are now `partial`; schema strings are `const`; enum keys are `static readonly`; generated names are validated before writing; and complete scope output is staged and replaced so removed tables cannot leave stale source files.
- Made runtime vector strings invariant-culture and reject non-finite components so editor automation and persisted data use deterministic wire values.
- Retained the networking path required by optional Google Sheets interoperability.

Regenerate all GameDB C# classes after updating to `1.0.0-preview.2`; generated schema members and output replacement behavior are intentionally breaking changes from `1.0.0-preview.1`.

### Deprecated

- Marked the unsupported legacy remote/deployment runtime APIs obsolete, with removal planned for GameDB `1.0.0`.

### Removed

- Removed the legacy server-management, upload, retrieval, and revision-promotion editor UI.

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

[Unreleased]: https://github.com/reefbarman/gamedb/compare/v1.0.0-preview.1...HEAD
[1.0.0-preview.1]: https://github.com/reefbarman/gamedb/releases/tag/v1.0.0-preview.1
