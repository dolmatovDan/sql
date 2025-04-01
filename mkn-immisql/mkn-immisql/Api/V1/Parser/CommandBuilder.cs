using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class Parser
{
    private readonly String[] KEYWORDS = new String[]
    {
        "create", "table", "if", "not", "exists", "primary",
        "key", "null", "default", "drop", "boolean", "integer", "float", "string", "serial"
    };

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

    private ISqlCommand RunCreateCommand(List<Token> tokens)
    {
        Table table = new Table();
        try
        {
            // Should start with CREATE TABLE
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "create", "table" })) == 0)
            {
                Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "create", "table" }));
            }
            else
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

            Boolean checkExistance = false;
            String table_name = String.Empty;

            // Check if has IF NOT EXISTS
            if (Token.FindSubarrayIndex(tokens, _tokenizer.CreateTokenSequence(new List<String>() { "if", "not", "exists" })) == 0)
            {
                Token.RemoveTokenSequence(ref tokens, _tokenizer.CreateTokenSequence(new List<String>() { "if", "not", "exists" }));
                checkExistance = true;
            }

            // Parse table name
            if (Token.IsInstanceName(tokens[0]))
            {
                table_name = tokens[0].str;
                tokens.RemoveAt(0);
            }
            else
                throw new TableException(StatusCodes.Status400BadRequest, "Incorrect query");

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
                }

                // Check DEFAULT value
                if (column.Count > 0)
                {
                    if (Token.IsEqual(column[0], _tokenizer.CreateToken("default")) && Token.CheckType(currentColumnSchema.Type, column[1]))
                    {
                        currentColumnSchema.DefaultValue.Value = column[1].str;
                        currentColumnSchema.DefaultValue.IsSpecified = true;
                        if (currentColumnSchema.DefaultValue.Value == "null")
                            currentColumnSchema.DefaultValue.IsNull = true;
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

            return new CreateCommand(table, table_name, checkExistance);
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
}
