using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Stride.Shaders.Core;



public abstract record SymbolType()
{
    /// <summary>
    /// Converts to an identifier compatible with <see cref="Spirv.Core.SDSLOp.OpName"/>.
    /// </summary>
    /// <returns></returns>
    public virtual string ToId() => ToString();

    public static bool TryGetNumeric(string name, out SymbolType? result)
    {
        if(ScalarType.Types.TryGetValue(name, out var s))
        {
            result = s;
            return true;
        }
        else if(VectorType.Types.TryGetValue(name, out var v))
        {
            result = v;
            return true;
        }
        else if(MatrixType.Types.TryGetValue(name, out var m))
        {
            result = m;
            return true;
        }
        else if (name == "void")
        {
            result = ScalarType.From("void");
            return true;
        }
        else
        {
            result = null;
            return true;
        }
    }
}

public sealed record UndefinedType(string TypeName) : SymbolType()
{
    public override string ToString()
    {
        return TypeName;
    }
}

public sealed record PointerType(SymbolType BaseType) : SymbolType()
{
    public override string ToId() => $"ptr_{BaseType.ToId()}";
    public override string ToString() => $"*{BaseType}";
}

public sealed partial record ScalarType(string TypeName) : SymbolType()
{
    public override string ToString() => TypeName;
}
public sealed partial record VectorType(ScalarType BaseType, int Size) : SymbolType()
{    
    public override string ToString() => $"{BaseType}{Size}";
}
public sealed partial record MatrixType(ScalarType BaseType, int Rows, int Columns) : SymbolType()
{
    public override string ToString() => $"{BaseType}{Rows}x{Columns}";
}
public sealed record ArrayType(SymbolType BaseType, int Size) : SymbolType()
{
    public override string ToString() => $"{BaseType}[{Size}]";
}
public sealed record StructType(string Name, List<(string Name, SymbolType Type)> Fields) : SymbolType()
{
    public override string ToId() => Name;
    public override string ToString() => $"{Name}{{{string.Join(", ", Fields.Select(x => $"{x.Type} {x.Name}"))}}}";

    public bool TryGetFieldType(string name, [MaybeNullWhen(false)] out SymbolType type)
    {
        foreach (var field in Fields)
        {
            if (field.Name == name)
            {
                type = field.Type;
                return true;
            }
        }

        type = null;
        return false;
    }
    public int TryGetFieldIndex(string name)
    {
        for (var index = 0; index < Fields.Count; index++)
        {
            var field = Fields[index];
            if (field.Name == name)
                return index;
        }

        return -1;
    }

}
public sealed record BufferType(SymbolType BaseType, int Size) : SymbolType()
{
    public override string ToString() => $"Buffer<{BaseType}, {Size}>";
}


public abstract record TextureType(SymbolType BaseType) : SymbolType()
{
    public override string ToString() => $"Texture<{BaseType}>";
}
public sealed record Texture1DType(SymbolType BaseType, int Size) : TextureType(BaseType)
{
    public override string ToString() => $"Texture<{BaseType}, {Size}>";
}
public sealed record Texture2DType(SymbolType BaseType, int Width, int Height) : TextureType(BaseType)
{
    public override string ToString() => $"Texture<{BaseType}, {Width}, {Height}>";
}
public sealed record Texture3DType(SymbolType BaseType, int Width, int Height, int Depth) : TextureType(BaseType)
{
    public override string ToString() => $"Texture<{BaseType}, {Width}, {Height}, {Depth}>";
}


public sealed record FunctionType(SymbolType ReturnType, List<SymbolType> ParameterTypes) : SymbolType()
{
    public bool Equals(FunctionType? other)
    {
        if(other is null)
            return false;
        if (ReturnType == null || other.ReturnType == null)
            return false;
        if (ParameterTypes == null || other.ParameterTypes == null)
            return false;
        return ReturnType == other.ReturnType && ParameterTypes.SequenceEqual(other.ParameterTypes);
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash = hash * 31 + ReturnType.GetHashCode();
        foreach (var item in ParameterTypes)
        {
            hash = hash * 31 + item.GetHashCode();
        }
        return hash;
    }

    public override string ToId()
    {
        var builder = new StringBuilder();
        builder.Append($"fn_");
        for (int i = 0; i < ParameterTypes.Count; i++)
        {
            builder.Append(ParameterTypes[i].ToId());
                builder.Append('_');
        }
        return builder.Append(ReturnType.ToId()).ToString();
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append($"fn(");
        for(int i = 0; i < ParameterTypes.Count; i++)
        {
            builder.Append(ParameterTypes[i]);
            if(i < ParameterTypes.Count - 1)
                builder.Append('*');
        }
        return builder.Append($")->{ReturnType}").ToString();
    }
}

public sealed record StreamsSymbol : SymbolType;

public sealed record ConstantBufferSymbol(string Name, List<Symbol> Symbols) : SymbolType;
public sealed record ParamsSymbol(string Name, List<Symbol> Symbols) : SymbolType;
public sealed record EffectSymbol(string Name, List<Symbol> Symbols) : SymbolType;
public sealed record ShaderSymbol(string Name, List<Symbol> Components) : SymbolType
{
    public Symbol Get(string name, SymbolKind kind)
    {
        foreach (var e in Components)
            if (e.Id.Kind == kind && e.Id.Name == name)
                return e;
        throw new ArgumentException($"{name} not found in Mixin {Name}");
    }
    public bool TryGet(string name, SymbolKind kind, out Symbol? value)
    {
        foreach (var e in Components)
            if (e.Id.Kind == kind && e.Id.Name == name)
            {
                value = e;
                return true;
            }
        value = null!;
        return false;
    }
}
