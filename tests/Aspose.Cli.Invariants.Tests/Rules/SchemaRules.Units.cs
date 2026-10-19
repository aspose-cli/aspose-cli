using System.Reflection;
using System.Text.Json.Serialization;
using Aspose.Cli.Sdk.Extensibility;
using Aspose.Cli.Sdk.Operations;
using Aspose.Cli.TestKit;

namespace Aspose.Cli.Invariants.Tests;

// C-R20: a field measured in a unit other than points names its unit (widthChars, scalePercent,
// durationMs, dpi). A schema does not say which number is a measure, so each measured op field
// declares its unit where it is declared, with [Measured]; the schema does not publish it, so the
// rule reads the declarations from the product assemblies beside the CLI under test. A field that
// declares no unit is judged only where its definition declares one for another field: a name that
// ends in a unit, or one measured in another definition, there declares its unit too.
internal static partial class ContractRules
{
    /// <summary>The name ending each non-point unit requires.</summary>
    private static readonly Dictionary<MeasureUnit, string> UnitSuffixes = new()
    {
        [MeasureUnit.Characters] = "Chars",
        [MeasureUnit.Dpi] = "Dpi",
        [MeasureUnit.Inches] = "Inches",
        [MeasureUnit.Milliseconds] = "Ms",
        [MeasureUnit.Percent] = "Percent",
        [MeasureUnit.Pixels] = "Pixels",
    };

    private static readonly Lazy<IReadOnlyDictionary<(string Schema, string Definition, string Field), MeasureUnit>> Measured =
        new(ReadMeasuredFields);

    /// <summary>The measured op fields by ops schema, definition and field, with the unit each declares.</summary>
    private static IReadOnlyDictionary<(string Schema, string Definition, string Field), MeasureUnit> MeasuredFields => Measured.Value;

    /// <summary>
    /// The measured fields of one ops definition that do not name their non-point unit, declarations
    /// the schema no longer has a field for, and fields of the definition that look measured but
    /// declare no unit.
    /// </summary>
    private static IEnumerable<string> UnitNames(string schemaId, string definition)
    {
        var declared = PublishedSchema.Definition(schemaId, definition)["properties"]?.AsObject();
        foreach (var ((schema, owner, field), unit) in MeasuredFields.OrderBy(static pair => pair.Key.Field, StringComparer.Ordinal))
        {
            if (schema != schemaId || owner != definition)
            {
                continue;
            }
            if (declared?[field] is null)
            {
                yield return $"{field}: declared measured in {Unit(unit)} but the schema has no such field";
                continue;
            }
            if (unit != MeasureUnit.Points && !field.EndsWith(UnitSuffixes[unit], StringComparison.Ordinal)
                && !string.Equals(field, UnitSuffixes[unit], StringComparison.OrdinalIgnoreCase))
            {
                yield return $"{field}: measured in {Unit(unit)}; name it *{UnitSuffixes[unit]}";
            }
        }

        foreach (string field in (declared ?? []).Select(static pair => pair.Key).Order(StringComparer.Ordinal))
        {
            if (MeasuredFields.ContainsKey((schemaId, definition, field)))
            {
                continue;
            }
            foreach ((MeasureUnit unit, string suffix) in UnitSuffixes)
            {
                if (field.EndsWith(suffix, StringComparison.Ordinal) || string.Equals(field, suffix, StringComparison.OrdinalIgnoreCase))
                {
                    yield return $"{field}: named for {Unit(unit)} but declares no unit; declare [Measured(MeasureUnit.{unit})]";
                }
            }
            string[] elsewhere = [.. MeasuredFields.Keys.Where(key => key.Field == field)
                .Select(static key => $"{key.Schema}#{key.Definition}").Order(StringComparer.Ordinal)];
            if (elsewhere.Length != 0)
            {
                yield return $"{field}: declares no unit, though {string.Join(", ", elsewhere)} declares one for it; declare [Measured]";
            }
        }
    }

    private static string Unit(MeasureUnit unit) => unit.ToString().ToLowerInvariant();

    /// <summary>
    /// The [Measured] declarations of every product assembly in the output of the CLI under test.
    /// An operation is its <c>$defs</c> entry by its wire name and a nested record by its type's
    /// camelCase name, as the operation generator names them; a field by its wire name.
    /// </summary>
    private static IReadOnlyDictionary<(string Schema, string Definition, string Field), MeasureUnit> ReadMeasuredFields()
    {
        string output = Path.GetDirectoryName(CliRunner.ExecutablePath)!;
        var fields = new Dictionary<(string Schema, string Definition, string Field), MeasureUnit>();
        string[] products = [.. Directory.GetFiles(output, "Aspose.Cli.Product.*.dll").Order(StringComparer.Ordinal)];
        if (products.Length == 0)
        {
            throw new InvalidOperationException($"No product assembly lies beside {CliRunner.ExecutablePath}, so the C-R20 unit declarations cannot be read.");
        }

        foreach (string path in products)
        {
            Assembly assembly = Assembly.LoadFrom(path);
            string product = assembly.GetCustomAttribute<ProductModuleAttribute>()?.ProductId
                ?? throw new InvalidOperationException($"{Path.GetFileName(path)} declares no product module.");
            Type[] types = Loadable(assembly);
            string[] vocabularies = [.. types.Select(static type => type.GetCustomAttribute<OperationVocabularyAttribute>()?.SchemaId)
                .OfType<string>().Distinct(StringComparer.Ordinal)];
            foreach (Type type in types.Where(static type => type.IsClass && (!type.IsAbstract || type.IsDefined(typeof(OperationAttribute), false))))
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetCustomAttribute<MeasuredAttribute>() is not { } measured)
                    {
                        continue;
                    }
                    if (vocabularies.Length != 1)
                    {
                        throw new InvalidOperationException($"{type.FullName}.{property.Name} declares a unit, but {product} has {vocabularies.Length} operation vocabularies, so its schema is not known.");
                    }
                    string definition = type.GetCustomAttribute<OperationAttribute>(false)?.Name ?? CamelCase(type.Name);
                    string field = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? CamelCase(property.Name);
                    fields[($"v2/{product}/{vocabularies[0]}", definition, field)] = measured.Unit;
                }
            }
        }

        return fields;
    }

    private static Type[] Loadable(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return [.. exception.Types.OfType<Type>()];
        }
    }

    private static string CamelCase(string name) => char.ToLowerInvariant(name[0]) + name[1..];
}
