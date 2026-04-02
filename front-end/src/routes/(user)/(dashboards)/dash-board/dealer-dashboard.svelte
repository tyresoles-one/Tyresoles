<script lang="ts">
    import { BalanceWidget } from '$lib/components/venUI/balanceWidget';
    import { getUser } from '$lib/stores/auth';
    let user = getUser();
</script>

<!-- Dashboard tile grid: mobile-first, 1 col → 2 col → 3/4 col on larger screens -->
<div class="dashboard-grid">
    <BalanceWidget class="tile" entityCode={user?.entityCode} entityType={user?.entityType} respCenter={user?.respCenter} />

    <!-- Add more tiles here as needed -->
    <!-- <AnotherWidget class="tile" /> -->
</div>

<style>
    .dashboard-grid {
        display: grid;
        gap: 0.875rem;
        padding: 1rem;

        /* Mobile: 1 column */
        grid-template-columns: 1fr;
    }

    /* Small tablets / large phones: 2 columns */
    @media (min-width: 480px) {
        .dashboard-grid {
            grid-template-columns: repeat(2, 1fr);
        }
    }

    /* Tablets: 3 columns */
    @media (min-width: 768px) {
        .dashboard-grid {
            grid-template-columns: repeat(3, 1fr);
            gap: 1rem;
            padding: 1.25rem;
        }
    }

    /* Desktop: 4 columns */
    @media (min-width: 1024px) {
        .dashboard-grid {
            grid-template-columns: repeat(4, 1fr);
            gap: 1.125rem;
            padding: 1.5rem;
        }
    }

    /* Wide desktop: auto-fill with min tile width */
    @media (min-width: 1280px) {
        .dashboard-grid {
            grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
        }
    }

    /* Ensure tiles passed via slot fill their grid cell */
    :global(.dashboard-grid > .tile) {
        width: 100%;
        min-width: 0;
    }
</style>