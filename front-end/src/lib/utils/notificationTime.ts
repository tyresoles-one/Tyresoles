import { formatDistance, formatDistanceToNow, parseISO } from "date-fns";

/** Relative age using API clock when available (see getNotifications.serverTimeUtc). */
export function formatNotificationAge(
  createdAt: string,
  serverTimeUtc: string | null | undefined,
): string {
  if (!serverTimeUtc) {
    return formatDistanceToNow(parseISO(createdAt), { addSuffix: true });
  }
  return formatDistance(parseISO(createdAt), parseISO(serverTimeUtc), {
    addSuffix: true,
  });
}
