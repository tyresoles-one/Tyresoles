<script lang="ts">
  import { onMount } from "svelte";
  import { notificationStore, type Notification } from "$lib/stores/notificationStore";
  import Icon from "../icon/icon.svelte";
  import { cn } from "$lib/utils";
  import { formatNotificationAge } from "$lib/utils/notificationTime";
  import { formatDistanceToNow, parseISO } from "date-fns";
  import * as DropdownMenu from "$lib/components/ui/dropdown-menu";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { ScrollArea } from "$lib/components/ui/scroll-area";

  let { class: className = "" } = $props();

  /** Dedupe debug logs per notification id + createdAt (session). */
  let _dbgNavNotifKey = "";

  onMount(() => {
    notificationStore.init();
    const unsub = notificationStore.subscribe((s) => {
      const n = s.notifications[0];
      if (!n) return;
      const key = `${n.id}:${n.createdAt}:${s.serverTimeUtc ?? ""}`;
      if (key === _dbgNavNotifKey) return;
      _dbgNavNotifKey = key;
      const raw = n.createdAt;
      const d = new Date(raw);
      const relClient = formatDistanceToNow(d, { addSuffix: true });
      const relServer = s.serverTimeUtc ? formatNotificationAge(raw, s.serverTimeUtc) : null;
      const deltaServerMs =
        s.serverTimeUtc && !Number.isNaN(parseISO(s.serverTimeUtc).getTime())
          ? parseISO(s.serverTimeUtc).getTime() - d.getTime()
          : null;
      // #region agent log
      fetch("http://127.0.0.1:7618/ingest/5d806cd4-86b2-403a-9e2d-75847ea1b6fa", {
        method: "POST",
        headers: { "Content-Type": "application/json", "X-Debug-Session-Id": "ab180f" },
        body: JSON.stringify({
          sessionId: "ab180f",
          runId: "post-fix",
          hypothesisId: "verify",
          location: "NotificationDropdown.svelte:subscribe",
          message: "notification relative time client vs server ref",
          data: {
            titlePrefix: (n.title || "").slice(0, 24),
            rawCreatedAt: raw,
            serverTimeUtc: s.serverTimeUtc,
            deltaMsClientVsCreated: Date.now() - d.getTime(),
            deltaMsServerRefVsCreated: deltaServerMs,
            formatDistanceClientClock: relClient,
            formatDistanceServerRef: relServer,
          },
          timestamp: Date.now(),
        }),
      }).catch(() => {});
      // #endregion
    });
    return unsub;
  });

  function getIcon(type: string) {
    switch (type) {
      case "INFO":
        return "info";
      case "WARNING":
        return "triangle-alert";
      case "ERROR":
        return "x-circle";
      case "SUCCESS":
        return "check-circle";
      default:
        return "bell";
    }
  }

  function getIconColor(type: string) {
    switch (type) {
      case "INFO":
        return "text-blue-500";
      case "WARNING":
        return "text-amber-500";
      case "ERROR":
        return "text-red-500";
      case "SUCCESS":
        return "text-emerald-500";
      default:
        return "text-muted-foreground";
    }
  }

  async function handleNotificationClick(n: Notification) {
    if (!n.isRead) {
      await notificationStore.markAsRead(n.id);
    }
    if (n.link) {
      window.location.href = n.link;
    }
  }
</script>

<DropdownMenu.Root>
  <DropdownMenu.Trigger>
    {#snippet child({ props })}
      <div class="relative">
        <Button
          variant="outline"
          size="icon"
          class={cn("group rounded-lg border bg-background hover:bg-muted transition-all duration-300 shadow-sm w-9 h-9 hover:scale-105 active:scale-95", className)}
          {...props}
        >
          <Icon
            name="bell"
            class={cn(
              "w-4 h-4 text-muted-foreground group-hover:text-primary transition-colors",
              $notificationStore.unreadCount > 0 && "animate-ringing"
            )}
          />
        </Button>
        {#if $notificationStore.unreadCount > 0}
          <div class="absolute -top-1.5 -right-1.5 pointer-events-none">
            <span class="absolute inline-flex h-full w-full animate-ping rounded-full bg-red-400 opacity-75"></span>
            <span
              class="relative flex h-4 w-4 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white border-2 border-background animate-in zoom-in duration-500"
            >
              {$notificationStore.unreadCount > 9 ? "9+" : $notificationStore.unreadCount}
            </span>
          </div>
        {/if}
      </div>
    {/snippet}
  </DropdownMenu.Trigger>

  <DropdownMenu.Content
    align="end"
    class="w-80 sm:w-96 p-0 shadow-2xl border-primary/20 bg-card/95 backdrop-blur-md overflow-hidden rounded-xl animate-in fade-in slide-in-from-top-2 duration-300"
    sideOffset={8}
  >
    <div class="flex items-center justify-between p-4 border-b bg-muted/30">
      <div class="flex items-center gap-2">
        <h3 class="font-bold text-sm tracking-tight text-foreground">Notifications</h3>
        {#if $notificationStore.unreadCount > 0}
          <Badge variant="secondary" class="h-5 px-1.5 text-[10px] bg-primary/10 text-primary border-primary/20 animate-pulse">
            {$notificationStore.unreadCount} New
          </Badge>
        {/if}
      </div>
      <Button
        variant="ghost"
        size="sm"
        class="h-8 px-2 text-[11px] font-medium hover:bg-primary/10 hover:text-primary transition-all rounded-md"
        onclick={() => notificationStore.markAllAsRead()}
        disabled={$notificationStore.unreadCount === 0}
      >
        Mark all as read
      </Button>
    </div>

    <ScrollArea class="h-[400px]">
      <div class="flex flex-col">
        {#if $notificationStore.notifications.length === 0}
          <div class="flex flex-col items-center justify-center py-12 px-4 text-center animate-in fade-in zoom-in duration-500">
            <div class="w-12 h-12 rounded-full bg-muted flex items-center justify-center mb-3">
              <Icon name="bell-off" class="w-6 h-6 text-muted-foreground/50" />
            </div>
            <p class="text-sm font-medium text-foreground">No notifications yet</p>
            <p class="text-[11px] text-muted-foreground mt-1">We'll alert you when something happens.</p>
          </div>
        {:else}
          {#each $notificationStore.notifications as notification, i (notification.id)}
            <button
              onclick={() => handleNotificationClick(notification)}
              class={cn(
                "flex flex-col gap-1 p-4 text-left hover:bg-muted/70 transition-all border-b last:border-0 relative group animate-in fade-in slide-in-from-right-4 duration-300",
                !notification.isRead && "bg-primary/[0.03]"
              )}
              style="animation-delay: {Math.min(i * 50, 400)}ms"
            >
              {#if !notification.isRead}
                <div class="absolute left-0 top-0 bottom-0 w-1 bg-primary group-hover:w-1.5 transition-all"></div>
              {/if}
              
              <div class="flex gap-3">
                <div class={cn(
                  "mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl border bg-background shadow-sm group-hover:scale-110 transition-transform duration-300",
                  getIconColor(notification.type).replace("text-", "border-").replace("500", "500/20")
                )}>
                  <Icon name={getIcon(notification.type)} class={cn("h-4.5 w-4.5", getIconColor(notification.type))} />
                </div>
                
                <div class="flex-1 space-y-1">
                  <div class="flex items-center justify-between gap-2">
                    <p class={cn("text-xs font-bold leading-tight", !notification.isRead ? "text-foreground" : "text-muted-foreground")}>
                      {notification.title}
                    </p>
                    <span class="text-[9px] font-medium text-muted-foreground whitespace-nowrap bg-muted/50 px-1.5 py-0.5 rounded-full">
                      {formatNotificationAge(notification.createdAt, $notificationStore.serverTimeUtc)}
                    </span>
                  </div>
                  <p class="text-[11px] leading-relaxed text-muted-foreground line-clamp-2 group-hover:text-foreground/80 transition-colors">
                    {notification.message}
                  </p>
                </div>
              </div>
            </button>
          {/each}
        {/if}
      </div>
    </ScrollArea>

    <div class="p-2 border-t bg-muted/10">
      <Button variant="ghost" class="w-full h-9 text-[11px] font-bold text-muted-foreground hover:text-primary group/viewall" onclick={() => window.location.href = '/notifications'}>
        View All Notifications
        <Icon name="arrow-right" class="size-3 ml-1 group-hover/viewall:translate-x-1 transition-transform" />
      </Button>
    </div>
  </DropdownMenu.Content>
</DropdownMenu.Root>

<style>
  :global(.line-clamp-2) {
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  @keyframes ringing {
    0% { transform: rotate(0); }
    10% { transform: rotate(15deg); }
    20% { transform: rotate(-15deg); }
    30% { transform: rotate(10deg); }
    40% { transform: rotate(-10deg); }
    50% { transform: rotate(5deg); }
    60% { transform: rotate(-5deg); }
    70% { transform: rotate(0); }
    100% { transform: rotate(0); }
  }

  :global(.animate-ringing) {
    animation: ringing 2.5s ease-in-out infinite;
    transform-origin: center top;
  }
</style>
