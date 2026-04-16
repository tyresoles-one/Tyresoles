import { describe, it, expect, vi } from "vitest";

vi.mock("$lib/config/system", () => ({
	getBackendBaseUrl: () => "https://api.example.com",
}));

describe("buildRemoteAssistWebSocketUrl", () => {
	it("uses wss and includes token, session, role", async () => {
		const { buildRemoteAssistWebSocketUrl } = await import("./signaling");
		const u = buildRemoteAssistWebSocketUrl(
			"tok",
			"550e8400-e29b-41d4-a716-446655440000",
			"host",
		);
		expect(u.startsWith("wss://api.example.com/ws/remote-assist?")).toBe(true);
		expect(u).toContain("access_token=tok");
		expect(u).toContain("role=host");
	});
});
