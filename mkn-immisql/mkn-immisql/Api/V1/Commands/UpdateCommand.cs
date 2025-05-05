using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
namespace MknImmiSql.Api.V1;

public class UpdateCommand : ISqlCommand
{
    private String _name;
    private List<List<Token>> _setValues;
    private WhereStruct? _whereStruct;
    private List<String>? _returningColumns;
    private Int32 whereColIndex = -1;
    private Dictionary<Int32, String?> _columnValues;

    public UpdateCommand(String name, List<List<Token>> setValues,
        WhereStruct? whereStruct, List<String>? returningColumns)
    {
        _name = name;
        _setValues = setValues;
        _whereStruct = whereStruct;
        _returningColumns = returningColumns;
        _columnValues = new Dictionary<Int32, String?>();

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
        // Checking name
        if (!ServiceContext.Instance.tables.ContainsKey(_name))
            throw new TableException(StatusCodes.Status404NotFound, "Incorrect name");
        Table table = ServiceContext.Instance.tables[_name];

        // Checking set args
        foreach (var args in _setValues)
        {
            for (Int32 i = 0; i < table.Schema.Columns.Length; ++i)
            {
                var col = table.Schema.Columns[i];
                if (col.Name == args[0].str)
                {
                    if (args[2].str == "default")
                    {
                        if (!col.DefaultValue.IsSpecified)
                            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect type");

                        if (col.DefaultValue.IsNull)
                            args[2].str = "null";
                        else
                            args[2].str = col.DefaultValue.Value;
                    }
                    else if (Token.GetType(args[2]) != col.Type)
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect type");
                    _columnValues[i] = args[2].str == "null" ? null : args[2].str;
                }
            }
        }

        // Checking where type
        if (_whereStruct != null)
        {
            for (Int32 i = 0; i < table.Schema.Columns.Length; ++i)
            {
                var col = table.Schema.Columns[i];
                if (col.Name == _whereStruct.colName)
                {
                    whereColIndex = i;
                    String currentType = col.Type;
                    if (currentType == "serial")
                        currentType = "integer";
                    if (currentType != _whereStruct.cond.type)
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect type");

                    break;
                }
            }
        }

        if (_whereStruct != null && whereColIndex == -1)
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect type");
    }

    public Table Execute()
    {
        Table table = ServiceContext.Instance.tables[_name];
        List<Int32> rowIndexes = new List<Int32>();
        if (_whereStruct == null)
        {
            for (Int32 i = 0; i < table.Result.Count; ++i)
            {
                rowIndexes.Add(i);
            }
        }
        else
        {

            rowIndexes = table.GetWhereIndexes(table.Result, _whereStruct.cond,
                whereColIndex, table.Schema.Columns[whereColIndex].Type, _whereStruct.value);

            foreach (Int32 i in rowIndexes)
            {
                foreach (var (col, val) in _columnValues)
                {
                    table.Result[i][col] = val;
                }
            }
        }

        if (!table.CheckPKey())
            throw new TableException(StatusCodes.Status409Conflict, "Same value of PKey");

        if (_returningColumns == null)
            return new Table();

        List<List<String?>> changedRows = new List<List<String?>>();
        foreach (var i in rowIndexes)
        {
            changedRows.Add(table.Result[i]);
        }
        Table returningTable = Table.GetReturningTable(_returningColumns, table.Schema.Columns, changedRows).CreateReturnTable();

        return returningTable;
    }
}
