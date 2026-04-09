export interface GraphViewport {
	x: number;
	y: number;
	zoom: number;
}

export interface NodeGraph {
	nodes: Array<unknown>;
	connections: Array<unknown>;
	viewport: GraphViewport;
}

export interface Segment {
	id: string;
	name: string;
	groupIds: number[];
	length: number;
}

export interface Device {
	id: string;
	name: string;
	ip: string;
	protocol: string;
	segments: Segment[];
}

export interface Effect {
	id: string;
	name: string;
	graph: NodeGraph;
}

export interface SystemStatus {
	lighting: {
		running: boolean;
	};
	settings: {
		devices: number;
		effects: number;
		activeEffectId: string | null;
		activeInputModuleId: string;
		dirty: boolean;
	};
	input: {
		activeModuleId: string | null;
		connected: boolean;
		active: boolean;
		sequence: number;
		timestampUtc: string | null;
	};
}

export interface InputChannelDefinition {
	key: string;
	label: string;
	valueType: string;
	category: string;
	description: string;
	defaultFloatValue?: number | null;
	minFloatValue?: number | null;
	maxFloatValue?: number | null;
}

export interface InputDefinition {
	moduleId: string;
	displayName: string;
	channels: InputChannelDefinition[];
}

export interface ColorValue {
	r: number;
	g: number;
	b: number;
}

export interface InputSnapshot {
	timestampUtc: string;
	sequence: number;
	isConnected: boolean;
	isActive: boolean;
	boolValues: Record<string, boolean>;
	floatValues: Record<string, number>;
	colorValues: Record<string, ColorValue>;
	metadata: Record<string, string>;
}

export interface NodeTypeDescriptor {
	id: string;
	displayName?: string;
}

export interface NodeTypesResponse {
	schemaVersion: number;
	nodeTypes: NodeTypeDescriptor[];
}

export type RgbColor = [number, number, number];

export interface PreviewSegment {
	id: string;
	name: string;
	length: number;
	colors: RgbColor[];
}

export interface PreviewDevice {
	id: string;
	name: string;
	ip: string;
	protocol: string;
	ledCount: number;
	segments: PreviewSegment[];
}

export interface PreviewPayload {
	frameIndex?: number | null;
	totalElapsedMs?: number | null;
	totalLedCount: number;
	maxPreviewLedsPerSegment: number;
	devices: PreviewDevice[];
}

export interface RuntimeEnvelope<T = unknown> {
	type: string;
	timestampUtc: string;
	payload: T;
}

export async function apiGet<T>(path: string): Promise<T> {
	const response = await fetch(path, {
		headers: {
			accept: 'application/json'
		}
	});

	if (!response.ok) {
		throw new Error(await buildErrorMessage(response, path));
	}

	return (await response.json()) as T;
}

export async function apiPost<T>(path: string, body?: unknown): Promise<T> {
	const response = await fetch(path, {
		method: 'POST',
		headers: {
			accept: 'application/json',
			'content-type': 'application/json'
		},
		body: body === undefined ? undefined : JSON.stringify(body)
	});

	if (!response.ok) {
		throw new Error(await buildErrorMessage(response, path));
	}

	if (response.status === 204) {
		return undefined as T;
	}

	return (await response.json()) as T;
}

export function createSocket(path: string): WebSocket {
	if (typeof window === 'undefined') {
		throw new Error('WebSocket connections can only be created in the browser.');
	}

	const protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
	return new WebSocket(`${protocol}//${window.location.host}${path}`);
}

export function entriesOf<T>(record: Record<string, T> | null | undefined): Array<[string, T]> {
	return Object.entries(record ?? {}).sort(([left], [right]) => left.localeCompare(right));
}

export function rgb(value: RgbColor | ColorValue): string {
	const [red, green, blue] = Array.isArray(value) ? value : [value.r, value.g, value.b];
	return `rgb(${red} ${green} ${blue})`;
}

export function toMessage(error: unknown): string {
	if (error instanceof Error) {
		return error.message;
	}

	if (typeof error === 'string' && error.trim().length > 0) {
		return error;
	}

	return 'An unexpected error occurred.';
}

export function formatAge(timestampUtc: string | null | undefined): string {
	if (!timestampUtc) {
		return 'Waiting for data';
	}

	const ageInMs = Math.max(0, Date.now() - new Date(timestampUtc).getTime());

	if (ageInMs < 1_000) {
		return 'Just now';
	}

	if (ageInMs < 60_000) {
		return `${Math.round(ageInMs / 1_000)}s ago`;
	}

	if (ageInMs < 3_600_000) {
		return `${Math.round(ageInMs / 60_000)}m ago`;
	}

	return `${Math.round(ageInMs / 3_600_000)}h ago`;
}

async function buildErrorMessage(response: Response, path: string): Promise<string> {
	const fallback = `${response.status} ${response.statusText} while calling ${path}`;

	try {
		const details = (await response.text()).trim();
		return details.length > 0 ? `${fallback}: ${details}` : fallback;
	} catch {
		return fallback;
	}
}
