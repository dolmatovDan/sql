using System;
using System.ComponentModel.DataAnnotations;
namespace MknImmiSql.Api.V1;

public class DefaultValueInfo
{
    [Required] public Boolean IsSpecified { get; set; } = false;
    [Required] public Boolean IsNull { get; set; } = false;
    [Required] public String Value { get; set; } = String.Empty;
}

public class TableSchemaColumnInfo
{
    [Required] public String Name { get; set; } = String.Empty;
    [Required] public String Type { get; set; } = String.Empty;
    [Required] public Boolean IsPKey { get; set; } = false;
    [Required] public Boolean IsNullable { get; set; } = true;
    [Required] public DefaultValueInfo DefaultValue { get; set; } = new DefaultValueInfo();
}

public class TableSchemaInfo
{
    [Required] public TableSchemaColumnInfo[] Columns { get; set; } = new TableSchemaColumnInfo[0];
}

public class PostTablesSchemaOutput
{
    [Required] public TableSchemaInfo Schema { get; set; }
    public PostTablesSchemaOutput(String name)
    {
        Schema = ServiceContext.Instance.tables[name].Schema;
    }
}

public class TableList
{
    [Required] public String[] Tables { get; set; } = new String[0];
}
