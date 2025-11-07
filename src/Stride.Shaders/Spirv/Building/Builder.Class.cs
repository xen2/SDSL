using Stride.Shaders.Parsing.SDSL;
using Stride.Shaders.Spirv.Core;
using Stride.Shaders.Spirv.Core.Buffers;
using System;
using System.Collections.Generic;
using System.Linq;
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
                int ltIndex = importShader.ShaderName.IndexOf('<');
                var shaderName = importShader.ShaderName;
                var shaderClassSource = new ShaderClassSource(importShader.ShaderName);
                if (ltIndex != -1)
                {
                    // Generic type: parse it
                    shaderClassSource.ClassName = shaderName.Substring(0, ltIndex);
                    shaderClassSource.GenericArguments = shaderName.Substring(ltIndex + 1).TrimEnd('>').Split(',').ToArray();
                }
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
