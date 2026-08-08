using System.ComponentModel.DataAnnotations;

namespace ModuleName;

public sealed class ModuleNameOptions
{
    public const string SectionName = "ModuleName";

    [Required]
    public string ServiceName { get; set; } = "ModuleName";

    public bool Enabled { get; set; } = true;
}
