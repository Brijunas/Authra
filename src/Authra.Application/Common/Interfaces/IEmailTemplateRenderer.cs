namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Renders email templates with model data.
/// </summary>
public interface IEmailTemplateRenderer
{
    /// <summary>
    /// Renders an email template with the provided model.
    /// </summary>
    /// <param name="templateName">Name of the template (e.g., "password-reset", "tenant-invite")</param>
    /// <param name="model">Model data to inject into the template</param>
    /// <returns>Rendered HTML content</returns>
    Task<string> RenderAsync(string templateName, object model);
}
