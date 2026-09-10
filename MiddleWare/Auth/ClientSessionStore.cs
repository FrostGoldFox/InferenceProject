using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace MiddleWare.Auth;

// 임시 인메모리 저장소. 프로세스 재시작 시 세션 유실됨 (TODO: 영속 저장소 여부 팀과 협의).
public sealed class ClientSessionStore : IClientSessionStore
{
    private readonly ConcurrentDictionary<string, ClientSession> _byToken = new();
    private readonly ConcurrentDictionary<string, ClientSession> _byClientNo = new();

    public void Save(ClientSession session)
    {
        _byToken[session.Token] = session;
        _byClientNo[session.ClientNo] = session;
    }

    public bool TryGetByToken(string token, [MaybeNullWhen(false)] out ClientSession session) =>
        _byToken.TryGetValue(token, out session);

    public bool TryGetByClientNo(string clientNo, [MaybeNullWhen(false)] out ClientSession session) =>
        _byClientNo.TryGetValue(clientNo, out session);
}
