using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class JoinCommand : ISqlCommand
{
    private String _leftTableName;
    private String _rightTableName;
    private SelectField _leftField;
    private SelectField _rightField;
    private EJoinType _joinType;

    public JoinCommand(String leftTableName, String rightTableName,
        SelectField leftField, SelectField rightField, EJoinType joinType)
    {
        _leftTableName = leftTableName;
        _rightTableName = rightTableName;
        _leftField = leftField;
        _rightField = rightField;
        _joinType = joinType;

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
        if (!ServiceContext.Instance.tables.ContainsKey(_leftTableName))
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        if (!ServiceContext.Instance.tables.ContainsKey(_rightTableName))
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

        if (_leftField.table.str != _leftTableName)
            (_leftTableName, _rightTableName) = (_rightTableName, _leftTableName);
        if (_leftField.table.str != _leftTableName || _rightField.table.str != _rightTableName)
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

        var getColType = (String colName, Table table) =>
        {
            Int32 index = GetIndexOfColumn(colName, table.Schema.Columns);
            if (index == -1)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            return table.Schema.Columns[index].Type;
        };

        if (!CompareTypes(getColType(_leftField.name.str, ServiceContext.Instance.tables[_leftField.table.str]),
            getColType(_rightField.name.str, ServiceContext.Instance.tables[_rightField.table.str])))
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private Boolean CompareTypes(String t1, String t2)
    {
        if (t1 == "serial" || t2 == "serial")
            return (t1 == "serial" || t1 == "integer") && (t2 == "serial" || t2 == "integer");
        return t1 == t2;
    }

    public Table Execute()
    {
        Table leftTable = ServiceContext.Instance.tables[_leftTableName];
        Table rightTable = ServiceContext.Instance.tables[_rightTableName];
        Table result = new Table();

        Int32 leftColIndex = GetIndexOfColumn(_leftField.name.str, leftTable.Schema.Columns);
        Int32 rightColIndex = GetIndexOfColumn(_rightField.name.str, rightTable.Schema.Columns);


        result.Schema.Columns = new TableSchemaColumnInfo[leftTable.Schema.Columns.Length +
          rightTable.Schema.Columns.Length];

        for (Int32 i = 0; i < leftTable.Schema.Columns.Length; ++i)
        {
            result.Schema.Columns[i] = Table.DeepCopy(leftTable.Schema.Columns[i]);
            result.Schema.Columns[i].Name = _leftTableName + "." + leftTable.Schema.Columns[i].Name;
        }
        for (Int32 i = 0; i < rightTable.Schema.Columns.Length; ++i)
        {
            result.Schema.Columns[i + leftTable.Schema.Columns.Length] =
              Table.DeepCopy(rightTable.Schema.Columns[i]);

            result.Schema.Columns[i + leftTable.Schema.Columns.Length].Name =
              _rightTableName + "." + rightTable.Schema.Columns[i].Name;
        }

        if (_joinType == EJoinType.Left)
        {
            for (Int32 i = 0; i < leftTable.Result.Count; ++i)
            {
                Int32 pos = rightTable.FindRow(_rightField.name.str, leftTable.Result[i][leftColIndex]);
                if (pos != -1)
                {
                    result.Result.Add(leftTable.Result[i].Concat(rightTable.Result[pos]).ToList());
                }
                else
                {
                    List<String?> temp = Enumerable.Repeat<String?>(null, rightTable.Schema.Columns.Length).ToList();
                    result.Result.Add(leftTable.Result[i].Concat(temp).ToList());
                }
            }
        }
        else if (_joinType == EJoinType.Right)
        {
            for (Int32 i = 0; i < rightTable.Result.Count; ++i)
            {
                Int32 pos = leftTable.FindRow(_leftField.name.str, rightTable.Result[i][rightColIndex]);
                if (pos != -1)
                {
                    result.Result.Add(leftTable.Result[i].Concat(rightTable.Result[pos]).ToList());
                }
                else
                {
                    List<String?> temp = Enumerable.Repeat<String?>(null, leftTable.Schema.Columns.Length).ToList();
                    result.Result.Add(temp.Concat(rightTable.Result[i]).ToList());
                }
            }
        }
        else if (_joinType == EJoinType.Inner)
        {
            for (Int32 i = 0; i < rightTable.Result.Count; ++i)
            {
                Int32 pos = leftTable.FindRow(_leftField.name.str, rightTable.Result[i][rightColIndex]);
                if (pos != -1)
                {
                    result.Result.Add(leftTable.Result[i].Concat(rightTable.Result[pos]).ToList());
                }
            }
        }

        foreach (var col in result.Schema.Columns)
        {
            col.IsPKey = false;
        }

        if (_joinType == EJoinType.Right)
        {
            for (Int32 i = 0; i < leftTable.Schema.Columns.Length; ++i)
            {
                result.Schema.Columns[i].IsNullable = true;
            }
        }
        else if (_joinType == EJoinType.Left)
        {
            for (Int32 i = leftTable.Schema.Columns.Length; i < result.Schema.Columns.Length; ++i)
            {
                result.Schema.Columns[i].IsNullable = true;
            }
        }
        for (Int32 i = 0; i < result.Schema.Columns.Length; ++i)
        {
            if (result.Schema.Columns[i].Type == "serial")
                result.Schema.Columns[i].Type = "integer";
        }

        return result;
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
}


public enum EJoinType
{
    Right,
    Left,
    Inner
}
