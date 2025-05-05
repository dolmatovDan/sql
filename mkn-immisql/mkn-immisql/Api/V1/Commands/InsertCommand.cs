using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class InsertCommand : ISqlCommand
{
    private String _name;
    private List<String> _columnNames;
    private List<List<Token>> _rows;
    private List<List<String?>> _addedRows;
    private List<String>? _returningColumns;

    public InsertCommand(String name, List<String> columnNames, List<List<Token>> rows, List<String>? returningColumns)
    {
        _name = name;
        _columnNames = columnNames;
        _rows = rows;
        _addedRows = new List<List<String?>>();
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

    private Int32 GetIndexOfColumn(String colName, TableSchemaColumnInfo[] columns)
    {
        for (Int32 i = 0; i < columns.Length; ++i)
        {
            if (columns[i].Name == colName)
                return i;
        }

        return -1;
    }

    private void CheckOperation()
    {
        if (!ServiceContext.Instance.tables.ContainsKey(_name))
        {
            throw new TableException(StatusCodes.Status404NotFound, "There is no table with this name.");
        }

        Table table = ServiceContext.Instance.tables[_name];

        // Check that all column names are correct
        foreach (var colName in _columnNames)
        {
            if (GetIndexOfColumn(colName, table.Schema.Columns) == -1)
            {
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect column name");
            }
        }

        // Check that dimensions of row and cnt columns are same
        foreach (var row in _rows)
        {
            if (row.Count != _columnNames.Count)
                throw new TableException(StatusCodes.Status400BadRequest, "Not enough row values");
        }

        // Check each row satisfyies the type
        foreach (var row in _rows)
        {
            foreach (var (val, colName) in row.Zip(_columnNames))
            {
                var schema = table.GetColumnInfo(colName);
                if (schema.Type == "serial")
                    throw new TableException(StatusCodes.Status400BadRequest, "Can't specify serial column");
                if (!Token.CheckType(schema.Type, val))
                    throw new TableException(StatusCodes.Status400BadRequest, "Cell type don't match");

                if (schema.IsPKey)
                {
                    List<String?> column = table.GetColumnValues(colName);
                    foreach (var x in column)
                    {
                        if (x == val.str)
                        {
                            throw new TableException(StatusCodes.Status409Conflict,
                                "Column is primary key, all values should be unique");
                        }
                    }
                }
            }
        }

        Dictionary<String, Int32> colIndex = new Dictionary<String, Int32>();
        Int32 last = 0;
        for (Int32 i = 0; i < table.Schema.Columns.Length; ++i)
        {
            if (last < _columnNames.Count && _columnNames[last] == table.Schema.Columns[i].Name)
            {
                colIndex[_columnNames[last]] = i;
                last++;
            }
        }

        // Inserting values
        foreach (var row in _rows)
        {
            List<String?> currentRow = Enumerable.Repeat<String?>(null, table.Schema.Columns.Length).ToList();
            foreach (var (val, colName) in row.Zip(_columnNames))
            {
                currentRow[colIndex[colName]] = val.str;
            }

            Int32 pkeyIndex = -1;
            for (Int32 i = 0; i < currentRow.Count; ++i)
            {
                var val = currentRow[i];
                if (val == "null")
                    val = null;
                var columnSchema = table.Schema.Columns[i];

                if (columnSchema.IsPKey && columnSchema.Type == "serial")
                {
                    pkeyIndex = GetIndexOfColumn(columnSchema.Name, table.Schema.Columns);
                }
                else if (val == null && columnSchema.DefaultValue.IsSpecified)
                {
                    Int32 currentIndex = GetIndexOfColumn(columnSchema.Name, table.Schema.Columns);
                    currentRow[currentIndex] = columnSchema.DefaultValue.Value;
                }
                else if (!columnSchema.IsNullable && val == null)
                    throw new TableException(StatusCodes.Status400BadRequest, "Not nullable must be not null");
            }

            if (pkeyIndex != -1 && table.Schema.Columns[pkeyIndex].Type == "serial")
            {
                currentRow[pkeyIndex] = table.GetNextID();
            }

            _addedRows.Add(currentRow);
        }
    }

    public Table Execute()
    {
        Table table = ServiceContext.Instance.tables[_name];
        foreach (var row in _addedRows)
        {
            table.Result.Add(row);
        }

        if (!table.CheckPKey())
            throw new TableException(StatusCodes.Status409Conflict, "Same value of PKey");

        if (_returningColumns == null)
            return new Table();

        Table returningTable = Table.GetReturningTable(_returningColumns,
            table.Schema.Columns, _addedRows).CreateReturnTable();

        return returningTable;
    }

}
