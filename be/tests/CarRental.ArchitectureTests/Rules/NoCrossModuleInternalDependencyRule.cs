using Mono.Cecil;
using NetArchTest.Rules;

namespace CarRental.ArchitectureTests.Rules;

/// <summary>
/// NetArchTest custom rule enforcing that a module does not depend on another module's internal namespaces.
/// Cross-module communication is only allowed via *.Contracts and CarRental.BuildingBlocks.
/// </summary>
public sealed class NoCrossModuleInternalDependencyRule : ICustomRule
{
    private readonly string _currentModuleName;
    private readonly string[] _otherModuleNames;

    public NoCrossModuleInternalDependencyRule(string currentModuleName, IEnumerable<string> allModuleNames)
    {
        _currentModuleName = currentModuleName;
        _otherModuleNames = allModuleNames
            .Where(m => !string.Equals(m, currentModuleName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public bool MeetsRule(TypeDefinition type)
    {
        var referencedNamespaces = ExtractReferencedNamespaces(type);

        foreach (var ns in referencedNamespaces)
        {
            foreach (var other in _otherModuleNames)
            {
                var otherModuleInternalPrefix = $"CarRental.Modules.{other}";
                var otherModuleContractsPrefix = $"CarRental.Modules.{other}.Contracts";

                if (ns.StartsWith(otherModuleInternalPrefix, StringComparison.Ordinal) &&
                    !ns.StartsWith(otherModuleContractsPrefix, StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public static HashSet<string> ExtractReferencedNamespaces(TypeDefinition type)
    {
        var namespaces = new HashSet<string>(StringComparer.Ordinal);

        void AddType(TypeReference? tr)
        {
            if (tr == null) return;
            if (!string.IsNullOrEmpty(tr.Namespace))
            {
                namespaces.Add(tr.Namespace);
            }
            if (tr is GenericInstanceType git)
            {
                foreach (var arg in git.GenericArguments)
                {
                    AddType(arg);
                }
            }
        }

        AddType(type.BaseType);

        if (type.HasInterfaces)
        {
            foreach (var iface in type.Interfaces)
            {
                AddType(iface.InterfaceType);
            }
        }

        if (type.HasFields)
        {
            foreach (var field in type.Fields)
            {
                AddType(field.FieldType);
            }
        }

        if (type.HasProperties)
        {
            foreach (var prop in type.Properties)
            {
                AddType(prop.PropertyType);
            }
        }

        if (type.HasMethods)
        {
            foreach (var method in type.Methods)
            {
                AddType(method.ReturnType);
                if (method.HasParameters)
                {
                    foreach (var param in method.Parameters)
                    {
                        AddType(param.ParameterType);
                    }
                }

                if (method.HasBody)
                {
                    if (method.Body.HasVariables)
                    {
                        foreach (var variable in method.Body.Variables)
                        {
                            AddType(variable.VariableType);
                        }
                    }

                    foreach (var instr in method.Body.Instructions)
                    {
                        if (instr.Operand is TypeReference tr)
                        {
                            AddType(tr);
                        }
                        else if (instr.Operand is MethodReference mr)
                        {
                            AddType(mr.DeclaringType);
                            AddType(mr.ReturnType);
                        }
                        else if (instr.Operand is FieldReference fr)
                        {
                            AddType(fr.DeclaringType);
                            AddType(fr.FieldType);
                        }
                    }
                }
            }
        }

        if (type.HasCustomAttributes)
        {
            foreach (var attr in type.CustomAttributes)
            {
                AddType(attr.AttributeType);
            }
        }

        return namespaces;
    }
}
