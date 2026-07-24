function doPost(e) 
{
  if(typeof e !== 'undefined')
  {
    //TODO improve error handling
    //TODO check security key
    return ContentService.createTextOutput(JSON.stringify(importJson(e.parameters)));
  }
}

function test()
{
  importJson({
    id: "1MgK5cvArAvKl_laXvwQ_D8rahy0rvU_ysx_cOktetms", 
    schema: "{\"tables\":{\"ShipParts\":{\"fields\":{\"Name\":{\"type\":\"string\",\"isArray\":false,\"typeArg\":null},\"Cost\":{\"type\":\"int\",\"isArray\":false,\"typeArg\":null},\"Prefab\":{\"type\":\"prefab\",\"isArray\":false,\"typeArg\":null,\"validValues\":[\"Prefabs\/MyPrefab1\"]},\"Loot\":{\"type\":\"tableRef\",\"isArray\":true,\"typeArg\":\"Loot\"},\"Days\":{\"type\":\"enum\",\"isArray\":true,\"typeArg\":\"Days\",\"validValues\":[\"Sun\",\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\",\"Sat\"]},\"Type\":{\"type\":\"enum\",\"isArray\":false,\"typeArg\":\"Days\",\"validValues\":[\"Sun\",\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\",\"Sat\"]}}},\"Loot\":{\"fields\":{\"Name\":{\"type\":\"string\",\"isArray\":false,\"typeArg\":null},\"Health\":{\"type\":\"int\",\"isArray\":false,\"typeArg\":null},\"Day\":{\"type\":\"enum\",\"isArray\":false,\"typeArg\":\"Days\",\"validValues\":[\"Sun\",\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\",\"Sat\"]}}}},\"scope\":\"Main\"}",
    data: "{\"tables\":{\"ShipParts\":{\"Wing\":{\"Name\":\"Wing\",\"Cost\":40,\"Prefab\":\"Prefabs\/MyPrefab1\",\"Loot\":[\"Gold\"],\"Days\":[\"Mon\",\"Tue\",\"Wed\",\"Thu\",\"Fri\"],\"Type\":\"Sun\"}},\"Loot\":{\"Gold\":{\"Name\":\"Gold Coin\",\"Health\":100,\"Day\":\"Mon\"}}}}"
  });
}

function importJson(params) 
{
  var spreadSheet = SpreadsheetApp.openById(params.id);
  
  var schema = JSON.parse(params.schema);
  var data = JSON.parse(params.data);
  
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
    
    var range = sheet.getRange(1, sheet.getLastColumn() + 1, 1);
    range.setValue(params.schema);
    sheet.hideColumn(range);
  }
  
  return true;
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
          
        var fieldData = row[field] instanceof Array ? row[field].join(",") : row[field];
          
        rowData.push(fieldData);
      }
      
      rows.push(rowData);
    }
    
    var range = sheet.getRange(1,1,rows.length, headerRow.length);
    range.setValues(rows);
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
          case "prefab":
            if (!("prefab" in validValues))
            {
              validValues = createDataRange(sheet, field, tableSchema, validValues, "prefab");
            }
            
            var columnRange = sheet.getRange(2, parseInt(i) + 2, Object.keys(tablesData[tableName]).length, 1);
            columnRange.setDataValidation(validValues["prefab"].rule);
            break;
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
  var startColumn = (Object.keys(tableSchema).length + 4) + Object.keys(validValues).length;
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