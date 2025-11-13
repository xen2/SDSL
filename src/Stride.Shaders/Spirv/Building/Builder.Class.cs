using Stride.Shaders.Parsing.SDSL;
using Stride.Shaders.Spirv.Core;
using Stride.Shaders.Spirv.Core.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Stride.Shaders.Spirv.Specification;
using static System.Net.Mime.MediaTypeNames;

namespace Stride.Shaders.Spirv.Building;
public partial class SpirvBuilder
{
    public static void BuildInheritanceList(IExternalShaderLoader shaderLoader, NewSpirvBuffer buffer, List<ShaderClassSource> inheritanceList)
    {
        // Build shader name mapping
        var shaderMapping = new Dictionary<int, ShaderClassSource>();
        foreach (var i in buffer)
        {
            if (i.Op == Specification.Op.OpSDSLImportShader && (OpSDSLImportShader)i is { } importShader)
            {
                var shaderClassSource = ConvertToShaderClassSource(buffer, 0, buffer.Count, importShader);

                shaderMapping[importShader.ResultId] = shaderClassSource;
            }
        }

        // Check inheritance
        foreach (var i in buffer)
        {
            if (i.Op == Specification.Op.OpSDSLMixinInherit && (OpSDSLMixinInherit)i is { } inherit)
            {
                var shaderName = shaderMapping[inherit.Shader];
                BuildInheritanceList(shaderLoader, shaderName, inheritanceList);
            }
        }
    }

    internal static string ResolveConstant(NewSpirvBuffer buffer, int shaderStart, int shaderEnd, int id)
    {
        for (var index = shaderStart; index < shaderEnd; index++)
        {
            var i = buffer[index];
            // TODO: some way to query type before casting?
            // TODO: cast is broken
            if (i.Op == Op.OpConstant) // && (OpConstant<float>)i is { } constant)
            {
                if (i.Data.IdResult == id)
                {
                    var value = new LiteralValue<float>(i.Data.Memory.Span[3..]);
                    return value.Value.ToString() + "f32";
                }
            }
        }

        throw new InvalidOperationException($"Constant {id} not found or its opcode could not be processed");
    }

    public static ShaderClassSource ConvertToShaderClassSource(NewSpirvBuffer buffer, int shaderStart, int shaderEnd, OpSDSLImportShader importShader)
    {
        var shaderClassSource = new ShaderClassSource(importShader.ShaderName);

        if (importShader.Values.Elements.Length > 0)
        {
            var genericArguments = new string[importShader.Values.Elements.Length];
            var genericArgumentIndex = 0;
            foreach (var element in importShader.Values)
            {
                // Resolve constant
                // TODO: single pass, avoid O(n^2) and OpSpecConstantOp
                genericArguments[genericArgumentIndex++] = ResolveConstant(buffer, shaderStart, shaderEnd, element);
            }
            shaderClassSource.GenericArguments = genericArguments;
        }

        return shaderClassSource;
    }

    public static void BuildInheritanceList(IExternalShaderLoader shaderLoader, ShaderClassSource classSource, List<ShaderClassSource> inheritanceList)
    {
        if (!inheritanceList.Contains(classSource))
        {
            var shader = GetOrLoadShader(shaderLoader, classSource);

            BuildInheritanceList(shaderLoader, shader, inheritanceList);
            inheritanceList.Add(classSource);
        }
    }

    public static NewSpirvBuffer InstantiateGenericShader(NewSpirvBuffer shader, string[] genericArguments)
    {
        // Instantiate generics
        var copiedShader = new NewSpirvBuffer();
        foreach (var i in shader)
        {
            var i2 = new OpData(i.Data.Memory.Span);
            copiedShader.Add(i2);
        }
        shader = copiedShader;

        var generics = new List<int>();
        var genericArgumentIndex = 0;
        for (var index = 0; index < shader.Count; index++)
        {
            var i = shader[index];
            if (i.Op == Op.OpSDSLShader && (OpSDSLShader)i is { } shaderDeclaration)
            {
                shaderDeclaration.ShaderName = shaderDeclaration.ShaderName + "<" + string.Join(',', genericArguments) + ">";
            }
            else if (i.Op == Op.OpSDSLGenericParameter && (OpSDSLGenericParameter)i is {} genericParameter)
            {
                var genericArgument = genericArguments[genericArgumentIndex++];
                if (genericArgument.EndsWith("f32"))
                {
                    var floatValue = float.Parse(genericArgument.Substring(0, genericArgument.Length - "f32".Length));
                    shader.Replace(index, new OpConstant<float>(genericParameter.ResultType, genericParameter.ResultId, floatValue));
                }
                else
                {
                    throw new NotImplementedException($"Unknown type for generic argument with value {genericArgument}");
                }
            }
        }

        return shader;
    }

    public static void SetOpNop(Span<int> words)
    {
        words[0] = words.Length << 16;
        words[1..].Clear();
    }

    public static NewSpirvBuffer GetOrLoadShader(IExternalShaderLoader shaderLoader, ShaderClassSource classSource)
    {
        var shader = GetOrLoadShader(shaderLoader, classSource.ClassName);

        if (classSource.GenericArguments.Length > 0)
        {
            shader = InstantiateGenericShader(shader, classSource.GenericArguments);
        }

        return shader;
    }

    private static NewSpirvBuffer GetOrLoadShader(IExternalShaderLoader shaderLoader, string className)
    {
        if (!shaderLoader.LoadExternalBuffer(className, out var buffer))
            throw new InvalidOperationException($"Could not load shader [{className}]");

        return buffer;
    }
}
