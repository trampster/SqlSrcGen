using System;
using System.Collections.Generic;

namespace SqlSrcGen;

public class Query
{
    uint _highestPararmeter = 0;
    readonly Dictionary<uint, Parameter> _parameters = new Dictionary<uint, Parameter>();
    readonly HashSet<string> _sqlNames = new HashSet<string>();
    readonly HashSet<string> _csharpNames = new HashSet<string>();

    public void AddAutoNumbered(Token token)
    {
        _highestPararmeter++;
        _parameters.Add(_highestPararmeter, new Parameter
        {
            Number = _highestPararmeter,
            CSharpName = $"param{_highestPararmeter}",
            SqlName = null
        });
    }

    public void AddNumberedParameter(uint number, Token token)
    {
        if (_highestPararmeter < number)
        {
            _highestPararmeter = number;
        }

        if (_parameters.ContainsKey(number))
        {
            return; // already have it
        }
        _parameters.Add(number, new Parameter
        {
            Number = number,
            CSharpName = $"param{number}",
            SqlName = null
        });
    }

    void AddParameter(Parameter parameter, Token token)
    {
        _parameters.Add(parameter.Number, parameter);

        if (_csharpNames.Contains(parameter.CSharpName))
        {
            throw new InvalidSqlException("Parameter produces the same c# name as an existing parameter", token);
        }
        _csharpNames.Add(parameter.CSharpName);
        _sqlNames.Add(parameter.SqlName);
    }

    public void AddNamedParameter(string sqlName, Token token)
    {
        // Because the parameter names end up as csharp names :, @ and $ parameters in
        // although unique in sql due to including the :, @ or $ are not in SqlScrGen
        // as we can't have those in 


        if (_sqlNames.Contains(sqlName))
        {
            return; // already have it
        }
        _highestPararmeter++;

        string name = sqlName.AsSpan().Slice(1).ToString();

        AddParameter(new Parameter
        {
            Number = _highestPararmeter,
            SqlName = sqlName,
            CSharpName = CSharp.ToCSharpName(name),
        }, token);
    }

}

public class Parameter
{
    public uint Number { get; set; }
    public string CSharpName { get; set; }
    public string SqlName { get; set; }
}