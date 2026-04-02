<script lang="ts">
	import { onMount } from 'svelte';
	import { PageHeading } from '$lib/components/venUI/page-heading';
	import { Button } from '$lib/components/ui/button';
	import { Input } from '$lib/components/ui/input';
	import { Label } from '$lib/components/ui/label';
	import { Icon } from '$lib/components/venUI/icon';
	import { Badge } from '$lib/components/ui/badge';
	import { Skeleton } from '$lib/components/ui/skeleton';
	import { Separator } from '$lib/components/ui/separator';
	import {
		Card,
		CardContent,
		CardHeader,
		CardTitle,
		CardDescription,
		CardFooter
	} from '$lib/components/ui/card';
	import { AvatarSelector } from '$lib/components/venUI/avatar-selector';
	import { authStore } from '$lib/stores/auth';
	import { toast } from '$lib/components/venUI/toast';
	import { graphqlQuery, graphqlMutation } from '$lib/services/graphql/client';
	import {
		GetProfileDocument,
		SetProfileDocument,
		type GetProfileQuery
	} from '$lib/services/graphql/generated/types.js';

	let loading = $state(true);
	let saving = $state(false);

	let profile = $state<GetProfileQuery['profile'] | null>(null);

	let fullName = $state('');
	let email = $state('');
	let mobileNo = $state('');
	let avatarIndex = $state(0);
	
	let editingPin = $state(false);
	let securityPin = $state('');

	function getUserId() {
		return $authStore.username || $authStore.user?.userId || '';
	}

	async function loadProfile() {
		loading = true;
		const userId = getUserId();
		const res = await graphqlQuery(GetProfileDocument, {
			variables: { userId: userId || '' },
			skipCache: true
		});
		if (res.success && res.data?.profile) {
			const p = res.data.profile;
			profile = p;
			fullName = p.fullName || '';
			email = p.email || '';
			mobileNo = p.mobileNo || '';
			avatarIndex = p.avatar || 0;
			securityPin = p.securityPIN?.toString() || '';
		} else {
			toast.error(res.error || 'Failed to load profile');
		}
		loading = false;
	}

	onMount(() => {
		loadProfile();
	});

	async function handleSave() {
		saving = true;
		const userId = getUserId();
		const res = await graphqlMutation(SetProfileDocument, {
			variables: {
				userId: userId || '',
				input: {
					fullName: fullName,
					email: email,
					mobileNo: mobileNo,
					avatar: avatarIndex,
					securityPIN: securityPin ? parseInt(securityPin) : null
				}
			}
		});

		if (res.success && res.data?.setProfile?.success) {
			editingPin = false;
			toast.success(res.data.setProfile.message || 'Profile updated successfully');

			// Update the local authStore with the new avatar and details to reflect instantly
			const authData = authStore.get();
			if (authData.user) {
				authStore.set({
					...authData,
					user: {
						...authData.user,
						avatar: avatarIndex
					}
				});
			}

			// Refresh the page
			window.location.reload();
		} else {
			toast.error(res.data?.setProfile?.message || res.error || 'Failed to update profile');
		}
		saving = false;
	}

	function handleCancel() {
		if (profile) {
			fullName = profile.fullName || '';
			email = profile.email || '';
			mobileNo = profile.mobileNo || '';
			avatarIndex = profile.avatar || 0;
			securityPin = profile.securityPIN?.toString() || '';
			editingPin = false;
			toast.info('Changes discarded.');
		}
	}
</script>

<div class="min-h-screen bg-muted/30 pb-16">
	<!-- Page Heading -->
	<PageHeading backHref="/" backLabel="Back to home" icon="user" class="border-b bg-background">
		{#snippet title()}
			Profile Details
		{/snippet}
		{#snippet description()}
			Manage your personal information and account settings
		{/snippet}		
	</PageHeading>

	<main class="container mx-auto px-4 py-8 md:px-6">
		<div class="grid gap-6 md:grid-cols-3 lg:gap-8">
			<!-- Left Column: Avatar & Quick Info -->
			<div class="flex flex-col gap-6 md:col-span-1">
				<Card class="overflow-hidden border-primary/10 bg-gradient-to-b from-background to-primary/5 shadow-md">
					<CardHeader class="pb-4">
						<CardTitle class="text-lg">Profile Photo</CardTitle>
						<CardDescription>
							Choose an avatar. It will appear across the app.
						</CardDescription>
					</CardHeader>
					<CardContent class="flex flex-col items-center justify-center p-6 pt-0">
						{#if loading}
							<Skeleton class="size-32 rounded-full" />
						{:else}
							<AvatarSelector bind:selectedIndex={avatarIndex} maxImages={8} class="animate-in fade-in zoom-in duration-300" />
						{/if}
						<div class="mt-6 flex flex-col items-center text-center">
							{#if loading}
								<Skeleton class="mb-2 h-6 w-32" />
								<Skeleton class="h-4 w-24" />
							{:else}
								<h3 class="text-xl font-semibold tracking-tight text-foreground">
									{fullName || profile?.userId || 'User'}
								</h3>
								<div class="mt-1 flex items-center justify-center gap-2">
									<Badge variant="secondary" class="font-normal capitalize">{profile?.userType || 'User'}</Badge>
								</div>
							{/if}
						</div>
					</CardContent>
				</Card>

				<!-- System Information Card (Read-only) -->
				<Card class="shadow-sm">
					<CardHeader class="pb-4">
						<CardTitle class="flex items-center gap-2 text-base">
							<Icon name="shield" class="size-4 text-primary" />
							Security & Access
						</CardTitle>
					</CardHeader>
					<CardContent class="flex flex-col gap-4 text-sm">
						{#if loading}
							<div class="flex flex-col gap-2">
								<Skeleton class="h-10 w-full" />
								<Skeleton class="h-10 w-full" />
							</div>
						{:else}
							<div class="flex flex-col gap-1">
								<span class="text-xs font-medium text-muted-foreground">User ID</span>
								<span class="font-medium text-foreground">{profile?.userId}</span>
							</div>
							<Separator />
							<div class="flex flex-col gap-1">
								<span class="text-xs font-medium text-muted-foreground">Last Password Changed</span>
								<span class="text-foreground">
									{profile?.lastPasswordChanged ? new Date(profile.lastPasswordChanged).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' }) : 'Never'}
								</span>
							</div>
							<Separator />
							<div class="flex flex-col gap-1">
								<span class="text-xs font-medium text-muted-foreground">Security PIN</span>
								<div class="flex items-center justify-between mt-1">
									{#if editingPin}
										<Input
											type="text"
											bind:value={securityPin}
											class="h-8 w-2/3 font-mono text-sm"
											placeholder="Enter PIN"
										/>
										<div class="flex gap-1">
											<Button variant="ghost" size="icon" class="size-7 text-green-600 hover:text-green-700 hover:bg-green-100 dark:hover:bg-green-900/30" onclick={handleSave}>
												<Icon name="check" class="size-4" />
											</Button>
											<Button variant="ghost" size="icon" class="size-7 text-muted-foreground" onclick={() => { editingPin = false; securityPin = profile?.securityPIN?.toString() || ''; }}>
												<Icon name="x" class="size-4" />
											</Button>
										</div>
									{:else}
										<span class="text-foreground font-mono font-medium">{profile?.securityPIN || 'Not Set'}</span>
										<Button variant="ghost" size="icon" class="h-7 w-7 text-muted-foreground hover:text-foreground" onclick={() => editingPin = true}>
											<Icon name="pencil" class="size-3.5" />
											<span class="sr-only">Edit PIN</span>
										</Button>
									{/if}
								</div>
							</div>
						{/if}
					</CardContent>
					<CardFooter class="border-t bg-muted/10 px-6 py-4">
						<Button href="/change-password" variant="outline" class="w-full relative shadow-sm hover:shadow-md transition-all">
							<Icon name="key" class="mr-2 size-4 text-primary" />
							Change Password
						</Button>
					</CardFooter>
				</Card>
			</div>

			<!-- Right Column: Personal Info & Entities -->
			<div class="flex flex-col gap-6 md:col-span-2">
				<!-- Personal Information Form -->
				<Card class="shadow-sm">
					<CardHeader>
						<CardTitle class="text-lg">Personal Information</CardTitle>
						<CardDescription>
							Update your contact details and name.
						</CardDescription>
					</CardHeader>
					<CardContent>
						{#if loading}
							<div class="grid gap-6 sm:grid-cols-2">
								{#each { length: 3 } as _}
									<div class="space-y-2">
										<Skeleton class="h-4 w-16" />
										<Skeleton class="h-10 w-full" />
									</div>
								{/each}
							</div>
						{:else}
							<div class="grid gap-6 sm:grid-cols-2">
								<div class="space-y-2">
									<Label for="fullName">Full Name</Label>
									<div class="relative">
										<Icon name="user" class="absolute left-3 top-3 size-4 text-muted-foreground" />
										<Input
											id="fullName"
											placeholder="Enter your full name"
											class="pl-9"
											bind:value={fullName}
										/>
									</div>
								</div>
								
								<div class="space-y-2">
									<Label for="email">Email Address</Label>
									<div class="relative">
										<Icon name="mail" class="absolute left-3 top-3 size-4 text-muted-foreground" />
										<Input
											id="email"
											type="email"
											placeholder="Enter email address"
											class="pl-9"
											bind:value={email}
										/>
									</div>
								</div>

								<div class="space-y-2">
									<Label for="mobileNo">Mobile Number</Label>
									<div class="relative">
										<Icon name="phone" class="absolute left-3 top-3 size-4 text-muted-foreground" />
										<Input
											id="mobileNo"
											type="tel"
											placeholder="Enter mobile number"
											class="pl-9"
											bind:value={mobileNo}
										/>
									</div>
								</div>
							</div>
						{/if}
					</CardContent>
					<CardFooter class="flex justify-end border-t bg-muted/10 px-6 py-4">
						<Button onclick={handleSave} disabled={loading || saving} class="min-w-[120px] transition-all hover:shadow-md">
							{#if saving}
								<Icon name="loader-2" class="mr-2 size-4 animate-spin" />
								Saving...
							{:else}
								Save Changes
							{/if}
						</Button>
					</CardFooter>
				</Card>

				<!-- Linked Entities -->
				<Card class="shadow-sm">
					<CardHeader>
						<CardTitle class="text-lg">Linked Entities</CardTitle>
						<CardDescription>
							Entities or locations assigned to your account.
						</CardDescription>
					</CardHeader>
					<CardContent>
						{#if loading}
							<div class="grid gap-4 sm:grid-cols-2">
								{#each { length: 2 } as _}
									<Skeleton class="h-24 w-full rounded-xl" />
								{/each}
							</div>
						{:else if profile?.entities && profile.entities.length > 0}
							<div class="grid gap-4 sm:grid-cols-2">
								{#each profile.entities as entity}
									<div class="relative flex flex-col gap-1 overflow-hidden rounded-xl border bg-card p-4 transition-all hover:border-primary/50 hover:shadow-sm">
										<div class="flex items-center justify-between">
											<span class="font-semibold text-foreground">{entity?.name || 'Unknown'}</span>
											<Badge variant="outline" class="bg-black/70 text-white">{entity?.code}</Badge>
										</div>
										<span class="text-sm text-muted-foreground">{entity?.title || 'No Title'}</span>
										{#if entity?.location}
											<div class="mt-2 flex items-center gap-1.5 text-xs text-muted-foreground">
												<Icon name="map-pin" class="size-3" />
												<span class="truncate">{entity.location}</span>
											</div>
										{/if}
									</div>
								{/each}
							</div>
						{:else}
							<div class="flex flex-col items-center justify-center rounded-xl border border-dashed py-8 text-center bg-muted/30">
								<Icon name="building" class="mb-3 size-8 text-muted-foreground/50" />
								<span class="text-sm font-medium text-foreground">No Entities Linked</span>
								<span class="mt-1 text-xs text-muted-foreground">Your account is not linked to any specific entities or locations.</span>
							</div>
						{/if}
					</CardContent>
				</Card>
			</div>
		</div>
	</main>
</div>
