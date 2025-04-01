using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MknImmiSql.Api.V1;

public class SchemaInput
{
    [Required] public String Name { get; set; } = String.Empty;
}

[Route("/api/v1/tables/list")]
public class TableListController : Controller
{
    [HttpGet]
    public IActionResult Get()
    {
        String[] allTables = new String[ServiceContext.Instance.tables.Count];
        Int32 last = 0;
        foreach (var v in ServiceContext.Instance.tables)
        {
            allTables[last++] = v.Key;
        }
        return Ok(new TableList { Tables = allTables });
    }
}

[Route("/api/v1/tables/schema")]
public class TableSchemaController : Controller
{
    [HttpPost]
    public IActionResult Get([FromBody] SchemaInput input)
    {
        if (ServiceContext.Instance.tables.ContainsKey(input.Name))
            return Ok(new PostTablesSchemaOutput(input.Name));
        return new StatusCodeResult(StatusCodes.Status404NotFound);
    }
}
