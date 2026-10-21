namespace SmartSchool.Website.Api.DTOs;

/// <summary>
/// Payload submitted from the website contact/demo form.
/// </summary>
public class CreateContactSubmissionRequest
{
    private string? _name;

    public string? Name
    {
        get => _name;
        set => _name = value;
    }

    /// <summary>
    /// Alias for Name to support frontend payloads using fullName.
    /// </summary>
    public string? FullName
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                _name = value;
            }
        }
    }

    public string? SchoolName { get; set; }

    public string? PhoneNumber { get; set; }

    public string? WorkEmail { get; set; }

    public string? Message { get; set; }
}
