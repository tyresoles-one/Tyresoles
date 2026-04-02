<script lang="ts">
  import { onMount } from "svelte";
  import { authStore } from "$lib/stores/auth";
  import { graphqlQuery, buildQuery } from "$lib/services/graphql";
  import { PageHeading } from "$lib/components/venUI/page-heading";
  import { Icon } from "$lib/components/venUI/icon";
  import { Button } from "$lib/components/ui/button";
  import { toast } from "$lib/components/venUI/toast";
  import { goto } from "$app/navigation";

  let loading = $state(true);
  let reportCategories = $state<{ item: any; reports: any[] }[]>([]);

  const GetGroupDetailsQuery = buildQuery`
    query GetGroupDetails($category: String!) {
      groupDetails(category: $category) {
        code
        category
        name
        value
        extraValue
      }
    }
  `;

  onMount(async () => {
    loading = true;
    try {
      const auth = authStore.get();
      if (!auth.menus) {
        loading = false;
        return;
      }

      // Identify all report-related menu items
      const reportItems: any[] = [];
      for (const menu of auth.menus) {
        const isReportMenu = menu.label.toLowerCase().includes("report");
        for (const sub of menu.subMenus || []) {
          const isReportSub = sub.label.toLowerCase().includes("report");
          for (const item of sub.items || []) {
            
            if (
              isReportMenu ||
              isReportSub ||
              item.action.toLowerCase().includes("report")
            ) {
              console.log("item 1", item);
              if (item.code && !reportItems.find((r) => r.code === item.code)) {
                console.log("item 2", item);
                reportItems.push(item);
              }
            }
          }
        }
      }

      console.log("reportItems", reportItems);
      // Fetch GetGroupDetails for each report menu item's Code
      const promises = reportItems.map(async (item) => {
        // Parse options into an array of allowed codes
        const allowedCodes = item.options
          ? item.options
              .split(",")
              .map((o: string) => o.trim())
              .filter(Boolean)
          : [];

        const res = await graphqlQuery<any>(GetGroupDetailsQuery, {
          variables: { category: item.code },
        });

        let reports = [];
        if (res.success && res.data?.groupDetails) {
          reports = res.data.groupDetails;

          // Only filter if allowedCodes were actually provided
          if (allowedCodes.length > 0) {
            reports = reports.filter((report: any) =>
              allowedCodes.includes(report.code),
            );
          }
        }

        return {
          item,
          reports,
        };
      });

      const results = await Promise.all(promises);

      // Filter out categories that didn't yield any reports
      reportCategories = results.filter((r) => r.reports.length > 0);
    } catch (e) {
      console.error(e);
      toast.error("Failed to load reports configuration.");
    } finally {
      loading = false;
    }
  });

  function openReport(report: any, item: any) {
    if (item.action) {
      // Navigate to the action path, appending the report code
      // For instance: "/reports/sales?id=REPORT1"
      goto(`${item.action}?id=${encodeURIComponent(report.code)}`);
    } else {
      toast.error("No action defined for this report category.");
    }
  }
</script>

<div class="min-h-screen bg-muted/30 pb-16">
  <PageHeading
    backHref="/"
    backLabel="Back to Dashboard"
    icon="file-text"
    class="border-b bg-background"
  >
    {#snippet title()}
      Reports
    {/snippet}
    {#snippet description()}
      View and generate organizational reports
    {/snippet}
  </PageHeading>

  <main class="container mx-auto px-4 py-8 md:px-6">
    {#if loading}
      <div
        class="flex flex-col items-center justify-center py-16 gap-4 text-muted-foreground bg-card rounded-xl border shadow-sm"
      >
        <div
          class="size-10 border-4 border-muted border-t-primary rounded-full animate-spin"
        ></div>
        <p class="font-medium text-lg">Loading reports...</p>
      </div>
    {:else if reportCategories.length === 0}
      <div
        class="py-16 text-center border-2 border-dashed rounded-xl bg-muted/20"
      >
        <div
          class="mx-auto mb-4 flex size-12 items-center justify-center rounded-full bg-muted"
        >
          <Icon name="file-search" class="size-6 text-muted-foreground" />
        </div>
        <h3 class="mb-1 text-lg font-semibold">No Reports Found</h3>
        <p class="text-muted-foreground">
          You do not have any reports available in your account.
        </p>
      </div>
    {:else}
      <div class="flex flex-col gap-10">
        {#each reportCategories as category}
          <section>
            <div class="flex items-center gap-2 mb-4 px-1">
              <div
                class="flex size-8 items-center justify-center rounded-md bg-primary/10 text-primary"
              >
                <Icon
                  name={category.item.icon || "folder-open"}
                  class="size-4"
                />
              </div>
              <h2 class="text-xl font-bold tracking-tight">
                {category.item.label}
              </h2>
            </div>

            <div
              class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4"
            >
              {#each category.reports as report}
                <Button
                  variant="outline"
                  class="group h-auto w-full justify-start gap-4 p-4 text-left transition-all hover:-translate-y-1 hover:border-primary/50 hover:shadow-md bg-card"
                  onclick={() => openReport(report, category.item)}
                >
                  <div
                    class="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground"
                  >
                    <Icon name="file-chart-line" class="size-5" />
                  </div>
                  <div class="flex flex-col overflow-hidden">
                    <span
                      class="truncate font-semibold text-foreground text-sm leading-snug"
                      >{report.name}</span
                    >
                  </div>
                </Button>
              {/each}
            </div>
          </section>
        {/each}
      </div>
    {/if}
  </main>
</div>
