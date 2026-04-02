<script lang="ts">
    import { Button } from '$lib/components/ui/button';
    import { Toast } from '$lib/components/venUI/toast';

    function showSuccess() {
        Toast.success('Operation Successful', 'Your changes have been saved to the database.');
    }

    function showError() {
        Toast.error('Connection Failed', 'Could not reach the server. Please try again later.');
    }

    function showAction() {
        Toast.info('Update Available', 'A new version is available.', {
            action: {
                label: 'Update Now',
                onClick: () => Toast.success('Updated', 'Application updated!')
            },
            cancel: {
                 label: 'Dismiss',
                 onClick: () => console.log('Dismissed')
            }
        });
    }

    function showCustomIcon() {
        Toast.show({
            description: 'You have a new message',
            icon: 'bell', // VenUI icon name
            type: 'info'
        });
    }
</script>

<div
  class="p-4 md:p-10 flex flex-col items-center justify-center gap-6 min-h-[50vh]"
>
  <h1 class="text-2xl font-bold">Toast Service Demo</h1>

  <div class="flex flex-wrap gap-4">
    <Button
      variant="default"
      class="bg-emerald-600 hover:bg-emerald-700"
      onclick={showSuccess}>Success Toast</Button
    >
    <Button variant="destructive" onclick={showError}>Error Toast</Button>
    <Button variant="outline" onclick={showAction}>Action Toast</Button>
    <Button variant="secondary" onclick={showCustomIcon}>Custom Icon</Button>
  </div>
</div>
