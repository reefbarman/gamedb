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
