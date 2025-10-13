using Microsoft.Extensions.Configuration;

namespace RmqCli.Infrastructure.Configuration;

public class TomlConfigurationSource : IConfigurationSource
{
    public string FilePath { get; set; } = string.Empty;
    
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new TomlConfigurationProvider(FilePath);
    }
}

public static class TomlConfigurationSourceExtensions
{
    public static IConfigurationBuilder AddTomlConfig(this IConfigurationBuilder builder, string filePath)
    {
        return builder.Add(new TomlConfigurationSource { FilePath = filePath });
    }
}