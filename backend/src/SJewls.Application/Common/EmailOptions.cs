using System.ComponentModel.DataAnnotations;

namespace SJewls.Application.Common;

public class EmailOptions
{
    public const string SectionName = "Email";

    [Required(ErrorMessage = "Email:Host is required.")]
    public string Host { get; set; } = "smtp.gmail.com";

    [Range(1, 65535, ErrorMessage = "Email:Port must be a valid port number between 1 and 65535.")]
    public int Port { get; set; } = 587;

    [Required(ErrorMessage = "Email:Username is required.")]
    public string Username { get; set; } = "w.sageesan@gmail.com";

    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email:FromAddress is required.")]
    [EmailAddress(ErrorMessage = "Email:FromAddress must be a valid email address.")]
    public string FromAddress { get; set; } = "w.sageesan@gmail.com";

    [Required(ErrorMessage = "Email:FromName is required.")]
    public string FromName { get; set; } = "SJewls";
}
