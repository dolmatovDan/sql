using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class Parser
{
    private readonly String[] TYPES = new String[] { "boolean", "integer", "float", "string", "serial" };

    private Tokenizer _tokenizer;

    public Parser(Tokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    public ISqlCommand ApplyCommand(String s)
    {
        try
        {
            ISqlCommand result = GetCommand(_tokenizer.SplitInTokensWithoutSpaces(s));
            return result;
        }
        catch
        {
            throw;
        }
    }

    private Boolean EqualsToCommand(Token t, String command)
    {
        return t.type == EToken.Literal && t.str == command;
    }

    private ISqlCommand GetCommand(List<Token> tokens)
    {
        if (tokens.Count < 3)
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        if (tokens[^1].str == ";")
            tokens.RemoveAt(tokens.Count - 1);

        try
        {
            if (EqualsToCommand(tokens[0], "create"))
            {
                ISqlCommand result = RunCreateCommand(tokens);
                return result;
            }
            else if (EqualsToCommand(tokens[0], "drop"))
            {
                ISqlCommand result = RunDropCommand(tokens);
                return result;
            }
            else if (EqualsToCommand(tokens[0], "insert"))
            {
                ISqlCommand result = RunInsertCommand(tokens);
                return result;
            }
            else if (EqualsToCommand(tokens[0], "delete"))
            {
                ISqlCommand result = RunDeleteCommand(tokens);
                return result;
            }
            else if (EqualsToCommand(tokens[0], "update"))
            {
                ISqlCommand result = RunUpdateCommand(tokens);
                return result;
            }
            else if (EqualsToCommand(tokens[0], "select"))
            {
                ISqlCommand result = RunSelectCommand(ref tokens);
                return result;
            }
            else
            {
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            }
        }
        catch
        {
            throw;
        }
    }
    private ISqlCommand RunSubqueryCommand(ref List<Token> tokens)
    {
        if (tokens[0].str == "select")
            return RunSelectCommand(ref tokens);
        Boolean isJoin = false;
        foreach (var token in tokens)
        {
            if (token.str == "join")
                isJoin = true;
        }

        if (tokens[0].str == "(")
        {
            Int32 closingPos = GetClosingBracket(tokens, 0);
            List<Token> subTokens = new List<Token>();
            for (Int32 i = 1; i < closingPos; ++i)
            {
                subTokens.Add(tokens[i]);
            }
            while (tokens[0].str != ")")
                tokens.RemoveAt(0);
            tokens.RemoveAt(0);
            return RunSubqueryCommand(ref subTokens);
        }

        if (isJoin)
        {
            return RunJoinCommand(ref tokens);
        }
        else if (Token.IsInstanceName(tokens[0]))
        {
            Token name = tokens[0];
            tokens.RemoveAt(0);
            return new GetTableCommand(name.str);
        }
        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
    }

    private ISqlCommand RunJoinCommand(ref List<Token> tokens)
    {
        // Reading left table name
        String leftTableName = ReadName(ref tokens).str;

        // Reading join type
        EJoinType joinType;
        if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "left", "join" })) == 0)
        {
            joinType = EJoinType.Left;
            tokens.RemoveAt(0);
            tokens.RemoveAt(0);
        }
        else if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "right", "join" })) == 0)
        {
            joinType = EJoinType.Right;
            tokens.RemoveAt(0);
            tokens.RemoveAt(0);
        }
        else if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "join" })) == 0)
        {
            joinType = EJoinType.Inner;
            tokens.RemoveAt(0);
        }
        else if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "inner", "join" })) == 0)
        {
            joinType = EJoinType.Inner;
            tokens.RemoveAt(0);
            tokens.RemoveAt(0);
        }
        else
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

        // Reading right table name
        String rightTableName = ReadName(ref tokens).str;

        // Next should be ON
        if (tokens[0].str != "on")
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        tokens.RemoveAt(0);

        // Reading join names
        Token joinTableName = ReadName(ref tokens);
        // Here should be .
        if (tokens[0].str != ".")
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        tokens.RemoveAt(0);
        Token joinColumnName = ReadName(ref tokens);

        SelectField left = new SelectField(joinColumnName, joinTableName, null);
        // Should be =
        if (tokens[0].str != "=")
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        tokens.RemoveAt(0);

        joinTableName = ReadName(ref tokens);
        // Here should be .
        if (tokens[0].str != ".")
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        tokens.RemoveAt(0);
        joinColumnName = ReadName(ref tokens);

        SelectField right = new SelectField(joinColumnName, joinTableName, null);
        return new JoinCommand(leftTableName, rightTableName, left, right, joinType);
    }

    private ISqlCommand RunSelectCommand(ref List<Token> tokens)
    {
        try
        {
            tokens.RemoveAt(0);

            // Reading select fields
            List<SelectField>? selectFields = new List<SelectField>();
            Int32 fromIndex = -1;
            for (Int32 i = 0; i < tokens.Count; ++i)
            {
                if (tokens[i].str == "from")
                {
                    fromIndex = i;
                    break;
                }
            }
            if (fromIndex == -1)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            List<List<Token>> splittedFields = SplitTokens(tokens.GetRange(0, fromIndex), _tokenizer.CreateToken(","));
            if (splittedFields.Count == 1 && splittedFields[0].Count == 1 && splittedFields[0][0].str == "*")
            {
                selectFields = null;
            }
            else
            {
                foreach (var field in splittedFields)
                {
                    if (field.Count == 0 || !Token.IsInstanceName(field[0]))
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

                    SelectField currentField = new SelectField(field[0], null, null);
                    if (field.Count == 3)
                    {
                        if (Token.IsInstanceName(field[0]) && Token.IsInstanceName(field[2]) && field[1].str == ".")
                        {
                            currentField.name = field[2];
                            currentField.table = field[0];
                        }
                        else
                        {
                            if (field[1].str != "as" || !Token.IsInstanceName(field[2]))
                                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                            currentField.nickName = field[2];
                        }
                    }
                    else if (field.Count == 5)
                    {
                        // students . name AS name
                        if (!Token.IsInstanceName(field[2])
                            || !Token.IsInstanceName(field[4])
                            || field[1].str != "."
                            || field[3].str != "as")
                            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

                        currentField.name = field[2];
                        currentField.table = field[0];
                        currentField.nickName = field[4];
                    }

                    selectFields.Add(currentField);
                }
            }

            while (tokens[0].str != "from")
                tokens.RemoveAt(0);

            // Here should be FROM, checked before
            tokens.RemoveAt(0);
            ISqlCommand selectSource = RunSubqueryCommand(ref tokens);

            // Processing WHERE, ORDER BY, LIMIT
            WhereStruct? whereStruct = null;
            Int32? limit = null;
            OrderByField? orderByField = null;

            while (tokens.Count > 0)
            {
                if (tokens[0].str == "where")
                {
                    if (whereStruct != null)
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                    tokens.RemoveAt(0);
                    whereStruct = GetWhereStruct(ref tokens);
                }
                else if (tokens[0].str == "limit")
                {
                    if (limit != null)
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                    tokens.RemoveAt(0);
                    Token limitNum = tokens[0];
                    tokens.RemoveAt(0);
                    if (Token.GetType(limitNum) != "integer")
                    {
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                    }
                    Int32 temp = 0;
                    Int32.TryParse(limitNum.str, out temp);
                    limit = temp;
                }
                else if (tokens[0].str == "order")
                {
                    if (orderByField != null)
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

                    if (Token.FindSubarrayIndex(tokens,
                          _tokenizer.CreateTokenSequence(new List<String>() {
                            "order", "by" })) != 0) throw new TableException(StatusCodes.Status400BadRequest,
                          "Incorrect query");

                    Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "order", "by" }));

                    if (!Token.IsInstanceName(tokens[0]))
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                    if (tokens[1].str == "asc")
                        orderByField = new OrderByField(tokens[0], true);
                    else if (tokens[1].str == "desc")
                        orderByField = new OrderByField(tokens[0], false);
                    else
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

                    tokens.RemoveAt(0);
                    tokens.RemoveAt(0);
                }
                else
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            }

            if (tokens.Count > 0)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            return new SelectCommand(selectSource, whereStruct, limit, orderByField, selectFields);
        }
        catch (TableException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private ISqlCommand RunUpdateCommand(List<Token> tokens)
    {
        try
        {
            // Always starts with UPDATE
            tokens.RemoveAt(0);

            // Reading table name
            if (!Token.IsInstanceName(tokens[0]))
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens[0].ConvertToTableName();
            String tableName = tokens[0].str;
            tokens.RemoveAt(0);

            // Here should be SET
            if (tokens[0].str != "set")
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens.RemoveAt(0);

            // Parsing SET args
            List<List<Token>> setValues = new List<List<Token>>();
            while (tokens.Count > 2)
            {
                if (tokens[0].str == ",")
                {
                    tokens.RemoveAt(0);
                    continue;
                }

                if (tokens[0].str == "where" || tokens[0].str == "returning")
                    break;
                List<Token> curValues = new List<Token>();

                Token.GetType(tokens[2]); // Can throw error
                if (!Token.IsInstanceName(tokens[0]) || tokens[1].str != "=")
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

                tokens[0].ConvertToTableName();
                curValues.Add(tokens[0]);
                tokens.RemoveAt(0);

                curValues.Add(tokens[0]);
                tokens.RemoveAt(0);

                curValues.Add(tokens[0]);
                tokens.RemoveAt(0);

                setValues.Add(curValues);
            }

            WhereStruct? whereStruct = null;
            List<String>? returningColumns = null;
            // Check WHERE
            if (tokens.Count > 0 && tokens[0].str == "where")
            {
                tokens.RemoveAt(0);
                whereStruct = GetWhereStruct(ref tokens);
            }

            // Check RETURNING
            if (tokens.Count > 0 && tokens[0].str == "returning")
            {
                tokens.RemoveAt(0);
                returningColumns = GetReturning(ref tokens);
            }

            return new UpdateCommand(tableName, setValues, whereStruct, returningColumns);
        }
        catch (TableException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private ISqlCommand RunDeleteCommand(List<Token> tokens)
    {
        try
        {
            // Should start with DELETE FROM
            if (Token.FindSubarrayIndex(tokens,
                  _tokenizer.CreateTokenSequence(new List<String>() { "delete",
                    "from" })) != 0)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            Token.RemoveTokenSequence(ref tokens,
                _tokenizer.CreateTokenSequence(new List<String>() { "delete",
              "from" }));

            // Reading name
            if (!Token.IsInstanceName(tokens[0]))
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens[0].ConvertToTableName();
            String tableName = tokens[0].str;
            tokens.RemoveAt(0);

            WhereStruct? whereStruct = null;
            List<String>? returningColumns = null;
            // Check WHERE
            if (tokens.Count > 0 && tokens[0].str == "where")
            {
                tokens.RemoveAt(0);
                whereStruct = GetWhereStruct(ref tokens);
            }

            // Check RETURNING
            if (tokens.Count > 0 && tokens[0].str == "returning")
            {
                tokens.RemoveAt(0);
                returningColumns = GetReturning(ref tokens);
            }

            return new DeleteCommand(tableName, whereStruct, returningColumns);
        }
        catch (TableException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private ISqlCommand RunInsertCommand(List<Token> tokens)
    {
        try
        {
            // Should start with INSERT INTO
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "insert", "into" })) != 0)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "insert", "into" }));


            // Check name
            String tableName = String.Empty;
            if (!Token.IsInstanceName(tokens[0]))
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens[0].ConvertToTableName();
            tableName = tokens[0].str;
            tokens.RemoveAt(0);


            // Reading columns names
            List<String> columnsNames = new List<String>();
            if (tokens[0].str != "(")
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            tokens.RemoveAt(0);
            for (Int32 i = 0; i < tokens.Count; ++i)
            {
                if (tokens[i].str == ")")
                    break;

                if (tokens[i].str == ",")
                    continue;
                if (i > 0 && tokens[i - 1].str != ",")
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

                if (Token.IsInstanceName(tokens[i]))
                {
                    columnsNames.Add(tokens[i].str);
                }
                else
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            }
            while (tokens[0].str != ")")
            {
                tokens.RemoveAt(0);
            }
            tokens.RemoveAt(0);


            // Next should be VALUES
            if (tokens[0].str != "values")
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens.RemoveAt(0);

            Int32 returiningPos = Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "returning" }));
            if (returiningPos == -1)
                returiningPos = tokens.Count;
            List<List<Token>> splittedTokens = SplitTokensByPars(tokens.GetRange(0, returiningPos));

            List<List<Token>> addedRows = new List<List<Token>>();
            foreach (var row in splittedTokens)
            {

                addedRows.Add(row);
            }


            if (returiningPos == tokens.Count)
                return new InsertCommand(tableName, columnsNames, addedRows, null);

            tokens = tokens.GetRange(returiningPos, tokens.Count - returiningPos);

            if (tokens[0].str != "returning")
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens.RemoveAt(0);

            List<String> returningRows = new List<String>();
            List<List<Token>> splittedReturningTokens = SplitTokens(tokens, _tokenizer.CreateToken(","));
            foreach (var column in splittedReturningTokens)
            {
                if (column.Count != 1)
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                returningRows.Add(column[0].str);
            }

            return new InsertCommand(tableName, columnsNames, addedRows, returningRows);
        }
        catch (TableException)
        {
            throw;
        }
        catch
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private ISqlCommand RunCreateCommand(List<Token> tokens)
    {
        Table table = new Table();
        try
        {
            // Should start with CREATE TABLE
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "create", "table" })) == 0)
                Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "create", "table" }));
            else
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            Boolean checkExistance = false;
            String tableName = String.Empty;

            // Check if has IF NOT EXISTS
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "if", "not", "exists" })) == 0)
            {
                Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "if", "not", "exists" }));
                checkExistance = true;
            }

            // Parse table name
            if (Token.IsInstanceName(tokens[0]))
            {
                tokens[0].ConvertToTableName();
                tableName = tokens[0].str;
                tokens.RemoveAt(0);
            }
            else
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            if (tokens.Count == 0)
                return new CreateCommand(new Table(), tableName, checkExistance);

            // The body of the CREATE command should be surrounded with "()"
            if (tokens[0].str == "(" && tokens[^1].str == ")")
            {
                tokens.RemoveAt(tokens.Count - 1);
                tokens.RemoveAt(0);
            }
            else
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            // Split data into columns
            Token delim = _tokenizer.CreateToken(",");
            List<List<Token>> columns = SplitTokens(tokens, delim);

            List<TableSchemaColumnInfo> currentSchema = new List<TableSchemaColumnInfo>();
            foreach (List<Token> col in columns)
            {
                List<Token> column = col;
                TableSchemaColumnInfo currentColumnSchema = new TableSchemaColumnInfo();

                // Check name
                if (!Token.IsInstanceName(column[0]))
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                currentColumnSchema.Name = column[0].str;
                column.RemoveAt(0);

                // Check type
                if (Array.IndexOf(TYPES, column[0].str) == -1)
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                currentColumnSchema.Type = column[0].str;
                column.RemoveAt(0);

                // Check if has NOT NULL
                if (Token.RemoveTokenSequence(ref column, _tokenizer.CreateTokenSequence(new List<String>() { "not", "null" })))
                {
                    currentColumnSchema.IsNullable = false;
                }

                // Check if has PRIMARY KEY
                if (Token.RemoveTokenSequence(ref column, _tokenizer.CreateTokenSequence(new List<String>() { "primary", "key" })))
                {
                    currentColumnSchema.IsPKey = true;
                    currentColumnSchema.IsNullable = false;
                }

                // Check DEFAULT value
                if (column.Count > 0)
                {
                    if (currentColumnSchema.IsPKey)
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                    if (Token.IsEqual(column[0], _tokenizer.CreateToken("default")) && Token.CheckType(currentColumnSchema.Type, column[1]))
                    {
                        currentColumnSchema.DefaultValue.Value = column[1].str;
                        currentColumnSchema.DefaultValue.IsSpecified = true;
                        if (currentColumnSchema.DefaultValue.Value == "null")
                        {
                            currentColumnSchema.DefaultValue.IsNull = true;
                            currentColumnSchema.DefaultValue.Value = "";
                        }
                        else
                            currentColumnSchema.DefaultValue.IsNull = false;
                    }
                    else
                        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                }
                else
                {
                    currentColumnSchema.DefaultValue.IsSpecified = false;
                }

                if (!CreateCommand.ValidateColumnSchema(currentColumnSchema))
                    throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
                currentSchema.Add(currentColumnSchema);
            }
            table.Schema.Columns = currentSchema.ToArray();

            if (!CreateCommand.ValidateSchema(table.Schema))
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            foreach (var col in table.Schema.Columns)
            {
                if (col.IsPKey)
                    table.MakePKey();
            }

            return new CreateCommand(table, tableName, checkExistance);
        }
        catch (TableException)
        {
            throw;
        }
        catch
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private ISqlCommand RunDropCommand(List<Token> tokens)
    {
        try
        {
            // Should start with DROP TABLE
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "drop", "table" })) == 0)
            {
                Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "drop", "table" }));
            }
            else
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            // Check if has IF EXISTS
            Boolean checkExistance = false;
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "if", "exists" })) == 0)
            {
                checkExistance = true;
                Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "if", "exists" }));
            }

            if (tokens.Count != 1)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            if (!Token.IsInstanceName(tokens[0]))
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            tokens[0].ConvertToTableName();
            String name = tokens[0].str;
            return new DropCommand(name, checkExistance);
        }
        catch (TableException)
        {
            throw;
        }
        catch
        {
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        }
    }

    private static List<List<Token>> SplitTokens(List<Token> tokens, Token splitToken)
    {
        List<List<Token>> result = new List<List<Token>>();
        List<Token> currentList = new List<Token>();

        foreach (var token in tokens)
        {
            if (Token.IsEqual(token, splitToken))
            {
                if (currentList.Count > 0)
                {
                    result.Add(currentList);
                    currentList = new List<Token>();
                }
            }
            else
            {
                currentList.Add(token);
            }
        }

        if (currentList.Count > 0)
        {
            result.Add(currentList);
        }

        return result;
    }

    private static List<List<Token>> SplitTokensByPars(List<Token> tokens)
    {
        List<List<Token>> result = new List<List<Token>>();
        List<Token> currentList = new List<Token>();
        for (Int32 i = 0; i < tokens.Count; ++i)
        {
            if (tokens[i].str == ",")
                continue;
            if (tokens[i].str == "(")
                continue;
            if (tokens[i].str == ")")
            {
                result.Add(currentList);
                currentList = new List<Token>();
            }
            else
            {
                currentList.Add(tokens[i]);
            }
        }

        return result;
    }

    private WhereStruct? GetWhereStruct(ref List<Token> tokens)
    {

        WhereStruct? whereStruct = null;

        Token name = tokens[0];
        tokens.RemoveAt(0);

        Token op = tokens[0];
        tokens.RemoveAt(0);

        Token value = tokens[0];
        tokens.RemoveAt(0);

        WhereCondition whereCondition = new WhereCondition(op, Token.GetType(value));
        whereStruct = new WhereStruct(whereCondition, name.str, value.str);
        return whereStruct;
    }

    private List<String>? GetReturning(ref List<Token> tokens)
    {

        List<String> returningColumns = new List<String>();
        List<List<Token>> splitted = SplitTokens(tokens, _tokenizer.CreateToken(","));
        foreach (var token in splitted)
        {
            if (token.Count != 1)
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
            returningColumns.Add(token[0].str);
        }
        return returningColumns;
    }

    private Int32 GetClosingBracket(List<Token> tokens, Int32 openingPos)
    {
        Int32 balance = 0;
        for (Int32 i = openingPos; i < tokens.Count; ++i)
        {
            if (tokens[i].str == "(")
                balance++;
            if (tokens[i].str == ")")
                balance--;

            if (balance == 0)
                return i;
        }
        return -1;
    }

    private Token ReadName(ref List<Token> tokens)
    {
        if (!Token.IsInstanceName(tokens[0]))
            throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");
        tokens[0].ConvertToTableName();
        Token name = tokens[0];
        tokens.RemoveAt(0);
        return name;
    }
}

public class SelectField
{
    public Token name;
    public Token? table;
    public Token? nickName;

    public SelectField(Token name, Token? table, Token? nickName)
    {
        this.name = name;
        this.table = table;
        this.nickName = nickName;
    }

    public String GetName()
    {
        if (table != null)
            return table.str + "." + name.str;
        return name.str;
    }
}

public class OrderByField
{
    public Token columnName;
    public Boolean ascending;

    public OrderByField(Token columnName, Boolean ascending)
    {
        this.columnName = columnName;
        this.ascending = ascending;
    }
}
