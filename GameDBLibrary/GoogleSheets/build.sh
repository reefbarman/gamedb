#!/bin/bash

mkdir -p ../../Assets/Plugins/GameDBLibrary/GoogleSheets/
cat Main.gs Import.gs Export.gs > ../../Assets/Plugins/GameDBLibrary/GoogleSheets/GoogleSheetWebApp.gs

echo "Done!"