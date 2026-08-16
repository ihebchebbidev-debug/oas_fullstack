using System.Threading.Channels;

namespace MyApi.Modules.ExternalEndpoints.Services
{
    /// <summary>
    /// Lightweight in-memory signal channel for the webhook-forward worker.
    /// Carries job IDs that are ready RIGHT NOW so the worker doesn't have to
    /// wait for its next poll tick. Restart-safe because the durable
    /// WebhookForwardJobs table is the source of truth — anything missed in
    /// the channel will be picked up by the worker's polling loop.
    ///
    /// Registered as a singleton so producers (request handlers) and the
    /// hosted worker share the same channel.
    /// </summary>
    public interface IWebhookForwardQueue
    {
        ValueTask EnqueueAsync(int jobId, CancellationToken ct = default);
        ChannelReader<int> Reader { get; }
    }

    public class WebhookForwardQueue : IWebhookForwardQueue
    {
        private readonly Channel<int> _channel;

        public WebhookForwardQueue()
        {
            // Bounded channel: if 1024 unprocessed signals build up, drop the
            // oldest (the worker will rediscover the job via its DB poll).
            var options = new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            };
            _channel = Channel.CreateBounded<int>(options);
        }

        public ValueTask EnqueueAsync(int jobId, CancellationToken ct = default)
            => _channel.Writer.WriteAsync(jobId, ct);

        public ChannelReader<int> Reader => _channel.Reader;
    }
}
