using Dapper;
using System;
using System.Data;
using System.Linq.Expressions;
using System.Collections.Generic;

namespace GenericRepo.Dapper;

public class RequestParameterBuilder<TRequest>
{
    private readonly TRequest _request;

    // input mappings: parameter name + getter
    private readonly List<(string Name, Func<TRequest, object> ValueFunc)> _inputMappings
        = new List<(string, Func<TRequest, object>)>();

    // output mappings: parameter name + metadata
    private readonly List<(string Name, object? value, DbType DbType, ParameterDirection Direction, int? Size)> _outputMappings
        = new List<(string, object?, DbType, ParameterDirection, int?)>();

    private RequestParameterBuilder(TRequest request)
    {
        _request = request;
    }

    /// <summary>
    /// Entry point: create a builder for the given request.
    /// </summary>
    public static RequestParameterBuilder<TRequest> For(TRequest request)
        => new RequestParameterBuilder<TRequest>(request);

    /// <summary>
    /// Register an input parameter by property expression.
    /// </summary>
    public RequestParameterBuilder<TRequest> Input<TProp>(
        Expression<Func<TRequest, TProp>> propertyExpression)
    {
        string name = ExtractMemberName(propertyExpression);
        Func<TRequest, object> getter = propertyExpression.Compile()
                                                      .ConvertResultToObject();
        _inputMappings.Add((name, getter));
        return this;
    }

    /// <summary>
    /// Register an input parameter by name and value function.
    /// </summary>
    public RequestParameterBuilder<TRequest> Input(
        string name,
        Func<TRequest, object> valueFunc)
    {
        _inputMappings.Add((name, valueFunc));
        return this;
    }

    /// <summary>
    /// Register an output parameter by property expression.
    /// </summary>
    public RequestParameterBuilder<TRequest> Output<TProp>(
        Expression<Func<TRequest, TProp>> propertyExpression,
        DbType dbType)
    {
        string name = ExtractMemberName(propertyExpression);
        _outputMappings.Add((name, null, dbType, ParameterDirection.InputOutput, -1));
        return this;
    }

    /// <summary>
    /// Register an input/output parameter by property expression.
    /// </summary>
    public RequestParameterBuilder<TRequest> InputOutput<TProp>(
        Expression<Func<TRequest, TProp>> propertyExpression,
        DbType dbType)
    {
        string name = ExtractMemberName(propertyExpression);
        Func<TRequest, object> getter = propertyExpression.Compile()
                                                      .ConvertResultToObject();
        _outputMappings.Add((name, getter(_request), dbType, ParameterDirection.InputOutput, -1));
        return this;
    }

    /// <summary>
    /// Register an input/output parameter by name & property expression.
    /// </summary>
    public RequestParameterBuilder<TRequest> InputOutput<TProp>(
        string name,
        Expression<Func<TRequest, TProp>> propertyExpression,
        DbType dbType)
    {
        Func<TRequest, object> getter = propertyExpression.Compile()
                                                      .ConvertResultToObject();
        _outputMappings.Add((name, getter(_request), dbType, ParameterDirection.InputOutput, -1));
        return this;
    }

    /// <summary>
    /// Register an output parameter by explicit name.
    /// </summary>
    public RequestParameterBuilder<TRequest> Output(
        string name,
        DbType dbType)
    {
        _outputMappings.Add((name, null, dbType, ParameterDirection.InputOutput, -1));
        return this;
    }

    /// <summary>
    /// Builds DynamicParameters including both input and output mappings.
    /// </summary>
    public DynamicParameters Build(bool includeErrMsgOutput = true)
    {
        var dp = new DynamicParameters();

        // add inputs
        foreach (var (name, getter) in _inputMappings)
        {
            dp.Add($"p{name}", getter(_request));
        }

        // add outputs
        foreach (var (name, value, dbType, dir, size) in _outputMappings)
        {
            dp.Add($"p{name}", value: value, dbType: dbType, direction: dir, size: size);
        }

        if (includeErrMsgOutput)
        {
            dp.Add(
              name: "@pErrorMsg",
              dbType: DbType.String,
              direction: ParameterDirection.InputOutput,
              size: -1 // -1 means no limit for string types
            ); 
        }

        dp.Add(
            name: "@pErrorNo",
            dbType: DbType.Int32,
            direction: ParameterDirection.ReturnValue
        );

        return dp;
    }

    // Helper to extract property name from expression
    private static string ExtractMemberName<TProp>(
        Expression<Func<TRequest, TProp>> exp)
    {
        if (exp.Body is MemberExpression m)
            return m.Member.Name;
        if (exp.Body is UnaryExpression u && u.Operand is MemberExpression m2)
            return m2.Member.Name;
        throw new ArgumentException(
            "Expression must be a simple property accessor, e.g. x => x.Prop");
    }
}

// Extension to convert Func<T, TProp> into Func<T, object>
internal static class ExpressionExtensions
{
    public static Func<T, object> ConvertResultToObject<T, TProp>(
        this Func<T, TProp> func)
    {
        return t => (object)func(t);
    }
}

