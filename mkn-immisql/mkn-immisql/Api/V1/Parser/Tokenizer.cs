using System.Linq;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MknImmiSql.Api.V1;

public class Tokenizer
{
    public static readonly Char[] OPERATORS = { ',', '(', ')', '*', ';', '<', '>', '=', '!', '.' };
    public static readonly Char[] ALPHABET =
    {
        'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n',
        'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B',
        'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
        'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', '_', '.', '\'', '"',
        '!',

        '\u0410', '\u0411', '\u0412', '\u0413', '\u0414', '\u0415', '\u0416',
        '\u0417', '\u0418', '\u0419', '\u041A', '\u041B', '\u041C', '\u041D',
        '\u041E', '\u041F', '\u0420', '\u0421', '\u0422', '\u0423', '\u0424',
        '\u0425', '\u0426', '\u0427', '\u0428', '\u0429', '\u042A', '\u042B',
        '\u042C', '\u042D', '\u042E', '\u042F', '\u0401',

        '\u0430', '\u0431', '\u0432', '\u0433', '\u0434', '\u0435', '\u0436',
        '\u0437', '\u0438', '\u0439', '\u043A', '\u043B', '\u043C', '\u043D',
        '\u043E', '\u043F', '\u0440', '\u0441', '\u0442', '\u0443', '\u0444',
        '\u0445', '\u0446', '\u0447', '\u0448', '\u0449', '\u044A', '\u044B',
        '\u044C', '\u044D', '\u044E', '\u044F', '\u0451',
    };

    public static readonly Char[] NUMBERS = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
    public static readonly Char[] SPACES = { ' ', '\n', '\t', '\r' };
    public static readonly Char[] ALL =
      OPERATORS.Concat(ALPHABET).Concat(NUMBERS).Concat(SPACES).ToArray();

    public static readonly Char finalChar = '$';

    private Int32 _startNode;
    private Dictionary<Int32, Node> _nodes;
    private Dictionary<Int32, EToken> _nodeToToken;

    public Tokenizer()
    {
        _nodes = new Dictionary<Int32, Node>();
        _startNode = 0;
        build();
        _nodeToToken = new Dictionary<int, EToken>();
        _nodeToToken[2] = EToken.Space;
        _nodeToToken[5] = EToken.Numeric;
        _nodeToToken[7] = EToken.Operator;
        _nodeToToken[9] = EToken.Literal;
        _nodeToToken[17] = EToken.String;
        _nodeToToken[16] = EToken.String;
    }

    private void CreateTransition(Int32 node, Char ch, Int32 dest)
    {
        if (_nodes[node].Transition.ContainsKey(ch))
            return;
        _nodes[node].Transition[ch] = dest;
    }

    private void build()
    {
        // Init Node
        _nodes[_startNode] = new Node(false);


        // Spaces
        _nodes[1] = new Node(false);
        _nodes[2] = new Node(true);
        foreach (Char ch in SPACES)
        {
            CreateTransition(_startNode, ch, 1);
            _nodes[1].Transition[ch] = 1;
        }
        foreach (Char ch in ALL)
        {
            if (!Array.Exists(SPACES, c => c == ch))
            {
                _nodes[1].Transition[ch] = 2;
            }
        }
        _nodes[1].Transition[finalChar] = 2;


        // Numeric
        _nodes[3] = new Node(false);
        _nodes[4] = new Node(false);
        _nodes[5] = new Node(true);
        _nodes[18] = new Node(false);
        _nodes[19] = new Node(false);
        foreach (Char ch in NUMBERS)
        {
            CreateTransition(_startNode, ch, 3);
            _nodes[3].Transition[ch] = 3;
            _nodes[4].Transition[ch] = 4;
        }
        _nodes[3].Transition['.'] = 4;
        foreach (Char ch in ALL)
        {
            if (!Array.Exists(NUMBERS, c => c == ch))
            {
                _nodes[4].Transition[ch] = 5;
                if (ch != '.')
                {
                    _nodes[3].Transition[ch] = 5;
                }
            }
        }
        _nodes[4].Transition['e'] = 18;
        // for scientific format
        _nodes[18].Transition['+'] = 19;
        _nodes[18].Transition['-'] = 19;
        foreach (Char ch in NUMBERS)
        {
            _nodes[19].Transition[ch] = 19;
        }
        foreach (Char ch in ALL)
        {
            if (!Array.Exists(NUMBERS, c => c == ch))
            {
                _nodes[19].Transition[ch] = 5;
            }
        }
        _nodes[3].Transition[finalChar] = 5;
        _nodes[4].Transition[finalChar] = 5;
        _nodes[19].Transition[finalChar] = 5;


        // Operator
        _nodes[6] = new Node(false);
        _nodes[20] = new Node(false);
        _nodes[21] = new Node(false);
        _nodes[7] = new Node(true);
        CreateTransition(_startNode, '!', 20);
        CreateTransition(_startNode, '<', 20);
        CreateTransition(_startNode, '>', 20);
        foreach (Char ch in ALL)
        {
            if (Array.Exists(OPERATORS, c => c == ch))
            {
                CreateTransition(_startNode, ch, 6);
                _nodes[6].Transition[ch] = 7;
            }
            else
            {
                _nodes[6].Transition[ch] = 7;
            }
        }
        foreach (Char ch in ALL)
        {
            if (ch != '=')
            {
                _nodes[20].Transition[ch] = 7;
            }
            _nodes[21].Transition[ch] = 7;
        }
        _nodes[20].Transition['='] = 21;

        _nodes[6].Transition[finalChar] = 7;
        _nodes[20].Transition[finalChar] = 7;
        _nodes[21].Transition[finalChar] = 7;


        // Literal
        _nodes[8] = new Node(false);
        _nodes[9] = new Node(true);
        foreach (Char ch in ALPHABET)
        {
            if (ch != '\'' && ch != '"')
                CreateTransition(_startNode, ch, 8);
        }
        foreach (Char ch in ALPHABET.Concat(NUMBERS))
        {
            _nodes[8].Transition[ch] = 8;
        }
        foreach (Char ch in ALL)
        {
            if (!(Array.Exists(ALPHABET, c => c == ch) || Array.Exists(NUMBERS, c => c == ch)))
            {
                _nodes[8].Transition[ch] = 9;
            }
        }
        _nodes[8].Transition['.'] = 9;
        _nodes[8].Transition[finalChar] = 9;


        // String, single quoted
        _nodes[10] = new Node(false);
        _nodes[11] = new Node(false);
        _nodes[12] = new Node(false);
        _nodes[17] = new Node(true);
        CreateTransition(_startNode, '\'', 10);
        foreach (Char ch in ALL)
        {
            if (ch != '\'' && ch != '\\')
            {
                _nodes[10].Transition[ch] = 10;
            }
        }
        _nodes[10].Transition['\\'] = 11;
        foreach (Char ch in ALL)
        {
            _nodes[11].Transition[ch] = 10;
        }
        _nodes[10].Transition['\''] = 12;
        _nodes[10].Transition[finalChar] = 12;
        _nodes[11].Transition[finalChar] = 12;
        foreach (Char ch in ALL)
        {
            _nodes[12].Transition[ch] = 17;
        }
        _nodes[12].Transition[finalChar] = 17;


        // String, double quoted
        _nodes[13] = new Node(false);
        _nodes[14] = new Node(false);
        _nodes[15] = new Node(false);
        _nodes[16] = new Node(true);
        CreateTransition(_startNode, '"', 13);
        foreach (Char ch in ALL)
        {
            if (ch != '\\' && ch != '"')
            {
                _nodes[13].Transition[ch] = 13;
            }
        }
        _nodes[13].Transition['\\'] = 14;
        foreach (Char ch in ALL)
        {
            _nodes[14].Transition[ch] = 13;
        }
        _nodes[13].Transition['"'] = 15;

        foreach (Char ch in ALL)
        {
            _nodes[15].Transition[ch] = 16;
        }

        _nodes[13].Transition[finalChar] = 16;
        _nodes[14].Transition[finalChar] = 16;
        _nodes[15].Transition[finalChar] = 16;
    }

    public List<Token> SplitInTokens(String s)
    {
        s = s + finalChar;
        List<Token> result = new List<Token>();
        Int32 start = _startNode;
        while (start < s.Length - 1)
        {
            try
            {
                Token currentToken = GetToken(s, start);
                result.Add(currentToken);
                start = currentToken.r + 1;
            }
            catch
            {
                throw;
            }
        }
        foreach (var t in result)
        {
            if (t.type != EToken.String)
            {
                t.str = t.str.ToLower();
            }
        }
        return result;
    }

    private Token GetToken(String s, Int32 start)
    {
        Token result = new Token();
        result.l = start;
        result.r = -1;
        Int32 currentNode = _startNode;
        for (Int32 i = start; i < s.Length; ++i)
        {
            if (!_nodes[currentNode].Transition.ContainsKey(s[i]))
                throw new TableException(StatusCodes.Status400BadRequest, "Invalid string");
            currentNode = _nodes[currentNode].Transition[s[i]];
            if (_nodes[currentNode].IsTerminal)
            {
                result.r = i - 1;
                result.type = _nodeToToken[currentNode];
                break;
            }
        }
        if (result.r == -1)
            result.r = s.Length - 1;
        result.str = s.Substring(result.l, result.r - result.l + 1);
        return result;
    }

    public List<Token> SplitInTokensWithoutSpaces(String s)
    {
        List<Token> splittedString = SplitInTokens(s);
        return splittedString.Where(t => t.type != EToken.Space).ToList();
    }

    public Token CreateToken(String s)
    {
        List<Token> splittedString = SplitInTokens(s);
        if (splittedString.Count != 1)
            throw new ArgumentException();
        return splittedString[0];
    }

    public List<Token> CreateTokenSequence(List<String> tokens)
    {
        List<Token> result = new List<Token>();
        foreach (var s in tokens)
        {
            result.Add(CreateToken(s));
        }
        return result;
    }
}

