using System;
using System.Collections.Generic;

namespace MknImmiSql.Api.V1;

public class Node
{
    public Boolean IsTerminal { get; init; }
    public Dictionary<Char, Int32> Transition { get; set; }
    public Node(Boolean isTerminal)
    {
        IsTerminal = isTerminal;
        Transition = new Dictionary<Char, Int32>();
    }
}

