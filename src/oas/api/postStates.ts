import { apiFetch } from './client';

export interface OasPostStateDto {
  postId: string; state: string; since: string;
  activeEventId: string | null; currentUserId: string | null; currentProductId: string | null;
}
export interface OasPostStateHistoryDto { state: string; startedAt: string; endedAt: string | null; durationSec: number | null }

export const postStatesApi = {
  live: () => apiFetch<OasPostStateDto[]>('/post-states'),
  history: (postId: string) => apiFetch<OasPostStateHistoryDto[]>(`/post-states/${postId}/history`),
};
