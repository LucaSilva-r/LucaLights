export type HeaderAction = {
	id: string;
	label: string;
	title?: string;
	disabled?: boolean;
	busy?: boolean;
	onClick: () => void;
};

class HeaderActionsState {
	primary = $state<HeaderAction | null>(null);
	help = $state<HeaderAction | null>(null);

	setPrimary(action: HeaderAction) {
		this.primary = action;
	}

	setHelp(action: HeaderAction) {
		this.help = action;
	}

	clearPrimary(id: string) {
		if (this.primary?.id === id) {
			this.primary = null;
		}
	}

	clearHelp(id: string) {
		if (this.help?.id === id) {
			this.help = null;
		}
	}
}

export const headerActions = new HeaderActionsState();
