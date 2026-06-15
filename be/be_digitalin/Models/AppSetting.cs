using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Models;

public class AppSetting
{
    [Key] // Jadikan Key sebagai Primary Key
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}