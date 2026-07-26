# Optional Google Sheets interoperability

GameDB retains the original Google Sheets import/export protocol only as a legacy interoperability option. The supported spreadsheet interchange path is the transport-neutral [`ExportCsv`/`ImportCsv` API](automation.md#csv-import-and-export), which needs no web app, sends no data to a hosted endpoint, validates revisions and references transactionally, and reports structured cell errors.

## Security warning

The bundled script's `checkSecurity()` function is intentionally the unchanged legacy placeholder and performs no authentication. The old workflow deployed the script as an anonymously accessible Google Apps Script web app.

Before using it:

- add authentication or an unguessable, rotatable shared secret;
- restrict the deployment as much as your Google Workspace configuration permits;
- do not publish the web-app URL;
- use a dedicated spreadsheet containing no sensitive information;
- review the script and your Google Apps Script execution permissions.

GameDB stores the web-app URL and spreadsheet ID in `ProjectSettings/GameDBSettings.json`. Treat that file as project metadata that may contain sensitive deployment information and review it before committing.

GameDB sends the complete schema and data documents to this endpoint. Do not use the unmodified script for confidential production data.

## Script

The deployable source is:

```text
Documentation~/GoogleSheets/GoogleSheetWebApp.gs
```

It is the coherent monolithic output of the original `Main.gs`, `Import.gs`, and `Export.gs` sources. The old test harness and concatenation build script are not part of the maintained package.

## Setup

1. Create a Google Apps Script project.
2. Copy `GoogleSheetWebApp.gs` into the project.
3. Implement authentication in `checkSecurity(params)` before deployment.
4. Deploy it as a web app using access settings appropriate for your environment.
5. Create or choose a Google Spreadsheet and copy its spreadsheet ID from the URL.
6. In Unity, open **Window → GameDB → Open Editor**, load a database, and click **Google Sheets**.
7. Enter the web-app URL and spreadsheet ID.

## Behavior and limitations

- Unity-to-Sheets import recreates each matching GameDB worksheet and can destroy manual formatting or edits in those sheets.
- Each table is represented by a sheet named `GameDB-<scope>-<table>`.
- The schema is stored in a hidden column and is required for export back to Unity.
- The script accepts only schema format version `2`.
- The protocol cannot represent GUID-backed Unity-object values safely. The script rejects databases containing direct Unity-object fields or dictionaries with Unity-object values; use CSV for those databases.
- The protocol predates the modern dictionary field support. Treat dictionary interoperability as unsupported unless you extend and test the script.
- Export a database to a new spreadsheet before attempting spreadsheet-to-Unity import so the expected layout and validation ranges exist.
- Use a separate spreadsheet per GameDB scope to reduce accidental collisions.

For new editor tooling, agent automation, or manual spreadsheet interchange, use `GameDBAutomationService.ExportCsv` and `ImportCsv`. Use this retained `.gs` workflow only when direct Google Sheets integration is worth maintaining and securing separately.
