var importTestData = {
  id: "REPLACE_WITH_TEST_SHEET_ID",
  schema:
    '{"localizationDB":false,"scope":"TypeTest","tables":{"TypeTest1":{"fields":{"bool":{"isArray":false,"type":"bool","typeArg":null},"color":{"isArray":false,"type":"color","typeArg":null},"enum":{"isArray":false,"type":"enum","typeArg":"Days","validValues":["Sun","Mon","Tue","Wed","Thu","Fri","Sat"]},"float":{"isArray":false,"type":"float","typeArg":null},"int":{"isArray":false,"type":"int","typeArg":null},"obj":{"isArray":false,"type":"unityObject","typeArg":null},"string":{"isArray":false,"type":"string","typeArg":null},"tableRef":{"isArray":false,"type":"tableRef","typeArg":"TypeTest1"},"vec2":{"isArray":false,"type":"vector2","typeArg":null},"vec3":{"isArray":false,"type":"vector3","typeArg":null},"vec4":{"isArray":false,"type":"vector4","typeArg":null}},"key":{"type":"string","typeArg":null}},"TypeTest2":{"fields":{"bool":{"isArray":true,"type":"bool","typeArg":null},"color":{"isArray":true,"type":"color","typeArg":null},"enum":{"isArray":true,"type":"enum","typeArg":"Rarity","validValues":["tier1","tier2","tier3","tier4","tier5","tier6"]},"float":{"isArray":true,"type":"float","typeArg":null},"int":{"isArray":true,"type":"int","typeArg":null},"obj":{"isArray":true,"type":"unityObject","typeArg":null},"string":{"isArray":true,"type":"string","typeArg":null},"tableRef":{"isArray":true,"type":"tableRef","typeArg":"TypeTest1"},"vec2":{"isArray":true,"type":"vector2","typeArg":null},"vec3":{"isArray":true,"type":"vector3","typeArg":null},"vec4":{"isArray":true,"type":"vector4","typeArg":null}},"key":{"type":"enum","typeArg":"Colors"}}}}',
  data: '{"tables":{"TypeTest1":{"Test1":{"bool":true,"color":"#000000","enum":"Sun","float":34.5,"int":32,"obj":"","string":"test","tableRef":"Test1","vec2":"0.93,1.84","vec3":"1.03,2.03,1.82","vec4":"1.46,0.87,2.57,4.77"}},"TypeTest2":{"Green":{"bool":[false,true,false],"color":["#000000","#13FF00"],"enum":["tier1","tier2"],"float":[0,1.3,0],"int":[0,235,0],"obj":["",""],"string":["test1","test2"],"tableRef":["Test1","Test1"],"vec2":["0,0","0,0","0,0"],"vec3":["0,0,0","0,0,0","0,0,0"],"vec4":["0,0,0,0","0,0,0,0","0,0,0,0"]}}}}',
};

function testImport() {
  ImportJSON(importTestData.id, importTestData.schema, importTestData.data);
}

function testImportRequest() {
  var params = {
    mode: "import",
    id: importTestData.id,
    schema: importTestData.schema,
    data: importTestData.data,
  };

  var ret = handleRequest({
    parameters: params,
  });

  Logger.log(JSON.stringify(ret));
}

function testExport() {
  Logger.log(ExportJSON(importTestData.id, "TypeTest"));
}
