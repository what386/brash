namespace Brash.Compiler.Ast;

using Brash.Compiler.Ast.Statements;

// ============================================
// Type System
// ============================================

public abstract class TypeNode : AstNode { }

public class PrimitiveType : TypeNode
{
    public enum Kind { Int, Float, String, Bool, Char, Void }

    public Kind PrimitiveKind { get; set; }

    public override string ToString() => PrimitiveKind.ToString().ToLower();

    public override bool Equals(object? obj)
    {
        return obj is PrimitiveType other && PrimitiveKind == other.PrimitiveKind;
    }

    public override int GetHashCode() => PrimitiveKind.GetHashCode();
}

public class ArrayType : TypeNode
{
    public TypeNode ElementType { get; set; } = null!;

    public override string ToString() => $"{ElementType}[]";

    public override bool Equals(object? obj)
    {
        return obj is ArrayType other && ElementType.Equals(other.ElementType);
    }

    public override int GetHashCode() => HashCode.Combine(ElementType, "[]");
}

public class MapType : TypeNode
{
    public TypeNode KeyType { get; set; } = null!;
    public TypeNode ValueType { get; set; } = null!;

    public override string ToString() => $"map<{KeyType}, {ValueType}>";

    public override bool Equals(object? obj)
    {
        return obj is MapType other &&
               KeyType.Equals(other.KeyType) &&
               ValueType.Equals(other.ValueType);
    }

    public override int GetHashCode() => HashCode.Combine(KeyType, ValueType);
}

public class NullableType : TypeNode
{
    public TypeNode BaseType { get; set; } = null!;

    public override string ToString() => $"{BaseType}?";

    public override bool Equals(object? obj)
    {
        return obj is NullableType other && BaseType.Equals(other.BaseType);
    }

    public override int GetHashCode() => HashCode.Combine(BaseType, "?");
}

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

public class NamedType : TypeNode
{
    public string Name { get; set; } = string.Empty;

    public override string ToString() => Name;

    public override bool Equals(object? obj)
    {
        return obj is NamedType other && Name == other.Name;
    }

    public override int GetHashCode() => Name.GetHashCode();
}

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

public class UnknownType : TypeNode
{
    public override string ToString() => "<unknown>";

    public override bool Equals(object? obj) => obj is UnknownType;

    public override int GetHashCode() => typeof(UnknownType).GetHashCode();
}
