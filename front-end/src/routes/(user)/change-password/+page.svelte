<script lang="ts">
	import { onMount } from 'svelte';
	import { z } from 'zod';
	import { page } from '$app/stores';
	import { VenForm, FormGenerator, type FormSchema } from '$lib/components/venUI/form';
	import { focusManager } from '$lib/components/venUI/form/focus-manager';
	import { Button } from '$lib/components/ui/button';
	import { graphqlMutation } from '$lib/services/graphql';
	import { goto } from '$app/navigation';
	import { toast } from '$lib/components/venUI/toast';
	import { authStore } from '$lib/stores/auth';
	import { ChangePasswordDocument, ForgotPasswordDocument } from '$lib/services/graphql/generated/types';
	import PinField from '$lib/components/venUI/PinField.svelte';

	const securityPinSchema = z
		.object({
			securityPin: z.string().min(4, 'Security PIN must be at least 4 digits').regex(/^\d+$/, 'Security PIN must be digits only'),
			newPassword: z.string().min(6, 'Password must be at least 6 characters'),
			confirmPassword: z.string().min(1, 'Please confirm your password'),
		})
		.refine((d) => d.newPassword === d.confirmPassword, { message: 'Passwords do not match', path: ['confirmPassword'] });

	const forgotPasswordSchema = z
		.object({
			username: z.string().min(1, 'Username is required'),
			securityPin: z.string().min(4, 'Security PIN must be at least 4 digits').regex(/^\d+$/, 'Security PIN must be digits only'),
			newPassword: z.string().min(6, 'Password must be at least 6 characters'),
			confirmPassword: z.string().min(1, 'Please confirm your password'),
		})
		.refine((d) => d.newPassword === d.confirmPassword, { message: 'Passwords do not match', path: ['confirmPassword'] });

	const currentPasswordSchema = z
		.object({
			oldPassword: z.string().min(1, 'Current password is required'),
			newPassword: z.string().min(6, 'Password must be at least 6 characters'),
			confirmPassword: z.string().min(1, 'Please confirm your password'),
		})
		.refine((d) => d.newPassword === d.confirmPassword, { message: 'Passwords do not match', path: ['confirmPassword'] });

	const SECURITY_PIN_FORM_SCHEMA: FormSchema = [
		{
			type: 'set',
			children: [
				{
					type: 'group',
					children: [
						{ 
							type: 'custom', 
							component: PinField, 
							props: { 
								name: 'securityPin', 
								label: '4-Digit Security PIN' 
							} 
						},
						{ type: 'field', name: 'newPassword', label: 'New password', inputType: 'password', placeholder: 'Enter new password', leftIcon: 'lock' },
						{ type: 'field', name: 'confirmPassword', label: 'Confirm new password', inputType: 'password', placeholder: 'Confirm new password', leftIcon: 'lock' },
					],
				},
			],
		},
	];

	const FORGOT_PASSWORD_FORM_SCHEMA: FormSchema = [
		{
			type: 'set',
			children: [
				{
					type: 'group',
					children: [
						{ type: 'field', name: 'username', label: 'Username', inputType: 'text', placeholder: 'Enter your username', leftIcon: 'user',onBlur(value, fieldName, form) {
								if(value && value.toLowerCase().startsWith('ts:')) {
									const formattedValue = value.replace(/^ts:/i, 'TYRESOLES\\');
									form.setValue(fieldName, formattedValue);
								}
							} },
						{ 
							type: 'custom', 
							component: PinField, 
							props: { 
								name: 'securityPin', 
								label: '4-Digit Security PIN' 
							} 
						},
						{ type: 'field', name: 'newPassword', label: 'New password', inputType: 'password', placeholder: 'Enter new password', leftIcon: 'lock' },
						{ type: 'field', name: 'confirmPassword', label: 'Confirm new password', inputType: 'password', placeholder: 'Confirm new password', leftIcon: 'lock' },
					],
				},
			],
		},
	];

	const CURRENT_PASSWORD_FORM_SCHEMA: FormSchema = [
		{
			type: 'set',
			children: [
				{
					type: 'group',
					children: [
						{ type: 'field', name: 'oldPassword', label: 'Current password', inputType: 'password', placeholder: 'Enter current password', leftIcon: 'lock' },
						{ type: 'field', name: 'newPassword', label: 'New password', inputType: 'password', placeholder: 'Enter new password', leftIcon: 'lock' },
						{ type: 'field', name: 'confirmPassword', label: 'Confirm new password', inputType: 'password', placeholder: 'Confirm new password', leftIcon: 'lock' },
					],
				},
			],
		},
	];

	type SecurityPinData = z.infer<typeof securityPinSchema>;
	type ForgotPasswordData = z.infer<typeof forgotPasswordSchema>;
	type CurrentPasswordData = z.infer<typeof currentPasswordSchema>;

	let form: VenForm<SecurityPinData | ForgotPasswordData | CurrentPasswordData> | null = $state(null);
	let formSchema: FormSchema = $state([]);
	let pageHeading = $state('Change your password');
	let pageDescription = $state('Enter your current password and choose a new one.');
	let isForgotFlow = $state(false);

	onMount(() => {
		const auth = authStore.get();
		const hasToken = !!auth.token;
		const modeForgot = $page.url.searchParams.get('mode') === 'forgot';
		const reason = auth.requirePasswordChangeReason;

		if (!hasToken && !modeForgot) {
			goto('/login');
			return;
		}

		isForgotFlow = !hasToken && modeForgot;

		if (isForgotFlow) {
			formSchema = FORGOT_PASSWORD_FORM_SCHEMA;
			form = new VenForm({
				schema: forgotPasswordSchema,
				onSubmit: async (values: ForgotPasswordData) => {
					await handleChangePassword(values);
				},
			}) as VenForm<SecurityPinData | ForgotPasswordData | CurrentPasswordData>;
			pageHeading = 'Reset your password';
			pageDescription = 'Enter your username, Security PIN and choose a new password.';
			return;
		}

		const pin = reason === 'FirstLogin' || modeForgot;
		formSchema = pin ? SECURITY_PIN_FORM_SCHEMA : CURRENT_PASSWORD_FORM_SCHEMA;
		const schema = pin ? securityPinSchema : currentPasswordSchema;
		form = new VenForm({
			schema,
			onSubmit: async (values: SecurityPinData | CurrentPasswordData) => {
				await handleChangePassword(values);
			},
		}) as VenForm<SecurityPinData | CurrentPasswordData>;
		pageHeading = pin ? (modeForgot ? 'Reset your password' : 'Set your password') : 'Change your password';
		pageDescription = pin
			? modeForgot
				? 'Enter your Security PIN and choose a new password.'
				: 'Enter your Security PIN and choose a new password to continue.'
			: 'Enter your current password and choose a new one.';
	});

	async function handleChangePassword(values: SecurityPinData | ForgotPasswordData | CurrentPasswordData) {
		if (isForgotFlow && 'username' in values) {
			try {
				const result = await graphqlMutation<
					import('$lib/services/graphql/generated/types').ForgotPasswordMutation,
					import('$lib/services/graphql/generated/types').ForgotPasswordMutationVariables
				>(ForgotPasswordDocument, {
					variables: {
						username: values.username,
						securityPin: parseInt(values.securityPin, 10),
						newPassword: values.newPassword,
					},
					skipLoading: true,
				});

				if (result.success && result.data?.forgotPassword?.success) {
					toast.success('Password changed successfully. Please log in.');
					goto('/login');
				} else {
					const msg = result.data?.forgotPassword?.message ?? result.errors?.[0]?.message ?? 'Invalid username or Security PIN.';
					toast.error(msg);
				}
			} catch (error) {
				const message = error instanceof Error ? error.message : 'An error occurred.';
				toast.error(message);
			}
			return;
		}

		const userId = authStore.get().username || authStore.get().user?.userId;
		if (!userId) {
			toast.error('Session expired. Please log in again.');
			goto('/login');
			return;
		}

		const isPin = 'securityPin' in values && values.securityPin != null && !('username' in values);
		const variables: {
			userId: string;
			newPassword: string;
			oldPassword?: string;
			securityPin?: number;
		} = {
			userId,
			newPassword: values.newPassword,
		};
		if (isPin) {
			variables.securityPin = parseInt((values as SecurityPinData).securityPin, 10);
		} else {
			variables.oldPassword = (values as CurrentPasswordData).oldPassword;
		}

		try {
			const result = await graphqlMutation<
				import('$lib/services/graphql/generated/types').ChangePasswordMutation,
				import('$lib/services/graphql/generated/types').ChangePasswordMutationVariables
			>(ChangePasswordDocument, {
				variables,
				skipLoading: true,
			});

			if (result.success && result.data?.changePassword?.success) {
				authStore.set({
					...authStore.get(),
					requirePasswordChange: false,
					requirePasswordChangeReason: null,
				});
				toast.success('Password changed successfully.');
				goto('/');
			} else {
				const msg = result.data?.changePassword?.message ?? result.errors?.[0]?.message ?? 'Failed to change password.';
				toast.error(msg);
			}
		} catch (error) {
			const message = error instanceof Error ? error.message : 'An error occurred.';
			toast.error(message);
		}
	}

</script>

<div class="flex min-h-[60vh] items-center justify-center p-4">
	<div class="w-full max-w-md">
		<div class="border rounded-lg shadow-lg bg-card text-card-foreground p-8 space-y-6">
			<div class="text-center space-y-2">
				<h1 class="text-2xl font-bold tracking-tight">{pageHeading}</h1>
				<p class="text-muted-foreground text-sm">{pageDescription}</p>
			</div>

			{#if form}
				<form
					onsubmit={(e) => {
						e.preventDefault();
						form?.submit();
					}}
					class="space-y-6"
					use:focusManager={{ autoFocus: true }}
				>
					<FormGenerator schema={formSchema} form={form} root={false} />
					<Button type="submit" class="w-full" disabled={form?.isSubmitting} size="lg">
						{#if form?.isSubmitting}
							<span class="flex items-center gap-2">
								<svg class="animate-spin h-4 w-4" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
									<circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
									<path
										class="opacity-75"
										fill="currentColor"
										d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
									></path>
								</svg>
								Updating...
							</span>
						{:else}
							Update password
						{/if}
					</Button>
				</form>
			{:else}
				<div class="flex justify-center py-8">
					<div class="animate-spin h-6 w-6 border-2 border-primary border-t-transparent rounded-full"></div>
				</div>
			{/if}

			{#if isForgotFlow || !authStore.get().requirePasswordChange}
				<div class="text-center text-sm text-muted-foreground">
					<a href={isForgotFlow ? '/login' : '/'} class="text-primary hover:underline">{isForgotFlow ? 'Back to login' : 'Back to home'}</a>
				</div>
			{/if}
		</div>
	</div>
</div>
