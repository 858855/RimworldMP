using System.Text.Json;

namespace Multiplayer.Foundation.Server;

public sealed class ServerConfig : HostConfig
{
    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Server configuration not found.", path);
        var config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("Empty server configuration.");
        config.Validate(false);
        return config;
    }
}
