namespace Brash.Compiler.Ast.Types;

public class FunctionType : TypeNode
{
    public List<TypeNode> ParameterTypes { get; set; } = new();
    public TypeNode ReturnType { get; set; } = null!;

    public override string ToString() =>
        $"({string.Join(", ", ParameterTypes)}) -> {ReturnType}";

    public override bool Equals(object? obj)
    {
        if (obj is not FunctionType other || ParameterTypes.Count != other.ParameterTypes.Count)
            return false;

        for (int i = 0; i < ParameterTypes.Count; i++)
        {
            if (!ParameterTypes[i].Equals(other.ParameterTypes[i]))
                return false;
        }

        return ReturnType.Equals(other.ReturnType);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var type in ParameterTypes)
            hash.Add(type);
        hash.Add(ReturnType);
        return hash.ToHashCode();
    }
}
