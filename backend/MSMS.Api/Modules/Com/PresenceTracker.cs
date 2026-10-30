using System.Collections.Concurrent;

namespace MSMS.Api.Modules.Com;

/// <summary>
/// COM-4.8.8: online khi tài khoản còn ít nhất một kết nối, offline khi mất hết (nhiều tab, nhiều thiết bị).
/// Chỉ giữ trong bộ nhớ vì chạy một instance (không backplane, không bảng CSDL).
/// </summary>
public sealed class PresenceTracker
{
    private readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();

    /// <summary>Trả true nếu đây là kết nối đầu tiên của người này (vừa chuyển sang online).</summary>
    public bool Connected(int personId, string connectionId)
    {
        var set = _connections.GetOrAdd(personId, _ => []);
        lock (set)
        {
            set.Add(connectionId);
            return set.Count == 1;
        }
    }

    /// <summary>Trả true nếu đây là kết nối cuối cùng của người này (vừa chuyển sang offline).</summary>
    public bool Disconnected(int personId, string connectionId)
    {
        if (!_connections.TryGetValue(personId, out var set)) return false;
        lock (set)
        {
            set.Remove(connectionId);
            if (set.Count > 0) return false;
        }
        _connections.TryRemove(personId, out _);
        return true;
    }

    public bool IsOnline(int personId) => _connections.TryGetValue(personId, out var set) && set.Count > 0;
}
