using Microsoft.Extensions.Configuration;
using RmqCli.Shared;
using Tomlyn;

namespace RmqCli.Infrastructure.Configuration;

public class TomlConfigurationProvider : ConfigurationProvider
{
    private readonly string _filePath;

    public TomlConfigurationProvider(string filePath)
    {
        _filePath = filePath;
    }


    public override void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var tomlContent = File.ReadAllText(_filePath);
            var tomlTable = Toml.ToModel(tomlContent);

            Data = FlattenTomlTable(tomlTable);
        }
        catch (UnauthorizedAccessException e)
        {
            // If TOML parsing fails, just skip this configuration source
            Data = new Dictionary<string, string?>();
            Console.Error.WriteLine(
                $"{Constants.WarningSymbol} Access denied to toml file '{_filePath}'. Please check the file permissions. Config will be skipped.");
        }
        catch (TomlException e)
        {
            Data = new Dictionary<string, string?>();
            Console.Error.WriteLine($"{Constants.WarningSymbol} Failed to read toml file '{_filePath}'"); 
            Console.Error.WriteLine(e.Message);
            Console.Error.WriteLine("  Config will be skipped");
        } 
        catch (Exception)
        {
            // If TOML parsing fails, just skip this configuration source
            Data = new Dictionary<string, string?>();
            Console.Error.WriteLine($"{Constants.WarningSymbol} Failed to read toml file '{_filePath}'. Please check the file format and ensure it is valid TOML.");
            Console.Error.WriteLine("  Config will be skipped");
        }
    }

    /// <summary>
    /// Flatten a TOML table into a dictionary with dot notation for nested keys. Does not handle arrays.
    /// </summary>
    /// <param name="value">Value to flatten, typically a TomlTable object</param>
    /// <param name="prefix">Key of the parent object</param>
    /// <returns></returns>
    private static IDictionary<string, string?> FlattenTomlTable(object value, string prefix = "")
    {
        var result = new Dictionary<string, string?>();

        if (value is IDictionary<string, object> table)
        {
            foreach (var kvp in table)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}:{kvp.Key}";

                if (kvp.Value is IDictionary<string, object>)
                {
                    var nested = FlattenTomlTable(kvp.Value, key);
                    foreach (var nestedKvp in nested)
                    {
                        result[nestedKvp.Key] = nestedKvp.Value;
                    }
                }
                else
                {
                    result[key] = kvp.Value.ToString() ?? string.Empty;
                }
            }
        }

        return result;
    }
}