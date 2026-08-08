using Microsoft.Extensions.Options;

namespace ModuleName;

public sealed class ModuleNameStatus(IOptions<ModuleNameOptions> options)
{
    public ModuleNameStatusResponse GetStatus() =>
        new(options.Value.ServiceName, options.Value.Enabled);
}

public sealed record ModuleNameStatusResponse(string Module, bool Enabled);
