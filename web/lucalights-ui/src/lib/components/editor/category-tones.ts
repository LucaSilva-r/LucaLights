export function categoryHeaderTone(category: string) {
	switch (category) {
		case 'Annotations':
			return 'bg-amber-200 text-amber-950 dark:bg-amber-700 dark:text-amber-100';
		case 'Constants':
			return 'bg-amber-500 text-white';
		case 'Graph Inputs':
			return 'bg-sky-500 text-white';
		case 'Math':
			return 'bg-cyan-600 text-white';
		case 'Logic':
			return 'bg-violet-500 text-white';
		case 'Time':
			return 'bg-orange-500 text-white';
		case 'Color':
			return 'bg-rose-500 text-white';
		case 'Noise':
			return 'bg-fuchsia-500 text-white';
		case 'Segment':
			return 'bg-teal-500 text-white';
		case 'Outputs':
			return 'bg-emerald-500 text-white';
		default:
			return 'bg-zinc-700 text-white';
	}
}

export function categoryTextTone(category: string) {
	switch (category) {
		case 'Annotations':
			return 'text-amber-700 dark:text-amber-300';
		case 'Constants':
			return 'text-amber-600 dark:text-amber-300';
		case 'Graph Inputs':
			return 'text-sky-600 dark:text-sky-300';
		case 'Math':
			return 'text-cyan-600 dark:text-cyan-300';
		case 'Logic':
			return 'text-violet-600 dark:text-violet-300';
		case 'Time':
			return 'text-orange-600 dark:text-orange-300';
		case 'Color':
			return 'text-rose-600 dark:text-rose-300';
		case 'Noise':
			return 'text-fuchsia-600 dark:text-fuchsia-300';
		case 'Segment':
			return 'text-teal-600 dark:text-teal-300';
		case 'Outputs':
			return 'text-emerald-600 dark:text-emerald-300';
		default:
			return 'text-zinc-700 dark:text-zinc-300';
	}
}
