import type { Node } from '@xyflow/svelte';
import type {
	InputChannelDefinition,
	NodePortDefinition,
	NodePropertyDefinition
} from '$lib/lucalights';

export interface EditorDeviceOption {
	id: string;
	name: string;
}

export interface EditorSegmentOption {
	id: string;
	name: string;
	deviceId: string;
	deviceName: string;
}

export interface EditorNodeData {
	[key: string]: unknown;
	label: string;
	typeId: string;
	category: string;
	description: string;
	properties: Record<string, unknown>;
	propertyDefs: NodePropertyDefinition[];
	inputs: NodePortDefinition[];
	outputs: NodePortDefinition[];
	connectedInputIds: string[];
	inputChannelOptions: InputChannelDefinition[];
	deviceOptions: EditorDeviceOption[];
	segmentOptions: EditorSegmentOption[];
}

export type EditorFlowNode = Node<EditorNodeData>;

export interface EditorNodeActions {
	updateNodeProperty: (nodeId: string, key: string, value: unknown) => void;
}

export const editorNodeActionsContext = Symbol('editor-node-actions');
