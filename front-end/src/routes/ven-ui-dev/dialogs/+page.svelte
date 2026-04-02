<script lang="ts">
    import { Button } from '$lib/components/ui/button';
    import { Dialog } from '$lib/components/venUI/dialog';
    // Usage of helper function
    
    async function showSimpleAlert() {
        await Dialog.alert('Action Successful', 'Your changes have been saved successfully.');
    }

    async function showConfirmDialog() {
        const result = await Dialog.confirm(
            'Delete Account?',
            'Are you sure you want to delete your account? This action cannot be undone.',
            { 
                 confirmLabel: 'Delete', 
                 variant: 'destructive',
                 icon: 'triangle-alert' // 'triangle-alert' should map to TriangleAlert icon if available
            }
        );

        if (result) {
            Dialog.alert('Deleted', 'Account has been deleted (simulated).');
        } else {
             // console.log('Cancelled');
        }
    }

    async function showCustomActionDialog() {
         const result = await Dialog.open({
            title: 'Choose an Option',
            description: 'Please select one of the following actions.',
            actions: [
                { label: 'Option A', value: 'A', variant: 'default' },
                { label: 'Option B', value: 'B', variant: 'secondary' },
                { label: 'Cancel', value: null, variant: 'ghost' }
            ]
         });

         if (result) {
             Dialog.alert('Selected', `You chose ${result}`);
         }
    }
</script>

<div
  class="p-4 md:p-10 flex flex-col items-center justify-center gap-6 min-h-[50vh]"
>
  <h1 class="text-2xl font-bold">Dialog Service Demo</h1>

  <div class="flex flex-wrap gap-4">
    <Button variant="outline" onclick={showSimpleAlert}>Simple Alert</Button>
    <Button variant="destructive" onclick={showConfirmDialog}
      >Confirm Dialog</Button
    >
    <Button variant="secondary" onclick={showCustomActionDialog}
      >Custom Actions</Button
    >
  </div>
</div>
