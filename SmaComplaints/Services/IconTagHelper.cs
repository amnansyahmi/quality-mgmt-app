using Microsoft.AspNetCore.Razor.TagHelpers;

namespace SmaComplaints.Services;

/// <summary>Renders &lt;icon name="inbox" /&gt; as an inline SVG from <see cref="Icons"/>.</summary>
[HtmlTargetElement("icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class IconTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public int Size { get; set; } = 18;
    public string? Class { get; set; }
    /// <summary>Accessible name. Only set it when the icon carries meaning that no visible text gives.</summary>
    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(Icons.Svg(Name, Size, Class, Label));
    }
}
