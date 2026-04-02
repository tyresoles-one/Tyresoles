<script lang="ts">
  import { onMount } from "svelte";
  import { z } from "zod";
  import {
    VenForm,
    FormGenerator,
    type FormSchema,
  } from "$lib/components/venUI/form";
  import { focusManager } from "$lib/components/venUI/form/focus-manager";
  import { Button } from "$lib/components/ui/button";
  import { graphqlMutation } from "$lib/services/graphql";
  import PageHeading from "$lib/components/venUI/page-heading/PageHeading.svelte";
  import { goto } from "$app/navigation";
  import { toast } from "$lib/components/venUI/toast";
  import { authStore } from "$lib/stores/auth";
  import { isTauri } from "$lib/tauri";
  import { getAppConfig } from "$lib/config/runtime";

  import LoginPinField from "./LoginPinField.svelte";

  let appVersion = $state("...");
  let windowsUser = $state("");
  let isServerMode = $state(false);

  // Define login schema - matching GraphQL mutation structure
  const loginSchema = z.object({
    username: z.string().min(1, "Username is required"),
    password: z.string().min(1, "Password is required"),
  });

  type LoginData = z.infer<typeof loginSchema>;

  // Initialize form only on client side to avoid SSR issues with $state runes
  let form: VenForm<LoginData> | null = $state(null);

  onMount(async () => {
    const config = await getAppConfig();
    appVersion = config?.version ?? "0.1.default";
    isServerMode = config?.mode === "Server";

    if (isTauri()) {
      try {
        const { getVersion } = await import("@tauri-apps/api/app");
        appVersion = await getVersion();
      } catch (e) {
        console.warn("Failed to get Tauri version:", e);
      }

      if (isServerMode) {
        try {
          const { invoke } = await import("@tauri-apps/api/core");
          windowsUser = await invoke("get_windows_user");
          if (windowsUser && form) {
            form.setValue("username", windowsUser);
          }
        } catch (e) {
          console.warn("Failed to get windows user:", e);
        }
      }
    }

    form = new VenForm<LoginData>({
      schema: loginSchema,
      onSubmit: async (values) => {
        await handleLogin(values);
      },
    });

    // Re-check windows user if form was just initialized
    if (windowsUser && form) {
      form.setValue("username", windowsUser);
    }
  });

  // Handle login using GraphQL mutation with generated types
  async function handleLogin(values: LoginData) {
    try {
      // Import generated document node - full type safety!
      const { LoginDocument } =
        await import("$lib/services/graphql/generated/types");

      // No manual type definitions needed - types are automatically inferred!
      const result = await graphqlMutation<
        import("$lib/services/graphql/generated/types").LoginMutation,
        import("$lib/services/graphql/generated/types").LoginMutationVariables
      >(LoginDocument, {
        variables: {
          username: values.username,
          password: values.password,
          platform: isTauri() ? "win" : "web",
        },
        skipLoading: true, // Form handles its own loading state
      });

      const loginData = result.data?.login;
      if (result.success && loginData?.success) {
        // Schema: LoginUser has userId; LoginResult has user, token, menus
        const username = String(
          loginData.user?.userId ??
            (loginData as Record<string, unknown>).username ??
            "",
        );
        const expiresAt =
          (loginData as Record<string, unknown>).expiresAt ?? "";
        const requirePasswordChange = loginData.requirePasswordChange ?? false;
        const requirePasswordChangeReason =
          loginData.requirePasswordChangeReason ?? null;

        authStore.set({
          token: loginData.token ?? "",
          username,
          expiresAt: typeof expiresAt === "string" ? expiresAt : "",
          user: loginData.user ?? null,
          menus: loginData.menus ?? null,
          locations: loginData.locations ?? null,
          requirePasswordChange,
          requirePasswordChangeReason,
        });

        if (requirePasswordChange) {
          toast.info("Please set a new password to continue.");
          goto("/change-password");
        } else {
          toast.success("Login successful!");
          goto("/");
        }
      } else {
        if (result.errors?.length) {
          toast.error(result.errors.map((e) => e.message).join(", "));
        } else if (loginData?.message) {
          toast.error(loginData.message);
        }
      }
    } catch (error) {
      const message =
        error instanceof Error
          ? error.message
          : "An error occurred during login";
      toast.error(message);
    }
  }

  // Define form layout schema - matching GraphQL mutation
  const formSchema = $derived<FormSchema>([
    {
      type: "set",
      children: [
        {
          type: "group",
          children: [
            {
              type: "field",
              name: "username",
              label: "Username",
              inputType: "text",
              placeholder: "Enter your username",
              leftIcon: "user",
              clearable: !isServerMode,
              disabled: isServerMode && !!windowsUser,
              description: isServerMode ? "Detected Windows Account" : "Your Reg. Mobile No. / ERP Code",
              onBlur(value, fieldName, form) {
                if (value && value.toLowerCase().startsWith("ts:")) {
                  const formattedValue = value.replace(/^ts:/i, "TYRESOLES\\");
                  form.setValue(fieldName, formattedValue);
                }
              },
            },
            isServerMode && isTauri()
              ? {
                  type: "custom",
                  component: LoginPinField,
                  props: {
                    name: "password",
                    label: "4-Digit PIN",
                  },
                }
              : {
                  type: "field",
                  name: "password",
                  label: "Password",
                  inputType: "password",
                  placeholder: "Enter your password",
                  leftIcon: "lock",
                },
          ],
        },
      ],
    },
  ]);
</script>

<div
  class="flex min-h-screen items-center justify-center bg-[radial-gradient(ellipse_at_top,_var(--tw-gradient-stops))] from-primary/20 via-background to-background p-6 relative overflow-hidden"
>
  <!-- Decorative Background Elements -->
  <div
    class="absolute top-0 left-0 w-full h-full overflow-hidden pointer-events-none opacity-20"
  >
    <div
      class="absolute -top-24 -left-24 w-96 h-96 bg-primary rounded-full blur-[120px]"
    ></div>
    <div
      class="absolute top-1/2 -right-24 w-64 h-64 bg-accent rounded-full blur-[100px]"
    ></div>
  </div>

  <div class="w-full max-w-md z-10">
    <div
      class="border rounded-2xl shadow-2xl bg-card/80 backdrop-blur-xl text-card-foreground p-10 space-y-8 transition-all hover:shadow-accent/5"
    >
      <!-- Header with Logo -->
      <div class="text-center space-y-4">
        <div class="flex justify-center">
          <img
            src="/tyresoles-logo.png"
            alt="Tyresoles Logo"
            class="h-12 w-auto drop-shadow-md select-none transition-transform hover:scale-105 duration-300"
          />
        </div>
        <div class="space-y-1">
          <h1 class="text-3xl font-black tracking-tighter text-foreground">
            Welcome Back
          </h1>
          <p class="text-sm text-muted-foreground font-medium">
            Enter your credentials to access your account
          </p>
        </div>
      </div>

      <!-- Form -->
      {#if form}
        <form
          onsubmit={(e) => {
            e.preventDefault();
            form?.submit();
          }}
          class="space-y-6"
          use:focusManager={{ autoFocus: true }}
        >
          <div class="space-y-4">
            <FormGenerator schema={formSchema} {form} root={false} />
          </div>

          <!-- Submit Button -->
          <div class="space-y-4 pt-2">
            <Button
              type="submit"
              class="w-full h-12 text-base font-bold transition-all duration-300 hover:scale-[1.02] active:scale-[0.98] shadow-lg shadow-primary/20"
              disabled={form?.isSubmitting}
            >
              {#if form?.isSubmitting}
                <span class="flex items-center gap-2">
                  <svg
                    class="animate-spin h-5 w-5"
                    xmlns="http://www.w3.org/2000/svg"
                    fill="none"
                    viewBox="0 0 24 24"
                  >
                    <circle
                      class="opacity-25"
                      cx="12"
                      cy="12"
                      r="10"
                      stroke="currentColor"
                      stroke-width="4"
                    ></circle>
                    <path
                      class="opacity-75"
                      fill="currentColor"
                      d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                    ></path>
                  </svg>
                  Authenticating...
                </span>
              {:else}
                Login Now
              {/if}
            </Button>

            <!-- Links -->
            <div class="flex items-center justify-between px-1">
              <a
                href="/change-password?mode=forgot"
                class="text-md text-muted-foreground hover:text-accent font-medium transition-colors"
                tabindex="0"
              >
                Forgot your password?
              </a>
              <div class="flex flex-col items-end">
                {#if isServerMode && windowsUser}
                  <span class="text-[10px] text-accent font-bold uppercase tracking-wider mb-0.5">
                    User: {windowsUser}
                  </span>
                {/if}
                <span
                  class="text-[10px] text-muted-foreground opacity-50 font-mono"
                >
                  v{appVersion}
                </span>
              </div>
            </div>
          </div>
        </form>
      {:else}
        <div class="flex items-center justify-center py-12">
          <div
            class="animate-spin h-8 w-8 border-3 border-accent border-t-transparent rounded-full shadow-sm"
          ></div>
        </div>
      {/if}
    </div>
  </div>
</div>
