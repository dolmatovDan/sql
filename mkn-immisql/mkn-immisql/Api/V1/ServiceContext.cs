using System;
using System.Collections.Generic;

namespace MknImmiSql.Api.V1;

public sealed class ServiceContext
{
    private static volatile ServiceContext _instance;
    private static readonly Object _lock = new Object();
    public static Parser Parser { get; set; } = new Parser(new Tokenizer());

    public String Token { get; private set; }

    public Dictionary<String, Table> tables { get; private set; } = new Dictionary<String, Table>();

    public void CreateTable(String name, Table table)
    {
        if (!tables.ContainsKey(name))
        {
            tables[name] = table;
        }
    }

    private ServiceContext()
    {
        Token = Guid.NewGuid().ToString("N");
    }

    public static ServiceContext Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            lock (_lock)
            {
                if (_instance is not null) return _instance;
                _instance = new ServiceContext();
                return _instance;
            }
        }
    }

}
