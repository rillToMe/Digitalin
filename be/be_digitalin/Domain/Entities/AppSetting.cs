using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Domain.Entities;

public class AppSetting
{
    [Key]
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}