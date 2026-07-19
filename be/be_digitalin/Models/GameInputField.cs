namespace be_digitalin.Models;

public class GameInputField
{
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Placeholder { get; set; } = string.Empty;

    public string Type { get; set; } = "text";

    public bool Required { get; set; } = true;

    public int Order { get; set; }

    public List<string> Options { get; set; } = [];
}