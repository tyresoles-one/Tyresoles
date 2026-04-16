import { render, screen } from '@testing-library/svelte';
import { describe, it, expect } from 'vitest';
import DetailItem from './DetailItem.svelte';

describe('DetailItem', () => {
	it('renders label and value', () => {
		render(DetailItem, { props: { label: 'Vendor', value: 'V-001' } });
		expect(screen.getByText('Vendor')).toBeInTheDocument();
		expect(screen.getByText('V-001')).toBeInTheDocument();
	});

	it('shows em dash when value is null', () => {
		render(DetailItem, { props: { label: 'Note', value: null } });
		expect(screen.getByText('—')).toBeInTheDocument();
	});
});
