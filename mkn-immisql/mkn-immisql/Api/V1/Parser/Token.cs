using System;
using System.Collections.Generic;
using System.Linq;

namespace MknImmiSql.Api.V1;

public class Token
{
    public Int32 l { get; set; }
    public Int32 r { get; set; }
    public EToken type { get; set; }
    public String str { get; set; } = String.Empty;

    public Token(Int32 l, Int32 r, EToken type)
    {
        this.l = l;
        this.r = r;
        this.type = type;
    }

    public Token()
    {
        l = r = 0;
        type = EToken.Space;
    }

    public Boolean IsSingleQuoted()
    {
        return type == EToken.String && str[0] == '\'' && str[^1] == '\'';
    }
    public Boolean IsDoubleQuoted()
    {
        return type == EToken.String && str[0] == '"' && str[^1] == '"';
    }

    public static Boolean IsEqual(Token self, Token other)
    {
        if (other.type != self.type)
            return false;
        if (other.type == EToken.String)
            return other.str == self.str;
        return other.str == self.str;
    }

    public static Boolean IsInstanceName(Token t)
    {
        if (t.type == EToken.Literal)
            return true;
        if (t.type == EToken.String && t.IsDoubleQuoted())
            return true;
        return false;
    }

    public static Boolean CheckType(String type, Token t)
    {
        if (t.str == "null")
            return true;
        switch (type)
        {
            case "integer":
                return Int32.TryParse(t.str, out _);
            case "boolean":
                return Boolean.TryParse(t.str, out _);
            case "float":
                return Double.TryParse(t.str, out _);
            case "string":
                return t.IsSingleQuoted();
            case "serial":
                return Int32.TryParse(t.str, out _);
            default:
                return false;
        }
    }

    public static Boolean RemoveTokenSequence(ref List<Token> tokens, List<Token> target)
    {
        Int32 pos = Token.FindSubarrayIndex(tokens, target);
        if (pos == -1)
            return false;
        tokens = Token.RemoveSubarrayWithCopy(tokens, target).ToList();
        return true;
    }

    public static int FindSubarrayIndex(List<Token> mainArray, List<Token> subarray)
    {
        if (subarray.Count == 0 || mainArray.Count < subarray.Count)
            return -1;

        for (int i = 0; i <= mainArray.Count - subarray.Count; i++)
        {
            bool match = true;
            for (int j = 0; j < subarray.Count; j++)
            {
                if (!Token.IsEqual(mainArray[i + j], subarray[j]))
                {
                    match = false;
                    break;
                }
            }

            if (match) return i;
        }

        return -1;
    }

    public static List<Token> RemoveSubarrayWithCopy(List<Token> mainArray, List<Token> subarray)
    {
        int index = Token.FindSubarrayIndex(mainArray, subarray);
        if (index == -1) return mainArray;

        Token[] newArray = new Token[mainArray.Count - subarray.Count];
        Array.Copy(mainArray.ToArray(), 0, newArray, 0, index);
        Array.Copy(mainArray.ToArray(), index + subarray.Count, newArray, index,
            mainArray.Count - index - subarray.Count);
        return newArray.ToList();
    }
}

