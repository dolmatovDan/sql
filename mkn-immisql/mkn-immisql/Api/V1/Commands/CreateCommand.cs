using System;
using Microsoft.AspNetCore.Http;
namespace MknImmiSql.Api.V1;

public interface ISqlCommand
{
    Table Execute();
}

public class CreateCommand : ISqlCommand
{
    private Table _table;
    private String _name;
    private Boolean _checkExistance;

    public CreateCommand(Table table, String name, Boolean checkExistance)
    {
        _table = table;
        _name = name;
        _checkExistance = checkExistance;
    }

    public Table Execute()
    {
        if (ServiceContext.Instance.tables.ContainsKey(_name))
        {
            if (!_checkExistance)
            {
                throw new TableException(StatusCodes.Status409Conflict, "Table with the same name already exists.");
            }
            else
                return Table.CreateBooleanTable(false);
        }
        ServiceContext.Instance.CreateTable(_name, _table);
        return Table.CreateBooleanTable(true);
    }

    public static Boolean ValidateColumnSchema(TableSchemaColumnInfo schema)
    {
        if (schema.Type == "serial" && !schema.IsPKey)
            return false;
        if (schema.IsNullable == false && schema.DefaultValue.IsSpecified && schema.DefaultValue.IsNull)
            return false;
        return true;
    }

    public static Boolean ValidateSchema(TableSchemaInfo schema)
    {
        Int32 cntPKey = 0;
        foreach (var column in schema.Columns)
        {
            if (column.IsPKey)
                cntPKey++;
        }
        if (cntPKey > 1)
            return false;
        return true;
    }

}
