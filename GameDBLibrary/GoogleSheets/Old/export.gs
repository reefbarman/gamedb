function onOpen() 
{
  var ui = SpreadsheetApp.getUi();
  ui.createMenu('GameDB')
      .addItem('Export JSON', 'showDownloadDialog')
      .addToUi();
}

function doGet(e)
{
  return exportJSON();
}

function exportJSON()
{
  try
  {
    var gameDB = {"tables": {}};
    
    var sheets = SpreadsheetApp.getActive().getSheets();
    
    for (var i in sheets)
    {
      var sheet = sheets[i];
      
      var sheetName = sheet.getName();
      
      if (sheetName.indexOf("GameDB-") == 0)
      {
        var tableName = sheetName.split("-")[2];
        var tableData = exportSheet(sheet, tableName);
        
        gameDB.tables[tableName] = tableData;
      }
    }
    
    var output = ContentService.createTextOutput();
    output.setContent(JSON.stringify(gameDB));
    output.setMimeType(ContentService.MimeType.JSON);
    output.downloadAsFile("gameDB.json");
    return output;
  }
  catch (e)
  {
    return ContentService.createTextOutput("Error occurred: " + e.message);
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
  
  for (var i in rows[0])
  {
    var key = rows[0][i];
    
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
          
          var data = columnData[0][j];
          
          if (fieldSchema.isArray)
          {
            data = data.split(",");
            
            for (var k in data)
            {
              validateData(schema, fieldSchema, data[k]);
            }
          }
          else
          {
            validateData(schema, fieldSchema, data);
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
      if (!(typeof data === 'number' && (data % 1) === 0))
      {
        throw new Error(data + " is not an int");
      }
      break;
    case "float":
      if (!(typeof data === 'number'))
      {
        throw new Error(data + " is not a float");
      }
      break;
    case "bool":
      if (!(typeof data === 'boolean'))
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
    case "prefab":
      if (fieldSchema.validValues.indexOf(data) == -1)
      {
        throw new Error(data + " is not a valid imported prefab");
      }
      break;
    case "tableRef":
      var sheetName = ["GameDB", schema.scope, fieldSchema.typeArg].join("-");
      var sheet = SpreadsheetApp.getActive().getSheetByName(sheetName);
      var range = sheet.getRange(2, 1, sheet.getLastRow());
      var keys = range.getValues();
      
      if (keys[0].indexOf(data) == -1)
      {
        throw new Error(data + " is not a valid table reference to " + fieldSchema.typeArg);
      }
      break;
  }
}

function showDownloadDialog(){
  var doc = HtmlService.createHtmlOutput();
  doc.setWidth(200).setHeight(50);
  doc.setTitle("Export JSON");
  doc.append("<a href=\"" + ScriptApp.getService().getUrl() + "\" target=\"_blank\">Download</a>");
  SpreadsheetApp.getActive().show(doc);
}

function errorDialog(message)
{
  var ui = SpreadsheetApp.getUi();
  ui.alert('Error', message, ui.ButtonSet.OK);
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