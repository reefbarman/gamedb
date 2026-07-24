function ImportJSON(sheetID, schemaJSON, dataJSON)
{
  try
  {
    var spreadSheet = SpreadsheetApp.openById(sheetID);

    var schema = JSON.parse(schemaJSON);
    var data = JSON.parse(dataJSON);

    var sheets = {};

    for (var tableName in schema.tables)
    {
      sheets[tableName] = { sheet: getSheet(spreadSheet, schema.scope, tableName) };
    }

    importData(sheets, schema.tables, data.tables);
    buildDataValidation(spreadSheet, sheets, schema, schema.tables, data.tables);

    for (var tableName in sheets)
    {
      var sheet = sheets[tableName].sheet;

      var range = sheet.getRange(1, sheet.getLastColumn() + 10, 1);
      range.setValue(schemaJSON);
      sheet.hideColumn(range);
    }

    return true;
  }
  catch (e)
  {
    return { error: { message: e.message } }
  }
}

function importData(sheets, tableSchemas, tablesData)
{
  for (var tableName in tableSchemas)
  {
    var sheet = sheets[tableName].sheet;

    var tableSchema = tableSchemas[tableName].fields;

    var headerRow = [];
    var headers = [];

    var validValues = {};

    for (var field in tableSchema)
    {
      headerRow.push(field + " (" + tableSchema[field].type + (tableSchema[field].isArray ? "[ ]" : "") + ")" );
      headers.push(field);
    }

    headerRow.sort(function(a, b){
      if(a < b) return -1;
      if(a > b) return 1;
      return 0;
    });

    headers.sort(function(a, b){
      if(a < b) return -1;
      if(a > b) return 1;
      return 0;
    });

    headerRow.splice(0, 0, "Key");

    var rows = [];

    rows.push(headerRow);

    for (var key in tablesData[tableName])
    {
      var row = tablesData[tableName][key];

      var rowData = [key];

      for (var i in headers)
      {
        var field = headers[i];

        var fieldSchema = tableSchema[field];

        var fieldData = null;

        switch(fieldSchema.type)
        {
          case "vector2":
          case "vector3":
          case "vector4":
            if (row[field] instanceof Array)
            {
              if (row[field].length > 0)
              {
                fieldData = "{" + row[field].join("},{") + "}";
              }
            }
            else
            {
              fieldData = row[field];
            }
            break;
          default:
            fieldData = row[field] instanceof Array ? row[field].join(",") : row[field];
            break;
        }

        rowData.push(fieldData);
      }

      rows.push(rowData);
    }

    var range = sheet.getRange(1,1,rows.length, headerRow.length);
    range.setValues(rows).setNumberFormat('@STRING@');
  }
}

function buildDataValidation(spreadSheet, sheets, schema, tableSchemas, tablesData)
{
  for (var tableName in tableSchemas)
  {
    var sheet = sheets[tableName].sheet;

    var tableSchema = tableSchemas[tableName].fields;

    var headerRow = [];

    var validValues = {};

    for (var field in tableSchema)
    {
      headerRow.push(field);
    }

    headerRow.sort(function(a, b){
      if(a < b) return -1;
      if(a > b) return 1;
      return 0;
    });

    for (var i in headerRow)
    {
      var field = headerRow[i];

      if (!tableSchema[field].isArray)
      {
        switch(tableSchema[field].type)
        {
          case "enum":
            var enumType = tableSchema[field].typeArg;

            if (!(enumType in validValues))
            {
              validValues = createDataRange(sheet, field, tableSchema, validValues, enumType);
            }

            var columnRange = sheet.getRange(2, parseInt(i) + 2, Object.keys(tablesData[tableName]).length, 1);
            columnRange.setDataValidation(validValues[enumType].rule);
            break;
          case "tableRef":
            var tableRefName =  tableSchema[field].typeArg;
            var rule = SpreadsheetApp.newDataValidation().requireValueInRange(spreadSheet.getRange(getSheetName(schema.scope, tableRefName) + "!A2:A" + sheets[tableRefName].sheet.getMaxRows())).build();
            var columnRange = sheet.getRange(2, parseInt(i) + 2, Object.keys(tablesData[tableName]).length, 1);
            columnRange.setDataValidation(rule);
            break;
        }
      }
    }
  }
}

function createDataRange(sheet, field, tableSchema, validValues, valueName)
{
  var startColumn = (Object.keys(tableSchema).length + 4) + Object.keys(validValues).length + 10;
  var numberOfRows = tableSchema[field].validValues.length;

  var range = sheet.getRange(1, startColumn, numberOfRows, 1);

  var rangeValues = [];

  for (var i in tableSchema[field].validValues)
  {
    rangeValues.push([tableSchema[field].validValues[i]]);
  }

  range.setValues(rangeValues);
  range.protect();
  range.getLastColumn();
  sheet.hideColumn(range);

  rule = SpreadsheetApp.newDataValidation().requireValueInRange(range).build();

  validValues[valueName] = {column: startColumn, range: numberOfRows, rule: rule};

  return validValues;
}

/**
 * Gets or creates a sheet
 *
 * @param {Spreadsheet} spreadSheet The scope of the db.
 * @param {string} scopeName The scope of the db.
 * @param {string} tableName The name of the table.
 * @return {Sheet} the sheet
 * @customfunction
 */
function getSheet(spreadSheet, scopeName, tableName)
{
  var sheetName = getSheetName(scopeName, tableName);

  var sheet = spreadSheet.getSheetByName(sheetName);

  if (sheet != null)
  {
    spreadSheet.deleteSheet(sheet);
  }

  sheet = spreadSheet.insertSheet(sheetName);

  return sheet;
}

function getSheetName(scopeName, tableName)
{
  return ["GameDB", scopeName, tableName].join("-");
}
