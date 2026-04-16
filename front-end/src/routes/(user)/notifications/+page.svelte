<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import DashboardGrid from "$lib/components/venUI/dashboards/DashboardGrid.svelte";
  import { Button } from "$lib/components/ui/button";
  import { Badge } from "$lib/components/ui/badge";
  import { Icon } from "$lib/components/venUI/icon";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { cn } from "$lib/utils";
  import { formatNotificationAge } from "$lib/utils/notificationTime";
  import {
    notificationStore,
    type Notification,
  } from "$lib/stores/notificationStore";

  const PAGE_LIMIT = 500;

  onMount(() => {
    notificationStore.refresh(PAGE_LIMIT);
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

  async function handleOpen(n: Notification) {
    if (!n.isRead) {
      await notificationStore.markAsRead(n.id);
    }
    if (n.link) {
      if (n.link.startsWith("/")) {
        await goto(n.link);
      } else {
        window.location.href = n.link;
      }
    }
  }
</script>

<PageHeading
  pageTitle="Notifications"
  icon="bell"
  title="Notifications"
  description="Alerts and updates for your account"
  actions={headingActions}
/>

{#snippet headingActions()}
  <Button
    variant="outline"
    size="sm"
    class="shrink-0 text-xs font-semibold"
    onclick={() => notificationStore.refresh(PAGE_LIMIT)}
    disabled={$notificationStore.loading}
  >
    <Icon
      name="refresh-cw"
      class={cn("size-3.5 mr-1.5", $notificationStore.loading && "animate-spin")}
    />
    Refresh
  </Button>
  <Button
    variant="secondary"
    size="sm"
    class="shrink-0 text-xs font-semibold"
    onclick={() => notificationStore.markAllAsRead()}
    disabled={$notificationStore.unreadCount === 0 || $notificationStore.loading}
  >
    Mark all read
  </Button>
{/snippet}

<div class="container mx-auto px-3 sm:px-4 pb-8 pt-4 max-w-7xl">
  {#if $notificationStore.loading && $notificationStore.notifications.length === 0}
    <DashboardGrid class="p-0!">
      {#each Array.from({ length: 8 }) as _, i (i)}
        <div
          class="rounded-xl border bg-card/80 p-4 shadow-sm animate-pulse min-h-[140px]"
        >
          <div class="flex gap-3">
            <div class="size-10 rounded-xl bg-muted shrink-0"></div>
            <div class="flex-1 space-y-2 pt-0.5">
              <div class="h-4 bg-muted rounded w-3/4"></div>
              <div class="h-3 bg-muted/80 rounded w-1/2"></div>
            </div>
          </div>
          <div class="mt-4 space-y-2">
            <div class="h-3 bg-muted/70 rounded w-full"></div>
            <div class="h-3 bg-muted/60 rounded w-5/6"></div>
          </div>
        </div>
      {/each}
    </DashboardGrid>
  {:else if $notificationStore.error}
    <Card class="border-destructive/30 bg-destructive/5">
      <CardContent class="pt-6 flex flex-col sm:flex-row sm:items-center gap-3">
        <Icon name="triangle-alert" class="size-8 text-destructive shrink-0" />
        <div class="min-w-0">
          <p class="font-semibold text-foreground">Could not load notifications</p>
          <p class="text-sm text-muted-foreground mt-1">{$notificationStore.error}</p>
        </div>
        <Button
          variant="outline"
          class="sm:ml-auto shrink-0"
          onclick={() => notificationStore.refresh(PAGE_LIMIT)}
        >
          Try again
        </Button>
      </CardContent>
    </Card>
  {:else if $notificationStore.notifications.length === 0}
    <Card
      class="border-dashed border-2 bg-muted/20 max-w-lg mx-auto text-center"
    >
      <CardHeader class="pb-2">
        <div
          class="mx-auto flex size-14 items-center justify-center rounded-full bg-muted"
        >
          <Icon name="bell-off" class="size-7 text-muted-foreground/60" />
        </div>
        <CardTitle class="text-lg">No notifications yet</CardTitle>
      </CardHeader>
      <CardContent class="text-sm text-muted-foreground pb-8">
        When something needs your attention, it will show up here and in the bell
        in the top bar.
      </CardContent>
    </Card>
  {:else}
    <DashboardGrid class="p-0!">
      {#each $notificationStore.notifications as notification (notification.id)}
        <button
          type="button"
          class="text-left w-full min-w-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 rounded-xl"
          onclick={() => handleOpen(notification)}
        >
          <Card
            class={cn(
              "h-full transition-all hover:shadow-md hover:border-primary/25",
              !notification.isRead &&
                "border-primary/30 bg-primary/4 shadow-sm",
            )}
          >
            <CardHeader class="pb-2 space-y-0">
              <div class="flex items-start gap-3">
                <div
                  class={cn(
                    "mt-0.5 flex size-10 shrink-0 items-center justify-center rounded-xl border bg-background shadow-sm",
                    getIconColor(notification.type)
                      .replace("text-", "border-")
                      .replace("500", "500/25"),
                  )}
                >
                  <Icon
                    name={getIcon(notification.type)}
                    class={cn("size-5", getIconColor(notification.type))}
                  />
                </div>
                <div class="min-w-0 flex-1 space-y-1">
                  <div
                    class="flex flex-wrap items-center gap-x-2 gap-y-1 justify-between"
                  >
                    <CardTitle
                      class="text-sm sm:text-base leading-snug line-clamp-2 pr-1"
                    >
                      {notification.title}
                    </CardTitle>
                    {#if !notification.isRead}
                      <Badge
                        variant="secondary"
                        class="shrink-0 h-5 text-[10px] bg-primary/15 text-primary border-primary/20"
                      >
                        New
                      </Badge>
                    {/if}
                  </div>
                  <p class="text-[10px] sm:text-[11px] text-muted-foreground font-medium">
                    {formatNotificationAge(
                      notification.createdAt,
                      $notificationStore.serverTimeUtc,
                    )}
                  </p>
                </div>
              </div>
            </CardHeader>
            <CardContent class="pt-0 pb-4 text-xs sm:text-sm text-muted-foreground leading-relaxed line-clamp-4">
              {notification.message}
            </CardContent>
            {#if notification.link}
              <div
                class="px-6 pb-4 flex items-center gap-1 text-[11px] font-semibold text-primary"
              >
                Open link
                <Icon name="arrow-right" class="size-3" />
              </div>
            {/if}
          </Card>
        </button>
      {/each}
    </DashboardGrid>
  {/if}
</div>
