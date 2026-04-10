#!/usr/bin/env bash

set -euo pipefail

port=""
dry_run=0

usage() {
	cat <<'EOF'
Usage: scripts/kill-lucalights-dev.sh [--port PORT] [--dry-run]

Stops local LucaLights server processes launched as:
  dotnet ./LucaLights.Server.dll

Options:
  --port PORT   Also stop any listener bound to the given TCP port.
  --dry-run     Show matching processes without stopping them.
  --help        Show this help text.
EOF
}

while [[ $# -gt 0 ]]; do
	case "$1" in
		--port)
			if [[ $# -lt 2 ]]; then
				echo "Missing value for --port" >&2
				exit 1
			fi
			port="$2"
			shift 2
			;;
		--dry-run)
			dry_run=1
			shift
			;;
		--help|-h)
			usage
			exit 0
			;;
		*)
			echo "Unknown argument: $1" >&2
			usage >&2
			exit 1
			;;
	esac
done

if [[ -n "$port" ]] && ! [[ "$port" =~ ^[0-9]+$ ]]; then
	echo "Port must be numeric: $port" >&2
	exit 1
fi

declare -A seen_pids=()
pids=()

add_pid() {
	local pid="$1"
	if [[ -n "$pid" ]] && [[ -z "${seen_pids[$pid]:-}" ]]; then
		seen_pids["$pid"]=1
		pids+=("$pid")
	fi
}

while IFS= read -r pid; do
	add_pid "$pid"
done < <(
	ps -eo pid=,command= |
		awk '
			/LucaLights\.Server\.dll/ && !/awk/ && !/kill-lucalights-dev\.sh/ {
				print $1
			}
		'
)

if [[ -n "$port" ]]; then
	while IFS= read -r pid; do
		add_pid "$pid"
	done < <(lsof -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)
fi

if [[ ${#pids[@]} -eq 0 ]]; then
	echo "No matching LucaLights dev processes found."
	exit 0
fi

echo "Matching LucaLights processes:"
ps -fp "$(IFS=,; echo "${pids[*]}")"

if [[ "$dry_run" -eq 1 ]]; then
	echo
	echo "Dry run only. No processes were stopped."
	exit 0
fi

kill "${pids[@]}"
sleep 1

remaining=()
for pid in "${pids[@]}"; do
	if kill -0 "$pid" 2>/dev/null; then
		remaining+=("$pid")
	fi
done

if [[ ${#remaining[@]} -gt 0 ]]; then
	echo
	echo "Some processes are still running, sending SIGKILL:"
	ps -fp "$(IFS=,; echo "${remaining[*]}")"
	kill -9 "${remaining[@]}"
fi

echo
echo "Stopped ${#pids[@]} LucaLights process(es)."
