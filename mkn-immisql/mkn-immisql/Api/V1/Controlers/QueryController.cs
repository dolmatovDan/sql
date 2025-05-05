using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MknImmiSql.Api.V1;

public class QueryInput
{
    [Required] public String Query { get; set; } = String.Empty;
}

[Route("api/v1/query")]
public class QueryController : Controller
{
    [HttpPost]
    public IActionResult Get([FromBody] QueryInput query)
    {
        try
        {
            ISqlCommand result = ServiceContext.Parser.ApplyCommand(query.Query);
            Table resultTable = result.Execute();
            return Ok(resultTable);
        }
        catch (TableException e)
        {
            return StatusCode(e.StatusCode, Table.CreateBooleanTable(false));
        }
    }
}
