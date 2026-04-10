export type ThemePreference = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'lucalights-theme';

function getSystemPreference(): 'light' | 'dark' {
	if (typeof window === 'undefined') return 'light';
	return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function apply(resolved: 'light' | 'dark') {
	if (typeof document === 'undefined') return;
	document.documentElement.classList.toggle('dark', resolved === 'dark');
}

class ThemeState {
	preference = $state<ThemePreference>('system');

	get resolved(): 'light' | 'dark' {
		return this.preference === 'system' ? getSystemPreference() : this.preference;
	}

	constructor() {
		if (typeof window === 'undefined') return;

		const stored = localStorage.getItem(STORAGE_KEY);
		if (stored === 'light' || stored === 'dark' || stored === 'system') {
			this.preference = stored;
		}

		apply(this.resolved);

		window
			.matchMedia('(prefers-color-scheme: dark)')
			.addEventListener('change', () => {
				if (this.preference === 'system') {
					apply(this.resolved);
				}
			});
	}

	toggle() {
		const next: ThemePreference = this.resolved === 'dark' ? 'light' : 'dark';
		this.preference = next;
		localStorage.setItem(STORAGE_KEY, next);
		apply(next);
	}
}

export const theme = new ThemeState();
