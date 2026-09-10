namespace MiddleWare.Routing;

public sealed class RelayOptions
{
    public const string SectionName = "Relay";

    public string InferenceServerBaseUrl { get; init; } = "http://localhost:8000/";

    public string InferenceServerMessagePath { get; init; } = "message";

    public string MainServerBaseUrl { get; init; } = "http://localhost:5181/";

    public string MainServerResponsePath { get; init; } = "ResponSE";

    public string ClientBaseUrl { get; init; } = "http://localhost:5091/";

    public string ClientResponsePath { get; init; } = "MainResponse";

    public string MainServerLoginPath { get; init; } = "login/client";

    public string ClientLoginResponsePath { get; init; } = "loginResponse";
}
