using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class Table
{
    [Required] public TableSchemaInfo Schema { get; set; }
    [Required] public List<List<String?>> Result { get; set; }
    private Table? pkeyTable = null;

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

    public List<List<String?>> GetRightResult()
    {
        List<List<String?>> result = Result;
        for (Int32 i = 0; i < Schema.Columns.Length; ++i)
        {
            if (Schema.Columns[i].Type == "string")
            {
                for (Int32 j = 0; j < Result.Count; ++j)
                {
                    if (result[j][i] != null)
                        result[j][i] = EraseQuotes(result[j][i]);
                }
            }
        }

        foreach (var schema in Schema.Columns)
        {
            if (schema.Type == "string" && schema.DefaultValue.IsSpecified)
            {
                if (!schema.DefaultValue.IsNull)
                    schema.DefaultValue.Value = EraseQuotes(schema.DefaultValue.Value);
            }
        }
        return result;
    }

    public Table CreateReturnTable()
    {
        Table result = new Table();
        result.Schema = Schema;
        result.Result = GetRightResult();
        return result;
    }

    private String EraseQuotes(String s)
    {
        return s.Substring(1, s.Length - 2);
    }

    public static Table GetReturningTable(List<String>? returningColumns,
        TableSchemaColumnInfo[] columnsSchema, List<List<String?>> addedRows)
    {
        Table returningTable = new Table();
        if (returningColumns == null)
            return returningTable;

        Int32 cntColumns = returningColumns.Count;
        returningTable.Schema.Columns = new TableSchemaColumnInfo[cntColumns];
        // Setup TableSchema
        for (Int32 i = 0; i < cntColumns; ++i)
        {
            foreach (var schema in columnsSchema)
            {
                if (schema.Name == returningColumns[i])
                {
                    returningTable.Schema.Columns[i] = DeepCopy(schema);
                    break;
                }
            }
        }

        returningTable.Result = new List<List<String?>>();

        foreach (var row in addedRows)
        {
            List<String?> currentRow = new List<String?>();
            for (Int32 i = 0; i < returningColumns.Count; ++i)
            {
                for (Int32 schemaIndex = 0; schemaIndex < columnsSchema.Length; ++schemaIndex)
                {
                    var schema = columnsSchema[schemaIndex];
                    if (schema.Name == returningColumns[i])
                    {
                        currentRow.Add(row[schemaIndex]);
                    }
                }
            }

            returningTable.Result.Add(currentRow);
        }

        return returningTable;
    }

    public void MakePKey()
    {
        pkeyTable = new Table();
        pkeyTable.Schema.Columns = new TableSchemaColumnInfo[1];
        pkeyTable.Schema.Columns[0] = new TableSchemaColumnInfo();
        pkeyTable.Schema.Columns[0].Type = "integer";
        pkeyTable.Schema.Columns[0].Name = "PKey";
        pkeyTable.Schema.Columns[0].IsNullable = false;

        pkeyTable.Result = new List<List<String?>>();
        pkeyTable.Result.Add(new List<String?>());
        pkeyTable.Result[0].Add("1");
    }

    public String GetNextID()
    {
        if (pkeyTable == null)
            throw new NotSupportedException();

        Int32 currentId = -1;
        Int32.TryParse(pkeyTable.Result[0][0], out currentId);
        pkeyTable.Result[0][0] = (currentId + 1).ToString();
        return currentId.ToString();
    }

    public TableSchemaColumnInfo GetColumnInfo(String name)
    {
        foreach (var schema in Schema.Columns)
        {
            if (schema.Name == name)
                return schema;
        }
        throw new KeyNotFoundException();
    }

    public List<String?> GetColumnValues(String name)
    {
        Int32 pos = -1;
        for (Int32 i = 0; i < Schema.Columns.Length; ++i)
        {
            if (Schema.Columns[i].Name == name)
            {
                pos = i;
                break;
            }
        }
        if (pos == -1)
            throw new KeyNotFoundException();

        List<String?> result = new List<String?>();
        foreach (var row in Result)
        {
            result.Add(row[pos]);
        }
        return result;
    }

    public List<Int32> GetWhereIndexes(List<List<String?>> rows,
        WhereCondition condition, Int32 indexCol, String type, String comparedValue)
    {
        List<Int32> result = new List<Int32>();
        if (rows == null)
            return result;

        for (Int32 i = 0; i < rows.Count; ++i)
        {
            if (condition.Evaluate(rows[i][indexCol] ?? "null", comparedValue))
            {
                result.Add(i);
            }
        }

        return result;
    }

    public static TableSchemaColumnInfo DeepCopy(TableSchemaColumnInfo source)
    {
        TableSchemaColumnInfo destination = new TableSchemaColumnInfo();

        destination = new TableSchemaColumnInfo()
        {
            Name = source.Name,
            Type = source.Type,
            IsPKey = source.IsPKey,
            IsNullable = source.IsNullable,
            DefaultValue = source.DefaultValue,
        };
        return destination;
    }

    public Boolean CheckPKey()
    {
        List<Int32> pkeyIndexes = new List<Int32>();
        for (Int32 i = 0; i < Schema.Columns.Length; ++i)
        {
            if (Schema.Columns[i].IsPKey)
                pkeyIndexes.Add(i);
        }

        foreach (var j in pkeyIndexes)
        {
            List<String> values = new List<String>();
            for (Int32 i = 0; i < Result.Count; ++i)
            {
                if (Result[i][j] != null)
                    values.Add(Result[i][j]);
            }
            if (values.Distinct().Count() != values.Count)
                return false;
        }
        return true;
    }

    public Int32 FindRow(String colName, String? val)
    {
        Int32 pos = -1;
        for (Int32 i = 0; i < Schema.Columns.Length; ++i)
        {
            if (Schema.Columns[i].Name == colName)
                pos = i;
        }
        if (pos == -1)
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        for (Int32 i = 0; i < Result.Count; ++i)
        {
            if (Result[i][pos] == val)
                return i;
        }
        return -1;
    }
}

