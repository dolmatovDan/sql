using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace MknImmiSql.Api.V1;

public class Table
{
    [Required] public TableSchemaInfo Schema { get; set; }
    [Required] public List<List<String?>> Result { get; set; }

    public static Table CreateBooleanTable(Boolean value)
    {
        Table result = new Table();

        result.Schema.Columns = new TableSchemaColumnInfo[1];
        result.Schema.Columns[0] = new TableSchemaColumnInfo();
        result.Schema.Columns[0].Type = "boolean";
        result.Schema.Columns[0].Name = "result";
        result.Schema.Columns[0].IsNullable = false;

        result.Result = new List<List<String?>>();
        result.Result.Add(new List<String?>());
        result.Result[0].Add(value ? "true" : "false");
        return result;
    }

    public Table()
    {
        Result = new List<List<String?>>();
        Schema = new TableSchemaInfo();
    }
}

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
