using System;
using Microsoft.AspNetCore.Http;
namespace MknImmiSql.Api.V1;

public class DropCommand : ISqlCommand
{
    private String _name;
    private Boolean _checkExistance;
    public DropCommand(String name, Boolean checkExistance)
    {
        _name = name;
        _checkExistance = checkExistance;
    }

    public Table Execute()
    {
        if (!ServiceContext.Instance.tables.ContainsKey(_name))
        {
            if (!_checkExistance)
                throw new TableException(StatusCodes.Status404NotFound, "There is no table with this name.");
            else
                return Table.CreateBooleanTable(false);
        }
        ServiceContext.Instance.tables.Remove(_name);
        return Table.CreateBooleanTable(true);
    }
}
