using System.ComponentModel.DataAnnotations;

namespace be_digitalin.Config;

public class MidtransSettings
{
    [Required]
    public string ServerKey {get; set;} = string.Empty;

    [Required]
    public string BaseUrl {get; set;} = string.Empty;
}