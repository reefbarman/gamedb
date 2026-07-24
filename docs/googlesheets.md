Google Sheets Import/Export Guide {#googlesheetspage}
==========
(Pro version only)

[TOC]

# Overview {#googlesheetsoverview}
The GameDB (Pro) plugin supports importing and exporting from/to google sheets via a self-hosted web app.

This functionality allows mainly for designers who may be used to this workflow to migrate to using the tool or allow analysis of the data after it has been exported, or can allow for migrating data that may already be stored in a spreadsheet into Unity and the plugin,

This is provided as a value added component and is not a recommended way of working with the plugin long term as certain future features may not be supported via Google Sheets Export/Import.

# Setup {#setup}
Found in _Assets/Plugins/GameDBLibrary/GoogleSheets_ is a Google App Script file called _GoogleSheetWebApp.gs_

This file will be imported into a Google App Script project that can be created by visiting <https://www.google.com/script/start/> and clicking on the _Start Scripting_ button. Copy the contents of the _GoogleSheetWebApp.gs_ file into the main window (see screenshot below). Then give the project a name and save it from the file menu.

![Google App Script Main Window](Images/GoogleScriptsScreenshot.png)

Before the script is usable it needs to be published as a wep app this can be done by select the _Publish > Deploy as a web app..._ option from the menu. You will be asked to select who the web app operates under and who has access. You have to have it execute as a particular user as the plugin can't authenticate otherwise and you have to allow access to _Everyone, even anonymous_ so be careful not to share your web app URL which you will get in the next dialog after clicking deploy.

You can then use the URL you obtained to enter into the plugin in the _Web App Url:_ field when you click on the _Google Sheets_ button.

# Usage {#usage}
With the web app setup you can now export data from Unity into a spreadsheet or import it from one.

To specify the spreadsheet to export to/import from, create a spreadsheet in Google Drive or Google Sheets directly then take note of the spreadsheet id found by getting the long alpha-numeric string similar to the one hightlighted in this example: https:/docs.google.com/spreadsheets/d/**e3jmJoPNlsumfsYjW7sGOv81taMDe3jmJoPNlsumfsYjW7sGOv81taMD**/edit

This is then entered in the _Sheet ID:_ field of the dialog that is shown after clicking on the _Google Sheets_ button.

# Notes {#notes}
* To get the best results with importing make sure to export your table first, this will create the required sheet in your document and give you an idea of the format required for import
* A Sheet will be created for each table in your gameDB
* It is recommended to use a new sheet id for each gameDB you have
