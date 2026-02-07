namespace Brash.Compiler.Ast.Types;

public class TupleType : TypeNode
{
    public List<TypeNode> ElementTypes { get; set; } = new();

    public override string ToString() => $"({string.Join(", ", ElementTypes)})";

    public override bool Equals(object? obj)
    {
        if (obj is not TupleType other || ElementTypes.Count != other.ElementTypes.Count)
            return false;

        for (int i = 0; i < ElementTypes.Count; i++)
        {
            if (!ElementTypes[i].Equals(other.ElementTypes[i]))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var type in ElementTypes)
            hash.Add(type);
        return hash.ToHashCode();
    }
}
