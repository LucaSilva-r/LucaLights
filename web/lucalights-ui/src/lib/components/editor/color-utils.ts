export interface RgbColor {
	r: number;
	g: number;
	b: number;
}

export interface HsvColor {
	h: number;
	s: number;
	v: number;
}

export function clamp(value: number, min: number, max: number) {
	return Math.min(max, Math.max(min, value));
}

export function normalizeHex(value: string) {
	const trimmed = value.trim().replace(/^#/, '');
	if (/^[0-9a-fA-F]{3}$/.test(trimmed)) {
		return `#${trimmed
			.split('')
			.map((part) => `${part}${part}`)
			.join('')
			.toLowerCase()}`;
	}

	if (/^[0-9a-fA-F]{6}$/.test(trimmed)) {
		return `#${trimmed.toLowerCase()}`;
	}

	return null;
}

export function rgbToHex(color: RgbColor) {
	return (
		'#' +
		[color.r, color.g, color.b]
			.map((channel) =>
				clamp(Math.round(channel), 0, 255).toString(16).padStart(2, '0')
			)
			.join('')
	);
}

export function hexToRgb(value: string) {
	const normalized = normalizeHex(value);
	if (!normalized) {
		return null;
	}

	return {
		r: parseInt(normalized.slice(1, 3), 16),
		g: parseInt(normalized.slice(3, 5), 16),
		b: parseInt(normalized.slice(5, 7), 16)
	};
}

export function rgbToHsv(color: RgbColor): HsvColor {
	const r = clamp(color.r, 0, 255) / 255;
	const g = clamp(color.g, 0, 255) / 255;
	const b = clamp(color.b, 0, 255) / 255;
	const max = Math.max(r, g, b);
	const min = Math.min(r, g, b);
	const delta = max - min;

	let h = 0;
	if (delta > 0) {
		if (max === r) {
			h = 60 * (((g - b) / delta) % 6);
		} else if (max === g) {
			h = 60 * (((b - r) / delta) + 2);
		} else {
			h = 60 * (((r - g) / delta) + 4);
		}
	}

	if (h < 0) {
		h += 360;
	}

	const s = max === 0 ? 0 : delta / max;
	return { h, s, v: max };
}

export function hsvToRgb(color: HsvColor): RgbColor {
	const h = ((color.h % 360) + 360) % 360;
	const s = clamp(color.s, 0, 1);
	const v = clamp(color.v, 0, 1);

	const c = v * s;
	const x = c * (1 - Math.abs(((h / 60) % 2) - 1));
	const m = v - c;

	let r1 = 0;
	let g1 = 0;
	let b1 = 0;

	if (h < 60) {
		r1 = c;
		g1 = x;
	} else if (h < 120) {
		r1 = x;
		g1 = c;
	} else if (h < 180) {
		g1 = c;
		b1 = x;
	} else if (h < 240) {
		g1 = x;
		b1 = c;
	} else if (h < 300) {
		r1 = x;
		b1 = c;
	} else {
		r1 = c;
		b1 = x;
	}

	return {
		r: Math.round((r1 + m) * 255),
		g: Math.round((g1 + m) * 255),
		b: Math.round((b1 + m) * 255)
	};
}
