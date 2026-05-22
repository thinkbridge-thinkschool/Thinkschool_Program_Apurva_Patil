namespace QuotesApi.Services;

public class GuidService : IGuidService
{
    public Guid GetGuid()
    {
        return Guid.NewGuid();
    }
}