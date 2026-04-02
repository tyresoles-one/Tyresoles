import { dialogService } from "$lib/components/venUI/dialog/service.svelte";

export function dialogShow(opts: {
	title: string;
	description?: string;
	actionLabel?: string;
	onAction?: () => void | Promise<void>;
	onCancel?: () => void;
}) {
	void dialogService
		.open({
			title: opts.title,
			description: opts.description,
			actions: [
				{ label: "Cancel", value: false, variant: "outline" },
				{ label: opts.actionLabel ?? "OK", value: true, variant: "default" }
			]
		})
		.then((result) => {
			if (result) void opts.onAction?.();
			else opts.onCancel?.();
		});
}

export function dialogHide() {
	const dialogs = dialogService.dialogs;
	if (dialogs.length > 0) {
		const last = dialogs[dialogs.length - 1];
		dialogService._dismiss(last.id, undefined);
	}
}
