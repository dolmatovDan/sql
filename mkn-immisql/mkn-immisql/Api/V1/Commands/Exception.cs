using System;
namespace MknImmiSql.Api.V1;

public class TableException : Exception
{
    public Int32 StatusCode { get; set; }
    public String Text { get; set; } = String.Empty;
    public TableException(Int32 code, String message)
    {
        Text = message;
        StatusCode = code;
    }
}
