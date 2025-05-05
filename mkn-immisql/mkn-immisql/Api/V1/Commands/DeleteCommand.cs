using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class DeleteCommand : ISqlCommand
{
    private String _name;
    private WhereStruct? _whereCondition;
    private List<String>? _returningColumns;
    private List<Int32> _deletedRowsIndexes;

    public DeleteCommand(String name,
                         WhereStruct? whereCondition,
                         List<String>? returningColumns)
    {
        _name = name;
        _deletedRowsIndexes = new List<Int32>();
        _whereCondition = whereCondition;
        _returningColumns = returningColumns;

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
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect name");
        Table table = ServiceContext.Instance.tables[_name];

        // Check columns existance and type
        if (_whereCondition != null)
        {
            Int32 colIndex = -1;
            for (Int32 i = 0; i < table.Schema.Columns.Length; ++i)
            {
                var col = table.Schema.Columns[i];
                if (col.Name == _whereCondition.colName)
                {
                    if (col.Type == _whereCondition.cond.type)
                    {
                        colIndex = i;
                        break;
                    }
                    else
                    {
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect name");
                    }
                }
            }
            if (colIndex == -1)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect name");

            for (Int32 i = 0; i < table.Result.Count; ++i)
            {
                var row = table.Result[i];
                if (_whereCondition.cond.Evaluate(row[colIndex], _whereCondition.value))
                {
                    _deletedRowsIndexes.Add(i);
                }
            }
        }
        else
        {
            for (Int32 i = 0; i < table.Result.Count; ++i)
                _deletedRowsIndexes.Add(i);
        }

        // Check returning
        if (_returningColumns != null)
        {
            foreach (var col in _returningColumns)
            {
                Boolean find = false;
                foreach (var tableCol in table.Schema.Columns)
                {
                    if (tableCol.Name == col)
                        find = true;
                }
                if (!find)
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect name");
            }
        }
    }

    public Table Execute()
    {
        Table table = ServiceContext.Instance.tables[_name];

        // Mb should sort, but i hope not
        // List<Int32> _deletedRowsIndexesSorted = _deletedRowsIndexes.ToList();
        // _deletedRowsIndexesSorted.Sort((a, b) => b.CompareTo(a));

        List<List<String?>> deletedRows = new List<List<String?>>();
        _deletedRowsIndexes.Reverse();

        foreach (Int32 i in _deletedRowsIndexes)
        {
            deletedRows.Add(table.Result[i]);
            table.Result.RemoveAt(i);
        }
        deletedRows.Reverse();

        if (_returningColumns == null)
            return new Table();

        return Table.GetReturningTable(_returningColumns, table.Schema.Columns, deletedRows).CreateReturnTable();
    }
}
