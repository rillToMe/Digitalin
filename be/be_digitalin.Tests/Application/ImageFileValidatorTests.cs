using System.Text;
using be_digitalin.Application.Validation;
using Xunit;

namespace be_digitalin.Tests;

/// <summary>
/// Unit test ImageFileValidator — format lama tetap diterima, SVG valid
/// diterima, dan SVG berbahaya (script/event handler/foreignObject) ditolak.
/// </summary>
public class ImageFileValidatorTests
{
    private static Task AssertValidAsync(string fileName, byte[] bytes) =>
        ImageFileValidator.EnsureValidImageAsync(
            new ImageFileData(fileName, "application/octet-stream", new MemoryStream(bytes)));

    private static Task AssertInvalidAsync(string fileName, byte[] bytes) =>
        Assert.ThrowsAsync<InvalidOperationException>(
            () => AssertValidAsync(fileName, bytes));

    private static byte[] Svg(string body) =>
        Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24">{body}</svg>""");

    // ── Format biner lama (regresi) ──

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    public Task Jpeg_valid_accepted(string ext)
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        return AssertValidAsync($"logo{ext}", jpeg);
    }

    [Fact]
    public Task Png_valid_accepted()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return AssertValidAsync("logo.png", png);
    }

    [Fact]
    public Task Fake_binary_with_image_extension_rejected()
    {
        byte[] fake = "ini bukan gambar"u8.ToArray();
        return AssertInvalidAsync("fake.png", fake);
    }

    [Fact]
    public Task Unknown_extension_rejected()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        return AssertInvalidAsync("logo.gif", png);
    }

    // ── SVG ──

    [Fact]
    public Task Svg_valid_accepted()
        => AssertValidAsync("bca.svg", Svg("""<path d="M4 4h16v16H4z" fill="#00529C"/>"""));

    [Fact]
    public Task Svg_with_xml_declaration_and_bom_accepted()
    {
        byte[] svg = "\uFEFF<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray();
        return AssertValidAsync("qris.svg", svg);
    }

    [Fact]
    public Task Svg_with_doctype_accepted()
        => AssertValidAsync("gopay.svg",
            """<!DOCTYPE svg PUBLIC "-//W3C//DTD SVG 1.1//EN" "http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"><svg xmlns="http://www.w3.org/2000/svg"/>"""u8.ToArray());

    [Fact]
    public Task Svg_with_script_rejected()
        => AssertInvalidAsync("evil.svg",
            Svg("""<script>alert(1)</script><path d="M0 0"/>"""));

    [Fact]
    public Task Svg_with_event_handler_rejected()
        => AssertInvalidAsync("evil.svg",
            Svg("""<image href="x.png" onload="alert(1)"/>"""));

    [Fact]
    public Task Svg_with_javascript_uri_rejected()
        => AssertInvalidAsync("evil.svg",
            Svg("""<a xlink:href="javascript:alert(1)"><text>x</text></a>"""));

    [Fact]
    public Task Svg_with_foreign_object_rejected()
        => AssertInvalidAsync("evil.svg",
            Svg("""<foreignObject width="10" height="10"><div>x</div></foreignObject>"""));

    [Fact]
    public Task Svg_with_entity_encoded_javascript_uri_rejected()
        // Bypass klasik blocklist: java&#115;cript: — teks mentah tidak match
        // "javascript:", tapi browser decode entity saat parse. Scan kedua
        // (hasil HtmlDecode) harus menangkapnya.
        => AssertInvalidAsync("evil.svg",
            Svg("""<a xlink:href="java&#115;cript:alert(1)"><text>x</text></a>"""));

    [Fact]
    public Task Svg_with_entity_encoded_event_handler_rejected()
        => AssertInvalidAsync("evil.svg",
            Svg("""<image href="x.png" o&#110;load="alert(1)"/>"""));

    [Fact]
    public Task Svg_with_data_uri_rejected()
        => AssertInvalidAsync("evil.svg",
            Svg("""<image href="data:text/html;base64,PHNjcmlwdD4=" />"""));

    [Fact]
    public Task Svg_extension_but_not_svg_content_rejected()
        => AssertInvalidAsync("fake.svg", "ini bukan svg"u8.ToArray());

    [Fact]
    public async Task Svg_validation_resets_stream_position()
    {
        var stream = new MemoryStream(Svg("""<circle r="5"/>"""));

        await ImageFileValidator.EnsureValidImageAsync(
            new ImageFileData("a.svg", "image/svg+xml", stream));

        Assert.Equal(0, stream.Position);
    }
}
