import { clsx, type ClassValue } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
	return twMerge(clsx(inputs));
}

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type WithoutChild<T> = T extends { child?: any } ? Omit<T, "child"> : T;
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type WithoutChildren<T> = T extends { children?: any } ? Omit<T, "children"> : T;
export type WithoutChildrenOrChild<T> = WithoutChildren<WithoutChild<T>>;
export type WithElementRef<T, U extends HTMLElement = HTMLElement> = T & { ref?: U | null };

/**
 * Generates initials from a full name or username.
 * e.g. "John Doe" -> "JD", "Admin" -> "A"
 */
export function getInitials(fullName?: string, userName?: string): string {
	if (fullName) {
		const parts = fullName.trim().split(/\s+/);
		if (parts.length >= 2) {
			return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
		}
		return fullName.charAt(0).toUpperCase();
	}
	return (userName || 'U').charAt(0).toUpperCase();
}

/**
 * Transforms technical error objects/messages into user-friendly strings.
 * Handles GraphQL/Network timeouts and raw JSON dumps.
 */
export function getFriendlyError(rawError: unknown): string {
	const message = typeof rawError === 'string' ? rawError : rawError instanceof Error ? rawError.message : 'Unknown error';
	
	// Handle HotChocolate timeout (HC0045)
	if (message.includes('HC0045') || message.includes('exceeded the configured timeout')) {
		return 'The request timed out. The server is taking too long to respond.';
	}
	
	// If it looks like a JSON error dump
	if (message.includes('{"response":') || (message.includes('"errors":') && message.includes('"message":'))) {
		return 'A server error occurred. Please try again later.';
	}

	return message;
}
