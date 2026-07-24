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
