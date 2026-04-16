import { describe, it, expect, vi, afterEach } from 'vitest';
import { getFriendlyError, getInitials } from './utils';
import { randomUuidLike } from './utils/randomId';

describe('getInitials', () => {
	it('uses first and last word for full name', () => {
		expect(getInitials('John Doe')).toBe('JD');
	});

	it('uses first letter for single word', () => {
		expect(getInitials('Admin')).toBe('A');
	});

	it('falls back to username when no full name', () => {
		expect(getInitials(undefined, 'superuser')).toBe('S');
	});
});

describe('getFriendlyError', () => {
	it('maps HotChocolate timeout to friendly text', () => {
		expect(getFriendlyError(new Error('HC0045 timeout'))).toContain('timed out');
	});

	it('passes through simple string messages', () => {
		expect(getFriendlyError('Something broke')).toBe('Something broke');
	});
});

describe('randomUuidLike', () => {
	afterEach(() => {
		vi.restoreAllMocks();
	});

	it('uses crypto.randomUUID when available', () => {
		vi.spyOn(crypto, 'randomUUID').mockReturnValue('aaaaaaaa-bbbb-4ccc-dddd-eeeeeeeeeeee');
		expect(randomUuidLike()).toBe('aaaaaaaa-bbbb-4ccc-dddd-eeeeeeeeeeee');
	});
});
