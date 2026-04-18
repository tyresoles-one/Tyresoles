<script lang="ts">
	import { onMount } from "svelte";
	import { browser } from "$app/environment";
	import { Button } from "$lib/components/ui/button";
	import MonitorUp from "@lucide/svelte/icons/monitor-up";
	import MonitorPlay from "@lucide/svelte/icons/monitor-play";
	import Shield from "@lucide/svelte/icons/shield";
	import { authStore } from "$lib/stores/auth";
	import { listActiveAssistSessions } from "$lib/services/remoteAssist/api";

	let assistAdmin = $state(false);

	onMount(() => {
		if (!browser) return;
		const token = authStore.get().token;
		if (!token) return;
		void listActiveAssistSessions(token)
			.then(() => {
				assistAdmin = true;
			})
			.catch(() => {
				assistAdmin = false;
			});
	});
</script>

<svelte:head>
	<title>Remote Assist</title>
</svelte:head>

<div class="flex min-h-[calc(100vh-4rem)] flex-col items-center justify-center p-4 bg-gradient-to-br from-background to-muted/20">
	<div class="w-full max-w-4xl space-y-4 animate-in fade-in slide-in-from-bottom-8 duration-700 ease-out">		
		<div class="grid md:grid-cols-2 gap-6 {assistAdmin ? 'lg:grid-cols-3' : ''}">
			<!-- Host Card -->
			<div class="relative group overflow-hidden rounded-3xl border bg-card text-card-foreground shadow-sm transition-all hover:shadow-lg hover:border-primary/50">
				<div class="absolute inset-0 bg-gradient-to-br from-primary/10 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
				<div class="p-8 sm:p-10 space-y-6 relative z-10">
					<div class="p-4 bg-primary/10 rounded-2xl w-fit text-primary group-hover:scale-110 transition-transform duration-300">
						<MonitorUp class="w-10 h-10" />
					</div>
					<div class="space-y-2">
						<h2 class="text-2xl font-semibold">Share Screen</h2>
						<p class="text-muted-foreground leading-relaxed h-14">
							Broadcast your desktop to a colleague and grant them remote control capabilities securely.
						</p>
					</div>
					<Button href="/assist/host" size="lg" class="w-full text-base h-12 rounded-xl group-hover:bg-primary/90 transition-colors">
						Start Hosting
					</Button>
				</div>
			</div>

			<!-- Join Card -->
			<div class="relative group overflow-hidden rounded-3xl border bg-card text-card-foreground shadow-sm transition-all hover:shadow-lg hover:border-blue-500/50">
				<div class="absolute inset-0 bg-gradient-to-br from-blue-500/10 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
				<div class="p-8 sm:p-10 space-y-6 relative z-10">
					<div class="p-4 bg-blue-500/10 rounded-2xl w-fit text-blue-500 group-hover:scale-110 transition-transform duration-300">
						<MonitorPlay class="w-10 h-10" />
					</div>
					<div class="space-y-2">
						<h2 class="text-2xl font-semibold">Join Session</h2>
						<p class="text-muted-foreground leading-relaxed h-14">
							Connect to an active session via a join code to view or control the host's screen.
						</p>
					</div>
					<Button href="/assist/join" variant="outline" size="lg" class="w-full text-base h-12 rounded-xl hover:bg-blue-500 hover:text-white transition-all hover:border-blue-500">
						Join Session
					</Button>
				</div>
			</div>

			{#if assistAdmin}
				<div class="relative group overflow-hidden rounded-3xl border bg-card text-card-foreground shadow-sm transition-all hover:shadow-lg hover:border-amber-500/50 md:col-span-2 lg:col-span-1">
					<div class="absolute inset-0 bg-gradient-to-br from-amber-500/10 to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-500"></div>
					<div class="p-8 sm:p-10 space-y-6 relative z-10">
						<div class="p-4 bg-amber-500/10 rounded-2xl w-fit text-amber-600 dark:text-amber-400 group-hover:scale-110 transition-transform duration-300">
							<Shield class="w-10 h-10" />
						</div>
						<div class="space-y-2">
							<h2 class="text-2xl font-semibold">Assist admin</h2>
							<p class="text-muted-foreground leading-relaxed h-14">
								View all active host sessions and join any session without a join code.
							</p>
						</div>
						<Button
							href="/assist/admin"
							variant="outline"
							size="lg"
							class="w-full text-base h-12 rounded-xl border-amber-500/50 hover:bg-amber-500/10"
						>
							Active host sessions
						</Button>
					</div>
				</div>
			{/if}
		</div>

		<p class="text-center text-sm text-muted-foreground mt-8">
			Note: Remote mouse control on the host requires the Tyresoles native desktop application.
		</p>
	</div>
</div>
