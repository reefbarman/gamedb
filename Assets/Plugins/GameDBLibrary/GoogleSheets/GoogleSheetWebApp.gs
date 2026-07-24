function doPost(request)
{
  return ContentService.createTextOutput(JSON.stringify(handleRequest(request)));
}

function handleRequest(request)
{
  if(typeof request === 'undefined' || request == null)
  {
    return { error: { message: "invalid request"} };
  }

  if ((err = checkSecurity(request.parameter)) != null)
  {
    return { error: err };
  }

  if (!("mode" in request.parameter))
  {
    return { error: { message: "mode not set" } };
  }

  switch(request.parameter.mode)
  {
    case "import":
      if (!("id" in request.parameter) || !("schema" in request.parameter) || !("data" in request.parameter))
      {
        return { error: { message: "missing parameters" } };
      }

      var ret = ImportJSON(request.parameter["id"], request.parameter["schema"], request.parameter["data"]);

      if (ret == true)
      {
        ret = { success: true };
      }

      return ret;
    case "export":
      if (!("id" in request.parameter) || !("scope" in request.parameter))
      {
        return { error: { message: "missing parameters" } };
      }

      return ExportJSON(request.parameter["id"], request.parameter["scope"]);
  }

  return { error: { message: "unknown mode" } }
}

function checkSecurity(params)
{
  return null;
}
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
function ExportJSON(sheetID, scopeName)
{
  try
  {
    var gameDB = {"tables": {}};

    var sheets = SpreadsheetApp.openById(sheetID).getSheets();

    for (var i in sheets)
    {
      var sheet = sheets[i];

      var sheetName = sheet.getName();

      if (sheetName.indexOf("GameDB-" + scopeName) == 0)
      {
        var tableName = sheetName.split("-")[2];
        var tableData = exportSheet(sheet, tableName);

        gameDB.tables[tableName] = tableData;
      }
    }

    return gameDB;
  }
  catch (e)
  {
    return { error: { message: e.message } };
  }
}

function exportSheet(sheet, tableName)
{
  var tableData = {};

  var schemaJSON = sheet.getRange(1, sheet.getLastColumn(), 1).getValue();
  var schema = JSON.parse(schemaJSON);

  if (!(tableName in schema.tables))
  {
    throw new Error("Table " + tableName + " doesn't exist in schema!");
  }

  var tableSchema = schema.tables[tableName];

  var rows = sheet.getRange("A2:A").getValues();

  var rowKeys = [];

  for (var i in rows)
  {
    var key = rows[i][0];

    if (key == "")
    {
      break;
    }

    rowKeys.push(key);
    tableData[key] = {};
  }

  var startFieldColumn = 2;

  for (var field in tableSchema.fields)
  {
    var fieldSchema = tableSchema.fields[field];

    var headers = sheet.getRange(1, startFieldColumn, 1, Object.keys(tableSchema.fields).length).getValues();

    for (var i in headers[0])
    {
      var header = (headers[0][i]).split("(")[0].trim();

      if (header == field)
      {
        var columnLetter = columnToLetter(startFieldColumn + parseInt(i));

        var columnData = sheet.getRange(columnLetter + "2:" + columnLetter).getValues();

        for (var j in rowKeys)
        {
          var key = rowKeys[j];

          var data = columnData[j][0];

          if (fieldSchema.isArray)
          {
            if (data != "")
            {
              var temp = data.split("},{");

              if (temp.length > 1)
              {
                temp[0] = temp[0].substring(1);
                temp[temp.length-1] = temp[temp.length-1].substring(0, temp[temp.length-1].length - 1);
                data = temp;
              }
              else
              {
                data = data.split(",");
              }

              for (var k in data)
              {
                data[k] = validateData(schema, fieldSchema, data[k]);
              }
            }
            else
            {
              data = [];
            }
          }
          else
          {
            data = validateData(schema, fieldSchema, data);
          }

          tableData[key][field] = data;
        }

        break;
      }
    }
  }

  return tableData;
}

function validateData(schema, fieldSchema, data)
{
  switch(fieldSchema.type)
  {
    case "string":
      if (!(typeof data == "string"))
      {
        throw new Error(data + " is not a string");
      }
      break;
    case "int":
      try
      {
        if (isNaN(data) || parseInt(data) != data)
        {
          throw new Error(data + " is not an int");
        }

        data = parseInt(data);
      }
      catch(e)
      {
        throw new Error(data + " is not an int");
      }
      break;
    case "float":
      if (isNaN(data))
      {
        throw new Error(data + " is not a float");
      }

      data = parseFloat(data);
      break;
    case "bool":
      if (typeof data == "string" && (data.toLowerCase() == "true" || data.toLowerCase() == "false"))
      {
        data = data.toLowerCase() == "true" ? true : false;
      }
      else if (typeof data != "boolean")
      {
        throw new Error(data + " is not a bool");
      }
      break;
    case "enum":
      if (fieldSchema.validValues.indexOf(data) == -1)
      {
        throw new Error(data + " is not a " + fieldSchema.typeArg + " enum");
      }
      break;
    case "unityObject":
      if (!(typeof data == "string"))
      {
        throw new Error(data + " is not a valid unityObject");
      }
      break;
    case "tableRef":
      var sheetName = ["GameDB", schema.scope, fieldSchema.typeArg].join("-");
      var sheet = SpreadsheetApp.getActive().getSheetByName(sheetName);
      var range = sheet.getRange(2, 1, sheet.getLastRow());
      var keys = range.getValues();

      var valid = false;

      for (var i in keys)
      {
        if (keys[i].indexOf(data) != -1)
        {
          valid = true;
        }
      }

      if (!valid)
      {
        throw new Error(data + " is not a valid table reference to " + fieldSchema.typeArg);
      }
      break;
    case "color":
      if (!(typeof data == "string") || data[0] != "#")
      {
        throw new Error(data + " is not a color string");
      }
      break;
    case "vector2":
      if (!(typeof data == "string"))
      {
        throw new Error(data + " is not a vector2 string");
      }
      break;
    case "vector3":
      if (!(typeof data == "string"))
      {
        throw new Error(data + " is not a vector3 string");
      }
      break;
    case "vector4":
      if (!(typeof data == "string"))
      {
        throw new Error(data + " is not a vector4 string");
      }
      break;
  }

  return data;
}

function columnToLetter(column)
{
  var temp, letter = '';
  while (column > 0)
  {
    temp = (column - 1) % 26;
    letter = String.fromCharCode(temp + 65) + letter;
    column = (column - temp - 1) / 26;
  }
  return letter;
}
