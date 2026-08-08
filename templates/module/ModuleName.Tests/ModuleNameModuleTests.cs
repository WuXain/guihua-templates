using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModuleName;
using Xunit;

namespace ModuleName.Tests;

public sealed class ModuleNameModuleTests
{
    [Fact]
    public void Registration_binds_options_and_exposes_status_service()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ModuleNameOptions.SectionName}:ServiceName"] = "test-module",
                [$"{ModuleNameOptions.SectionName}:Enabled"] = "false"
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddModuleName(configuration)
            .BuildServiceProvider();

        var status = provider.GetRequiredService<ModuleNameStatus>().GetStatus();
        Assert.Equal("test-module", status.Module);
        Assert.False(status.Enabled);
    }

    [Fact]
    public void Invalid_options_are_rejected_when_validated()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{ModuleNameOptions.SectionName}:ServiceName"] = ""
            })
            .Build();
        using var provider = new ServiceCollection()
            .AddModuleName(configuration)
            .BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ModuleNameOptions>>().Value);

        Assert.Contains(nameof(ModuleNameOptions.ServiceName), exception.Message);
    }
}
