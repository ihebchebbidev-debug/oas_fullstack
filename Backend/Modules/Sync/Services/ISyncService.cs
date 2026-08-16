using System.Threading.Tasks;
using MyApi.Modules.Sync.DTOs;

namespace MyApi.Modules.Sync.Services
{
    public interface ISyncService
    {
        Task<SyncPushResponseDto> PushAsync(SyncPushRequestDto request, string currentUser, string tenant);
        Task<SyncPullResponseDto> PullAsync(string? cursor, int limit, string currentUser, bool isAdmin);
        Task<SyncHistoryResponseDto> GetHistoryAsync(SyncHistoryQueryDto query, string currentUser, bool isAdmin);
        Task<SyncPushResultDto> RetryAsync(SyncRetryRequestDto request, string currentUser, bool isAdmin);
    }
}
