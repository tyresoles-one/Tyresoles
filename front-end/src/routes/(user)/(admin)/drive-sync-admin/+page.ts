import { redirect } from '@sveltejs/kit';
import type { PageLoad } from './$types';

/** Drive sync is configured per user on Nav Live User via Admin → Users → [user] → Google Drive Settings. */
export const load: PageLoad = () => {
	throw redirect(302, '/users');
};
