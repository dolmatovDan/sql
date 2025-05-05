using System;
using Microsoft.AspNetCore.Http;
namespace MknImmiSql.Api.V1;

public class GetTableCommand : ISqlCommand
{
    private String _name;

    public GetTableCommand(String name)
    {
        _name = name;
        try
        {
            CheckOperation();
        }
        catch
        {
            throw;
        }
    }

    private void CheckOperation()
    {
        if (!ServiceContext.Instance.tables.ContainsKey(_name))
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
    }

    public Table Execute()
    {
        return ServiceContext.Instance.tables[_name];
    }
}

