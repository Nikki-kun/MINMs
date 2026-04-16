namespace MINMs.Server.Options;

public class RedisOptions {
    public const string SectionName = "Redis";
    public string Endpoint { get; set; } = "localhost:6379";
}

