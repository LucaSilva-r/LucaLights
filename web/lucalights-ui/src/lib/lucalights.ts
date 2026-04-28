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
	layout: LedLayoutPoint[];
}

export type DeviceProtocol = number | 'DDP' | 'UdpRealtime';

export interface LedLayoutPoint {
	x: number;
	y: number;
}

export interface Device {
	id: string;
	name: string;
	ip: string;
	protocol: DeviceProtocol;
	segments: Segment[];
}

export interface SegmentPlacement {
	segmentId: string;
	x: number;
	y: number;
	rotation: number;
	scaleX: number;
	scaleY: number;
}

export interface RoomLayout {
	placements: SegmentPlacement[];
}

export interface SystemStatus {
	lighting: {
		running: boolean;
	};
	settings: {
		devices: number;
		graphNodes: number;
		graphConnections: number;
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

export type InputValueType = number | 'Bool' | 'Float' | 'Color' | 'String';

export interface InputChannelDefinition {
	key: string;
	label: string;
	group: string;
	moduleId?: string;
	moduleDisplayName?: string;
	valueType: InputValueType;
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

export interface NodePortDefinition {
	id: string;
	label: string;
	valueType: string;
	direction: string;
	description: string;
	allowMultipleConnections?: boolean;
}

export interface NodePropertyDefinition {
	key: string;
	label: string;
	valueType: string;
	description: string;
	defaultValue?: unknown;
	minFloatValue?: number | null;
	maxFloatValue?: number | null;
}

export interface NodeTypeDefinition {
	typeId: string;
	displayName: string;
	category: string;
	description: string;
	inputs: NodePortDefinition[];
	outputs: NodePortDefinition[];
	properties: NodePropertyDefinition[];
}

export interface NodeTypesResponse {
	schemaVersion: number;
	nodeTypes: NodeTypeDefinition[];
}

export interface SvelteFlowPosition {
	x: number;
	y: number;
}

export interface SvelteFlowNodeData {
	properties: Record<string, unknown>;
}

export interface SvelteFlowNode {
	id: string;
	type: string;
	position: SvelteFlowPosition;
	data: SvelteFlowNodeData;
}

export interface SvelteFlowEdge {
	id: string;
	source: string;
	sourceHandle: string;
	target: string;
	targetHandle: string;
}

export interface SvelteFlowViewport {
	x: number;
	y: number;
	zoom: number;
}

export interface SvelteFlowGraphDocument {
	nodes: SvelteFlowNode[];
	edges: SvelteFlowEdge[];
	viewport: SvelteFlowViewport;
}

export interface GraphDiagnostic {
	severity: string;
	message: string;
	nodeId?: string | null;
}

export interface GraphValidationResult {
	isValid: boolean;
	diagnostics: GraphDiagnostic[];
}

export interface GraphResponse {
	graph: SvelteFlowGraphDocument;
	validation: GraphValidationResult;
	evaluationOrder: string[];
}

export type RgbColor = [number, number, number];

export interface PreviewSegment {
	id: string;
	name: string;
	length: number;
	colors: RgbColor[];
}

export interface PreviewTopologySegment {
	id: string;
	name: string;
	length: number;
	sampledLedOffset: number;
	sampledLedCount: number;
}

export interface PreviewDevice {
	id: string;
	name: string;
	ip: string;
	protocol: DeviceProtocol;
	ledCount: number;
	segments: PreviewSegment[];
}

export interface PreviewTopologyDevice {
	id: string;
	name: string;
	ip: string;
	protocol: DeviceProtocol;
	ledCount: number;
	segments: PreviewTopologySegment[];
}

export interface PreviewTopology {
	totalLedCount: number;
	maxPreviewLedsPerSegment: number;
	devices: PreviewTopologyDevice[];
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

export async function apiDelete<T>(path: string): Promise<T> {
	const response = await fetch(path, {
		method: 'DELETE',
		headers: {
			accept: 'application/json'
		}
	});

	if (!response.ok) {
		throw new Error(await buildErrorMessage(response, path));
	}

	if (response.status === 204) {
		return undefined as T;
	}

	return (await response.json()) as T;
}

export async function apiPut<T>(path: string, body?: unknown): Promise<T> {
	const response = await fetch(path, {
		method: 'PUT',
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
	const socket = new WebSocket(`${protocol}//${window.location.host}${path}`);
	socket.binaryType = 'arraybuffer';
	return socket;
}

export function previewPayloadFromTopology(
	topology: PreviewTopology,
	frame: ArrayBuffer | Uint8Array
): PreviewPayload {
	const bytes = frame instanceof Uint8Array ? frame : new Uint8Array(frame);

	return {
		totalLedCount: topology.totalLedCount,
		maxPreviewLedsPerSegment: topology.maxPreviewLedsPerSegment,
		devices: topology.devices.map(
			(device): PreviewDevice => ({
				id: device.id,
				name: device.name,
				ip: device.ip,
				protocol: device.protocol,
				ledCount: device.ledCount,
				segments: device.segments.map(
					(segment): PreviewSegment => ({
						id: segment.id,
						name: segment.name,
						length: segment.length,
						colors: readPreviewColors(bytes, segment.sampledLedOffset, segment.sampledLedCount)
					})
				)
			})
		)
	};
}

function readPreviewColors(bytes: Uint8Array, ledOffset: number, ledCount: number): RgbColor[] {
	const colors: RgbColor[] = [];
	const startByte = ledOffset * 3;

	for (let i = 0; i < ledCount; i += 1) {
		const offset = startByte + i * 3;
		colors.push([
			offset < bytes.length ? bytes[offset] : 0,
			offset + 1 < bytes.length ? bytes[offset + 1] : 0,
			offset + 2 < bytes.length ? bytes[offset + 2] : 0
		]);
	}

	return colors;
}

export function entriesOf<T>(record: Record<string, T> | null | undefined): Array<[string, T]> {
	return Object.entries(record ?? {}).sort(([left], [right]) => left.localeCompare(right));
}

export function rgb(value: RgbColor | ColorValue): string {
	const [red, green, blue] = Array.isArray(value) ? value : [value.r, value.g, value.b];
	return `rgb(${red} ${green} ${blue})`;
}

export const DEVICE_PROTOCOL_OPTIONS = [
	{ value: 0, label: 'DDP' },
	{ value: 1, label: 'UDP Realtime' }
] as const;

export function normalizeProtocol(value: DeviceProtocol | null | undefined): number {
	if (typeof value === 'number' && Number.isFinite(value)) {
		return value;
	}

	if (typeof value === 'string') {
		switch (value.trim().toLowerCase()) {
			case 'udprealtime':
			case 'udp realtime':
			case 'udp_realtime':
				return 1;
			case 'ddp':
			default:
				return 0;
		}
	}

	return 0;
}

export function protocolLabel(value: DeviceProtocol | null | undefined): string {
	return DEVICE_PROTOCOL_OPTIONS.find((option) => option.value === normalizeProtocol(value))?.label ?? 'DDP';
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
