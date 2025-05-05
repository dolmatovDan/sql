using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class SelectCommand : ISqlCommand
{
    private ISqlCommand _sourceQuery;
    private WhereStruct? _whereStruct;
    private Int32? _limit;
    private OrderByField? _orderByField;
    private List<SelectField>? _selectFields;

    public SelectCommand(ISqlCommand table,
                         WhereStruct? whereStruct,
                         Int32? limit,
                         OrderByField? orderByField,
                         List<SelectField>? selectFields)
    {
        _sourceQuery = table;
        _whereStruct = whereStruct;
        _limit = limit;
        _orderByField = orderByField;
        _selectFields = selectFields;
    }

    public Table Execute()
    {
        Table table = _sourceQuery.Execute();
        List<Int32> selectedColumnsIndexes = new List<Int32>();
        List<String> aliases = new List<String>();

        // Checking columns
        if (_selectFields == null)
        {
            for (Int32 i = 0; i < table.Schema.Columns.Length; ++i)
            {
                selectedColumnsIndexes.Add(i);
                aliases.Add(table.Schema.Columns[i].Name);
            }
        }
        else
        {
            for (Int32 i = 0; i < _selectFields.Count; ++i)
            {
                Boolean ok = false;
                for (Int32 j = 0; j < table.Schema.Columns.Length; ++j)
                {
                    if (_selectFields[i].GetName() == table.Schema.Columns[j].Name)
                    {
                        if (_selectFields[i].nickName != null)
                            aliases.Add(_selectFields[i].nickName.str);
                        else
                            aliases.Add(table.Schema.Columns[j].Name);
                        ok = true;
                        selectedColumnsIndexes.Add(j);
                        break;
                    }
                }
                if (!ok)
                    throw new TableException(StatusCodes.Status400BadRequest, "Unknown column");

                if (aliases.Distinct().Count() != aliases.Count)
                    throw new TableException(StatusCodes.Status400BadRequest, "Unknown column");
            }
        }


        List<Int32> rowIndexes = new List<Int32>();

        // Checking whereStruct, and filling rows
        if (_whereStruct != null)
        {
            Int32 whereColIndex = -1;
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

            rowIndexes = table.GetWhereIndexes(table.Result, _whereStruct.cond,
                whereColIndex, _whereStruct.cond.type, _whereStruct.value);
        }
        else
        {
            for (Int32 i = 0; i < table.Result.Count; ++i)
            {
                rowIndexes.Add(i);
            }
        }

        List<List<String?>> returningRows = new List<List<String?>>();
        foreach (var i in rowIndexes)
        {
            returningRows.Add(table.Result[i]);
        }

        // Correcting limit
        if (_limit != null && returningRows.Count > _limit)
        {
            returningRows = returningRows.GetRange(0, _limit ?? 0);
        }

        // Proper sorting
        if (_orderByField != null)
        {
            Int32 orderCol = -1;
            for (Int32 i = 0; i < table.Schema.Columns.Length; ++i)
            {
                if (table.Schema.Columns[i].Name == _orderByField.columnName.str)
                {
                    orderCol = i;
                    break;
                }
            }
            if (orderCol == -1)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect type");

            if (_orderByField.ascending)
                returningRows.Sort((a, b) => (new WhereCondition(new Token(-1, -1, EToken.Operator, "<"),
                        table.Schema.Columns[orderCol].Type)).Evaluate(a[orderCol],
                        b[orderCol]) ? -1 : 1);
            else
                returningRows.Sort((a, b) => (new WhereCondition(new Token(-1, -1, EToken.Operator, "<"),
                        table.Schema.Columns[orderCol].Type)).Evaluate(a[orderCol],
                        b[orderCol]) ? 1 : -1);
        }


        List<String> selectedColumns = new List<String>();
        foreach (var i in selectedColumnsIndexes)
        {
            selectedColumns.Add(table.Schema.Columns[i].Name);
        }
        Table result = Table.GetReturningTable(selectedColumns,
            table.Schema.Columns, returningRows).CreateReturnTable();

        for (Int32 i = 0; i < result.Schema.Columns.Length; ++i)
        {
            result.Schema.Columns[i].Name = aliases[i];
        }

        return result;
    }
}

