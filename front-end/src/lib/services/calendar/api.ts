/**
 * Calendar API - calls GraphQL calendar endpoints.
 * Uses inline documents so the app works without running codegen.
 */

import { graphqlQuery, graphqlMutation } from '$lib/services/graphql/client';

export type EventTagType = 'USER' | 'CUSTOMER' | 'VENDOR' | 'TOPIC';
export type ReminderChannel = 'IN_APP' | 'EMAIL' | 'PUSH';


export interface CalendarEventDto {
  id: string;
  ownerUserId: string;
  eventTypeId?: number | null;
  eventTypeName?: string | null;
  eventTypeColor?: string | null;
  title: string;
  description?: string | null;
  startUtc: string;
  endUtc: string;
  isAllDay: boolean;
  timeZoneId?: string | null;
  location?: string | null;
  meetingLink?: string | null;
  visibility?: number;
  showAs?: number;
  status?: number;
  recurrenceRuleId?: string | null;
  parentEventId?: string | null;
  exceptionOccurrenceStartUtc?: string | null;
  tags: { tagType: EventTagType; tagKey: string; displayName?: string | null }[];
  reminders: {
    id: string;
    remindAtUtc: string;
    channel: ReminderChannel;
    isSent: boolean;
    snoozeUntilUtc?: string | null;
  }[];
  attendees?: { id: string; userId: string; email?: string | null; response: number; isRequired: boolean; respondedAt?: string | null }[];
  recurrence?: {
    frequency: number;
    interval: number;
    daysOfWeek?: string | null;
    endByDate?: string | null;
    occurrenceCount?: number | null;
  } | null;
  tasks: CalendarTaskDto[];
}

export interface CalendarTaskDto {
  id: string;
  parentTaskId?: string | null;
  title: string;
  isCompleted: boolean;
  sortOrder: number;
  subTasks: CalendarTaskDto[];
}

/** 0=All, 1=ThisOccurrence, 2=ThisAndFuture */
export type UpdateScope = 0 | 1 | 2;

export interface EventTypeDto {
  id: number;
  name: string;
  color?: string | null;
  icon?: string | null;
  sortOrder: number;
}

export interface CreateEventInput {
  title: string;
  description?: string | null;
  startUtc: string;
  endUtc: string;
  isAllDay?: boolean;
  timeZoneId?: string | null;
  location?: string | null;
  meetingLink?: string | null;
  visibility?: number | null;
  showAs?: number | null;
  status?: number | null;
  eventTypeId?: number | null;
  attendees?: { userId: string; isRequired: boolean }[] | null;
  recurrence?: {
    frequency: number;
    interval: number;
    daysOfWeek?: string | null;
    dayOfMonth?: number | null;
    monthOfYear?: number | null;
    endByDate?: string | null;
    occurrenceCount?: number | null;
    rRule?: string | null;
  } | null;
  tags?: { tagType: number; tagKey: string }[] | null;
  reminders?: { remindAtUtc: string; channel?: ReminderChannel }[] | null;
  tasks?: CalendarTaskInput[] | null;
}

export interface UpdateEventInput {
  title?: string | null;
  description?: string | null;
  startUtc?: string | null;
  endUtc?: string | null;
  isAllDay?: boolean | null;
  timeZoneId?: string | null;
  location?: string | null;
  meetingLink?: string | null;
  visibility?: number | null;
  showAs?: number | null;
  status?: number | null;
  eventTypeId?: number | null;
  recurrence?: CreateEventInput['recurrence'];
  tags?: { tagType: number; tagKey: string }[] | null;
  reminders?: { remindAtUtc: string; channel?: ReminderChannel }[] | null;
  attendees?: { userId: string; isRequired: boolean }[] | null;
  tasks?: CalendarTaskInput[] | null;
}

export interface CalendarTaskInput {
  id?: string | null;
  parentTaskId?: string | null;
  title: string;
  isCompleted: boolean;
  sortOrder: number;
  subTasks?: CalendarTaskInput[] | null;
}

export interface NotificationPreferenceDto {
  channel: ReminderChannel;

  defaultMinutesBefore: number;
  emailEnabled: boolean;
  pushEnabled: boolean;
}

export interface CalendarShareDto {
  id: string;
  ownerUserId: string;
  sharedWithUserId: string;
  permission: number;
  createdAt: string;
}

const eventsFragment = `
  id ownerUserId eventTypeId eventTypeName eventTypeColor title description
  startUtc endUtc isAllDay timeZoneId location meetingLink visibility showAs status
  recurrenceRuleId parentEventId exceptionOccurrenceStartUtc
  tags { tagType tagKey displayName }
  reminders { id remindAtUtc channel isSent snoozeUntilUtc }
  attendees { id userId email response isRequired respondedAt }
  tasks { 
    id parentTaskId title isCompleted sortOrder 
    subTasks { 
      id parentTaskId title isCompleted sortOrder 
      subTasks { id parentTaskId title isCompleted sortOrder }
    } 
  }
`;

export async function getMyCalendarEvents(
  fromUtc: Date,
  toUtc: Date,
  tagType?: EventTagType | null,
  tagKey?: string | null
) {
  const res = await graphqlQuery<{ getMyCalendarEvents: CalendarEventDto[] }>(
    `query GetMyCalendarEvents($fromUtc: DateTime!, $toUtc: DateTime!, $tagType: EventTagType, $tagKey: String) {
      getMyCalendarEvents(fromUtc: $fromUtc, toUtc: $toUtc, tagType: $tagType, tagKey: $tagKey) {
        ${eventsFragment}
      }
    }`,
    {
      variables: {
        fromUtc: fromUtc.toISOString(),
        toUtc: toUtc.toISOString(),
        tagType: tagType ?? undefined,
        tagKey: tagKey ?? undefined,
      },
      skipCache: true,
    }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getMyCalendarEvents ?? [];
}

export async function getCalendarEventById(eventId: string) {
  const res = await graphqlQuery<{ getCalendarEventById: CalendarEventDto | null }>(
    `query GetCalendarEventById($eventId: UUID!) {
      getCalendarEventById(eventId: $eventId) {
        ${eventsFragment}
        recurrence { frequency interval daysOfWeek dayOfMonth monthOfYear endByDate occurrenceCount rRule }
      }
    }`,
    { variables: { eventId }, skipCache: true }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getCalendarEventById ?? null;
}

export async function getEventTypes() {
  const res = await graphqlQuery<{ getEventTypes: EventTypeDto[] }>(
    `query GetEventTypes { getEventTypes { id name color icon sortOrder } }`,
    { cacheKey: 'calendar:eventTypes', cacheTTL: 60_000 }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getEventTypes ?? [];
}

export async function getUpcomingReminders(untilUtc?: Date) {
  const res = await graphqlQuery<{ getUpcomingReminders: CalendarEventDto[] }>(
    `query GetUpcomingReminders($untilUtc: DateTime) {
      getUpcomingReminders(untilUtc: $untilUtc) {
        id title startUtc endUtc
        reminders { id remindAtUtc channel snoozeUntilUtc }
      }
    }`,
    {
      variables: { untilUtc: (untilUtc ?? new Date(Date.now() + 24 * 60 * 60 * 1000)).toISOString() },
      skipCache: true,
    }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getUpcomingReminders ?? [];
}

export async function createCalendarEvent(input: CreateEventInput) {
  const res = await graphqlMutation<{ createCalendarEvent: CalendarEventDto | null }>(
    `mutation CreateCalendarEvent($input: CreateEventInput!) {
      createCalendarEvent(input: $input) { id title startUtc endUtc eventTypeId eventTypeName eventTypeColor tags { tagType tagKey displayName } reminders { id remindAtUtc channel } }
    }`,
    { variables: { input } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.createCalendarEvent ?? null;
}

export async function updateCalendarEvent(
  eventId: string,
  input: UpdateEventInput,
  updateScope: UpdateScope = 0,
  occurrenceStartUtc?: string | null
) {
  const res = await graphqlMutation<{ updateCalendarEvent: CalendarEventDto | null }>(
    `mutation UpdateCalendarEvent($eventId: UUID!, $input: UpdateEventInput!, $updateScope: Int, $occurrenceStartUtc: DateTime) {
      updateCalendarEvent(eventId: $eventId, input: $input, updateScope: $updateScope, occurrenceStartUtc: $occurrenceStartUtc) { id title startUtc endUtc eventTypeId eventTypeName tags { tagType tagKey displayName } reminders { id remindAtUtc channel } attendees { id userId response } }
    }`,
    { variables: { eventId, input, updateScope, occurrenceStartUtc: occurrenceStartUtc ?? undefined } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.updateCalendarEvent ?? null;
}

export async function deleteCalendarEvent(
  eventId: string,
  soft = true,
  deleteScope: UpdateScope = 0,
  occurrenceStartUtc?: string | null
) {
  const res = await graphqlMutation<{ deleteCalendarEvent: boolean }>(
    `mutation DeleteCalendarEvent($eventId: UUID!, $soft: Boolean, $deleteScope: Int, $occurrenceStartUtc: DateTime) {
      deleteCalendarEvent(eventId: $eventId, soft: $soft, deleteScope: $deleteScope, occurrenceStartUtc: $occurrenceStartUtc)
    }`,
    { variables: { eventId, soft, deleteScope, occurrenceStartUtc: occurrenceStartUtc ?? undefined } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.deleteCalendarEvent ?? false;
}

export async function snoozeReminder(reminderId: string, snoozeUntilUtc: Date) {
  const res = await graphqlMutation<{ snoozeReminder: boolean }>(
    `mutation SnoozeReminder($reminderId: UUID!, $snoozeUntilUtc: DateTime!) {
      snoozeReminder(reminderId: $reminderId, snoozeUntilUtc: $snoozeUntilUtc)
    }`,
    { variables: { reminderId, snoozeUntilUtc: snoozeUntilUtc.toISOString() } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.snoozeReminder ?? false;
}

export async function getSharedCalendars() {
  const res = await graphqlQuery<{ getSharedCalendars: CalendarShareDto[] }>(
    `query GetSharedCalendars { getSharedCalendars { id ownerUserId sharedWithUserId permission createdAt } }`,
    { skipCache: true }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getSharedCalendars ?? [];
}

export async function shareCalendar(sharedWithUserId: string, permission: number) {
  const res = await graphqlMutation<{ shareCalendar: boolean }>(
    `mutation ShareCalendar($sharedWithUserId: String!, $permission: Int!) {
      shareCalendar(sharedWithUserId: $sharedWithUserId, permission: $permission)
    }`,
    { variables: { sharedWithUserId, permission } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.shareCalendar ?? false;
}

export async function respondToInvite(eventId: string, response: number) {
  const res = await graphqlMutation<{ respondToInvite: boolean }>(
    `mutation RespondToInvite($eventId: UUID!, $response: Int!) {
      respondToInvite(eventId: $eventId, response: $response)
    }`,
    { variables: { eventId, response } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.respondToInvite ?? false;
}

export async function getNotificationPreference() {
  const res = await graphqlQuery<{ getNotificationPreference: NotificationPreferenceDto | null }>(
    `query GetNotificationPreference { getNotificationPreference { channel defaultMinutesBefore emailEnabled pushEnabled } }`,
    { skipCache: true }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getNotificationPreference ?? null;
}

export async function setNotificationPreference(input: NotificationPreferenceDto) {
  const res = await graphqlMutation<{ setNotificationPreference: boolean }>(
    `mutation SetNotificationPreference($input: NotificationPreferenceDtoInput!) {
      setNotificationPreference(input: $input)
    }`,
    { variables: { input } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.setNotificationPreference ?? false;
}

export async function toggleCalendarTaskStatus(taskId: string, isCompleted: boolean) {
  const res = await graphqlMutation<{ toggleCalendarTaskStatus: boolean }>(
    `mutation ToggleCalendarTaskStatus($taskId: UUID!, $isCompleted: Boolean!) {
      toggleCalendarTaskStatus(taskId: $taskId, isCompleted: $isCompleted)
    }`,
    { variables: { taskId, isCompleted } }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.toggleCalendarTaskStatus ?? false;
}

export async function getCalendarConflicts(startUtc: string, endUtc: string, excludeEventId?: string | null) {
  const res = await graphqlQuery<{ getCalendarConflicts: CalendarEventDto[] }>(
    `query GetCalendarConflicts($startUtc: DateTime!, $endUtc: DateTime!, $excludeEventId: UUID) {
      getCalendarConflicts(startUtc: $startUtc, endUtc: $endUtc, excludeEventId: $excludeEventId) { id title startUtc endUtc }
    }`,
    { variables: { startUtc, endUtc, excludeEventId: excludeEventId ?? undefined }, skipCache: true }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.getCalendarConflicts ?? [];
}

export async function exportCalendarIcs(fromUtc: Date, toUtc: Date): Promise<string> {
  const res = await graphqlQuery<{ exportCalendarIcs: string }>(
    `query ExportCalendarIcs($fromUtc: DateTime!, $toUtc: DateTime!) {
      exportCalendarIcs(fromUtc: $fromUtc, toUtc: $toUtc)
    }`,
    { variables: { fromUtc: fromUtc.toISOString(), toUtc: toUtc.toISOString() }, skipCache: true }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.exportCalendarIcs ?? '';
}

export interface UserSearchResult {
  userId: string;
  fullName: string;
  userType: string;
  avatar?: number | null;
}

export async function searchUsers(search: string, take: number = 20) {
  const res = await graphqlQuery<{ searchUsers: UserSearchResult[] }>(
    `query SearchUsers($search: String, $take: Int) {
      searchUsers(search: $search, take: $take) { userId fullName userType avatar }
    }`,
    { variables: { search, take }, skipCache: true }
  );
  if (!res.success) throw new Error(res.error);
  return res.data?.searchUsers ?? [];
}
