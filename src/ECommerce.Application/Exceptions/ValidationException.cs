namespace ECommerce.Application.Exceptions;

public class ValidationException : Exception
{
    public Dictionary<string, string[]> Errors { get; }

    public ValidationException(Dictionary<string, string[]> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error) : base("Validation failed")
    {
        Errors = new Dictionary<string, string[]>
        {
            { field, new[] { error } }
        };
    }

    public ValidationException(string field, string[] errors) : base("Validation failed")
    {
        Errors = new Dictionary<string, string[]>
        {
            { field, errors }
        };
    }
}