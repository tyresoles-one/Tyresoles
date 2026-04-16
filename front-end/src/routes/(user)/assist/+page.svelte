<script lang="ts">
	/**
	 * Remote assist landing: host shares screen, viewer joins with code.
	 */
	import { Button } from "$lib/components/ui/button";
	import { page } from "$app/stores";
</script>

<svelte:head>
	<title>Remote assist</title>
</svelte:head>

<div class="mx-auto max-w-lg space-y-6 p-6">
	<h1 class="text-2xl font-semibold tracking-tight">Remote assist</h1>
	<p class="text-muted-foreground text-sm leading-relaxed">
		Share your screen with a colleague over a secure connection, or join a session with a join code. Requires a
		modern browser with WebRTC. For remote mouse control on the host machine, use the Tyresoles desktop app
		(Windows).
	</p>
	<div class="flex flex-col gap-3 sm:flex-row">
		<Button href="/assist/host" variant="default" class="flex-1">Share screen (host)</Button>
		<Button href="/assist/join" variant="outline" class="flex-1">Join session (viewer)</Button>
	</div>
	<p class="text-muted-foreground text-xs">Path: {$page.url.pathname}</p>
</div>
