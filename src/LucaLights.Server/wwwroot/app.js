const state = {
  modules: [],
  snapshot: null,
  eventSocket: null,
  previewSocket: null,
};

const el = {
  eventStatus: document.querySelector("#eventStatus"),
  previewStatus: document.querySelector("#previewStatus"),
  lightingRunning: document.querySelector("#lightingRunning"),
  activeModule: document.querySelector("#activeModule"),
  deviceCount: document.querySelector("#deviceCount"),
  effectCount: document.querySelector("#effectCount"),
  inputConnected: document.querySelector("#inputConnected"),
  inputActive: document.querySelector("#inputActive"),
  inputSequence: document.querySelector("#inputSequence"),
  inputTimestamp: document.querySelector("#inputTimestamp"),
  activeBoolCount: document.querySelector("#activeBoolCount"),
  activeChannels: document.querySelector("#activeChannels"),
  moduleChannelCount: document.querySelector("#moduleChannelCount"),
  moduleCatalog: document.querySelector("#moduleCatalog"),
  eventLog: document.querySelector("#eventLog"),
  clearEvents: document.querySelector("#clearEvents"),
  previewFrame: document.querySelector("#previewFrame"),
  previewDevices: document.querySelector("#previewDevices"),
};

init();

async function init() {
  el.clearEvents.addEventListener("click", () => {
    el.eventLog.replaceChildren();
  });

  await Promise.all([loadStatus(), loadModules(), loadInputState()]);
  connectEvents();
  connectPreview();
}

async function loadStatus() {
  const status = await fetchJson("/api/system/status");
  el.lightingRunning.textContent = status.lighting.running ? "Running" : "Stopped";
  el.activeModule.textContent = status.input.activeModuleId ?? "(none)";
  el.deviceCount.textContent = status.settings.devices;
  el.effectCount.textContent = status.settings.effects;
}

async function loadModules() {
  state.modules = await fetchJson("/api/input-modules");
  renderModuleCatalog();
}

async function loadInputState() {
  renderSnapshot(await fetchJson("/api/input-state"));
}

function connectEvents() {
  const socket = new WebSocket(toWsUrl("/ws/events"));
  state.eventSocket = socket;
  setPill(el.eventStatus, "Events connecting", "warn");

  socket.addEventListener("open", () => setPill(el.eventStatus, "Events live", "ok"));
  socket.addEventListener("close", () => {
    setPill(el.eventStatus, "Events disconnected", "bad");
    window.setTimeout(connectEvents, 1500);
  });
  socket.addEventListener("error", () => setPill(el.eventStatus, "Events error", "bad"));
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);
    appendEvent(message);

    if (message.type === "input.snapshot") {
      renderSnapshot(message.payload);
    }

    if (message.type === "input.moduleChanged") {
      el.activeModule.textContent = message.payload.activeModuleId ?? "(none)";
    }

    if (message.type === "settings.changed") {
      el.deviceCount.textContent = message.payload.devices;
      el.effectCount.textContent = message.payload.effects;
      el.activeModule.textContent = message.payload.activeInputModuleId ?? "(none)";
    }
  });
}

function connectPreview() {
  const socket = new WebSocket(toWsUrl("/ws/preview"));
  state.previewSocket = socket;
  setPill(el.previewStatus, "Preview connecting", "warn");

  socket.addEventListener("open", () => setPill(el.previewStatus, "Preview live", "ok"));
  socket.addEventListener("close", () => {
    setPill(el.previewStatus, "Preview disconnected", "bad");
    window.setTimeout(connectPreview, 1500);
  });
  socket.addEventListener("error", () => setPill(el.previewStatus, "Preview error", "bad"));
  socket.addEventListener("message", event => {
    const message = JSON.parse(event.data);

    if (message.type === "preview.frame" || message.type === "preview.snapshot" || message.type === "preview.cleared") {
      renderPreview(message.payload);
    }
  });
}

function renderSnapshot(snapshot) {
  state.snapshot = snapshot;

  el.inputConnected.textContent = snapshot.isConnected ? "Yes" : "No";
  el.inputActive.textContent = snapshot.isActive ? "Yes" : "No";
  el.inputSequence.textContent = snapshot.sequence ?? 0;
  el.inputTimestamp.textContent = formatTime(snapshot.timestampUtc);

  const boolValues = snapshot.boolValues ?? {};
  const activeEntries = Object.entries(boolValues)
    .filter(([, value]) => value)
    .sort(([left], [right]) => left.localeCompare(right));

  el.activeBoolCount.textContent = `${activeEntries.length} active`;

  if (activeEntries.length === 0) {
    el.activeChannels.className = "channel-list empty";
    el.activeChannels.textContent = snapshot.isConnected
      ? "Connected, but no boolean channels are active."
      : "No active channels. Waiting for ITGMania data...";
    return;
  }

  el.activeChannels.className = "channel-list";
  el.activeChannels.replaceChildren(...activeEntries.map(([key]) => {
    const channel = findChannel(key);
    const item = document.createElement("div");
    item.className = "channel";
    item.innerHTML = `
      <strong>${escapeHtml(channel?.label ?? key)}</strong>
      <span class="channel-key">${escapeHtml(key)}</span>
    `;
    return item;
  }));
}

function renderModuleCatalog() {
  const totalChannels = state.modules.reduce((total, module) => total + module.channels.length, 0);
  el.moduleChannelCount.textContent = `${totalChannels} channels`;

  if (state.modules.length === 0) {
    el.moduleCatalog.className = "catalog empty";
    el.moduleCatalog.textContent = "No input modules registered.";
    return;
  }

  el.moduleCatalog.className = "catalog";
  el.moduleCatalog.replaceChildren(...state.modules.map(module => {
    const groups = groupBy(module.channels, channel => channel.category ?? "General");
    const section = document.createElement("section");
    section.className = "catalog-group";
    section.innerHTML = `
      <h3>${escapeHtml(module.displayName)}</h3>
      ${Object.entries(groups).map(([category, channels]) => `
        <div class="catalog-category">
          <p class="eyebrow">${escapeHtml(category)}</p>
          <div class="catalog-items">
            ${channels.map(channel => `
              <div class="catalog-item">
                <strong>${escapeHtml(channel.label)}</strong>
                <div class="catalog-key">${escapeHtml(channel.key)}</div>
              </div>
            `).join("")}
          </div>
        </div>
      `).join("")}
    `;
    return section;
  }));
}

function renderPreview(preview) {
  el.previewFrame.textContent = preview.frameIndex === null || preview.frameIndex === undefined
    ? `${preview.totalLedCount} LEDs`
    : `Frame ${preview.frameIndex}`;

  if (!preview.devices || preview.devices.length === 0) {
    el.previewDevices.className = "preview-list empty";
    el.previewDevices.textContent = "No devices configured yet.";
    return;
  }

  el.previewDevices.className = "preview-list";
  el.previewDevices.replaceChildren(...preview.devices.map(device => {
    const wrapper = document.createElement("section");
    wrapper.className = "preview-device";
    wrapper.innerHTML = `
      <h3>${escapeHtml(device.name)} <span class="catalog-key">${escapeHtml(device.protocol)}</span></h3>
      <p class="empty">${escapeHtml(device.ip)} - ${device.ledCount} LEDs</p>
      <div class="preview-segments">
        ${device.segments.map(segment => `
          <div>
            <strong>${escapeHtml(segment.name)}</strong>
            <span class="catalog-key">${segment.length} LEDs</span>
            <div class="led-row">
              ${segment.colors.map(color => `
                <span class="led" title="rgb(${color.join(", ")})" style="background: rgb(${color.join(", ")})"></span>
              `).join("")}
            </div>
          </div>
        `).join("")}
      </div>
    `;
    return wrapper;
  }));
}

function appendEvent(message) {
  const item = document.createElement("li");
  item.innerHTML = `
    <span class="event-type">${escapeHtml(message.type)}</span>
    <time>${formatTime(message.timestampUtc)}</time>
  `;
  el.eventLog.prepend(item);

  while (el.eventLog.children.length > 80) {
    el.eventLog.lastElementChild.remove();
  }
}

function findChannel(key) {
  return state.modules
    .flatMap(module => module.channels)
    .find(channel => channel.key.toLowerCase() === key.toLowerCase());
}

function setPill(node, text, status) {
  node.textContent = text;
  node.className = `pill pill-${status}`;
}

function groupBy(items, keySelector) {
  return items.reduce((groups, item) => {
    const key = keySelector(item);
    groups[key] ??= [];
    groups[key].push(item);
    return groups;
  }, {});
}

async function fetchJson(url) {
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText}`);
  }

  return response.json();
}

function toWsUrl(path) {
  const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
  return `${protocol}//${window.location.host}${path}`;
}

function formatTime(value) {
  if (!value) {
    return "...";
  }

  return new Intl.DateTimeFormat(undefined, {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  }).format(new Date(value));
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
