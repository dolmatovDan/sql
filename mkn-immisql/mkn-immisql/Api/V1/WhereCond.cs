using System;
using Microsoft.AspNetCore.Http;
namespace MknImmiSql.Api.V1;

public class WhereCondition
{
    public Token op;
    public String type;
    public WhereCondition(Token op, String type)
    {
        this.op = op;
        this.type = type;
    }

    public Boolean Evaluate(String? l, String? r)
    {
        Token left = new Token();
        if (l != null)
            left.str = l;
        else
            left.str = "null";

        Token right = new Token();
        if (r != null)
            right.str = r;
        else
            right.str = "null";

        Int32 result = Token.CompareTokensWithType(left, right, type);

        switch (op.str)
        {
            case ">":
                return result > 0;
            case "<":
                return result < 0;
            case "=":
                return result == 0;
            case "!=":
                return result != 0;
            case ">=":
                return result >= 0;
            case "<=":
                return result <= 0;
        }
        throw new TableException(StatusCodes.Status400BadRequest, "Incorrect operation");
    }
}

public class WhereStruct
{
    public WhereCondition cond;
    public String colName;
    public String value;

    public WhereStruct(WhereCondition cond, String colName, String value)
    {
        this.cond = cond;
        this.colName = colName;
        this.value = value;
    }
}
