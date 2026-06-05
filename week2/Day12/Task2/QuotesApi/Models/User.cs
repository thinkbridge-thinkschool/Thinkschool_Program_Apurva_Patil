namespace QuotesApi.Models;

public class User
{
    private User() { }

    private User(string email, string passwordHash)
    {
        Email        = email;
        PasswordHash = passwordHash;
        CreatedAt    = DateTimeOffset.UtcNow;
    }

    public int    Id           { get; private set; }
    public string Email        { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // Static factory — same pattern as Quote.Create()
    public static (bool Success, User? User, string? Error)
        Create(string email, string plainPassword)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return (false, null, "A valid email is required.");

        if (string.IsNullOrWhiteSpace(plainPassword) || plainPassword.Length < 6)
            return (false, null, "Password must be at least 6 characters.");

        // BCrypt hashes the password — never stored as plain text
        var hash = BCrypt.Net.BCrypt.HashPassword(plainPassword);

        return (true, new User(email, hash), null);
    }

    // Verify a plain password against the stored hash
    public bool VerifyPassword(string plainPassword)
        => BCrypt.Net.BCrypt.Verify(plainPassword, PasswordHash);
}