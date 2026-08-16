using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;

namespace MyApi.Modules.OAS.Common.Realtime;

/// <summary>
/// Backs GET /api/oas/stream (spec §3, §6.3): the ONLY realtime channel in
/// OAS (no SignalR hub — spec §3.2 forbids publishing on the socle's
/// /hubs/workflow). Channels are keyed by "{slug}:{tenantId}" so a
/// demooas event can never reach a krossieroas subscriber even though both
/// share process memory in a single-instance deployment (spec §1.2 bis
/// point 8).
///
/// Single-instance only: subscriber lists live in process memory. If this
/// deployment ever runs more than one instance, this broadcaster needs a
/// backplane (Redis pub/sub — REDIS_URL is already available, spec §1.1) so
/// every instance's subscribers receive every publish, not just the one
/// that happened to handle the publishing request.
/// </summary>
public interface IOasSseBroadcaster
{
    Guid Subscribe(string oasSlug, int tenantId, Channel<string> channel);
    void Unsubscribe(Guid subscriptionId);
    void Publish(string oasSlug, int tenantId, string eventType, object payload);
}

public class OasSseBroadcaster : IOasSseBroadcaster
{
    private sealed record Subscription(string ChannelKey, Channel<string> Channel);

    private readonly ConcurrentDictionary<Guid, Subscription> _subscriptions = new();

    private static string ChannelKey(string oasSlug, int tenantId) => $"{oasSlug}:{tenantId}";

    public Guid Subscribe(string oasSlug, int tenantId, Channel<string> channel)
    {
        var id = Guid.NewGuid();
        _subscriptions[id] = new Subscription(ChannelKey(oasSlug, tenantId), channel);
        return id;
    }

    public void Unsubscribe(Guid subscriptionId) => _subscriptions.TryRemove(subscriptionId, out _);

    public void Publish(string oasSlug, int tenantId, string eventType, object payload)
    {
        var key = ChannelKey(oasSlug, tenantId);
        var envelope = JsonSerializer.Serialize(new { type = eventType, data = payload });

        foreach (var (id, sub) in _subscriptions)
        {
            if (sub.ChannelKey != key) continue;
            // Drop instead of block: a slow/stalled subscriber must never
            // back-pressure a publish for every other tenant sharing this
            // process.
            if (!sub.Channel.Writer.TryWrite(envelope))
            {
                Unsubscribe(id);
            }
        }
    }
}
