using System.Net;

namespace SmaComplaints.Services;

/// <summary>Inline SVG icons (Lucide paths, generated into Icons.g.cs). One stroke style for the whole app.</summary>
public static partial class Icons
{
    public static IReadOnlyCollection<string> Names => Shapes.Keys;

    public static bool Exists(string name) => Shapes.ContainsKey(name);

    /// <summary>
    /// Returns an inline SVG. Decorative by default (hidden from assistive technology);
    /// pass a label when the icon is the only content of a control.
    /// An unknown name renders nothing, and SmaComplaints.Checks fails the build for it.
    /// </summary>
    public static string Svg(string name, int size = 18, string? cssClass = null, string? label = null)
    {
        if (!Shapes.TryGetValue(name, out var shape)) return "";
        var css = string.IsNullOrWhiteSpace(cssClass) ? "icon" : "icon " + cssClass;
        var a11y = string.IsNullOrWhiteSpace(label)
            ? "aria-hidden=\"true\""
            : $"role=\"img\" aria-label=\"{WebUtility.HtmlEncode(label)}\"";
        return $"<svg class=\"{css}\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\" focusable=\"false\" {a11y}>{shape}</svg>";
    }

    public static string ForRole(string role) => role switch
    {
        Models.Roles.Requestor => "role-requestor",
        Models.Roles.Owner => "role-owner",
        Models.Roles.Hod => "role-hod",
        Models.Roles.Qm => "role-qm",
        Models.Roles.Cm => "role-cm",
        _ => "role-admin",
    };
}
