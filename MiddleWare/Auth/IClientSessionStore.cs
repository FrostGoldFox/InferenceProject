using System.Diagnostics.CodeAnalysis;

namespace MiddleWare.Auth;

public interface IClientSessionStore
{
    void Save(ClientSession session);

    bool TryGetByToken(string token, [MaybeNullWhen(false)] out ClientSession session);

    bool TryGetByClientNo(string clientNo, [MaybeNullWhen(false)] out ClientSession session);
}
