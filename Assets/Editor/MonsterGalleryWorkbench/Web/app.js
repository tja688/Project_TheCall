(() => {
  const token = new URLSearchParams(location.hash.replace(/^#/, "")).get("token") || "";
  let revision = 0;
  let state = null;
  let busy = false;
  let polling = false;
  const images = new Map();

  const badge = document.getElementById("connectionBadge");
  const status = document.getElementById("statusLine");
  const errorLine = document.getElementById("errorLine");
  const portrait = document.getElementById("portrait");
  const empty = document.getElementById("empty");
  const summary = document.getElementById("summary");
  const facts = document.getElementById("facts");
  const parts = document.getElementById("parts");
  const strip = document.getElementById("strip");
  const filters = document.getElementById("filters");

  function setError(message) {
    errorLine.textContent = message || "";
  }

  async function api(path, options = {}) {
    const headers = Object.assign({ Authorization: "Bearer " + token }, options.headers || {});
    const response = await fetch(path, Object.assign({}, options, { headers }));
    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* not json */ }
    if (!response.ok) {
      const message = (json && (json.error || json.message)) || (path + " -> " + response.status);
      throw new Error(message);
    }
    return json;
  }

  let requestSeq = 0;
  async function command(name, payload = {}) {
    const requestId = "r" + (++requestSeq);
    const result = await api("/api/command", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ requestId, command: name, payload }),
    });
    if (!result.ok) throw new Error(result.error || (name + " 失败"));
    if (typeof result.revision === "number") revision = result.revision;
    if (result.payload) {
      state = result.payload;
      render();
    }
    return result;
  }

  function applyEnvelope(envelope) {
    if (!envelope || typeof envelope.revision !== "number") return;
    if (envelope.type === "idle") return;
    if (envelope.type === "snapshot") {
      if (envelope.protocolVersion !== 1) {
        setError("抽查台协议已变化，请重新从菜单打开。");
        return;
      }
      revision = envelope.revision;
      state = envelope.payload;
      render();
      return;
    }
    if (envelope.type === "delta") {
      if (envelope.revision !== revision + 1) {
        refreshSnapshot();
        return;
      }
      revision = envelope.revision;
      Object.assign(state || (state = {}), envelope.payload || {});
      render();
    }
  }

  async function refreshSnapshot() {
    const snapshot = await api("/api/snapshot");
    applyEnvelope(snapshot);
  }

  function currentSource() {
    const pressed = filters.querySelector("[aria-pressed='true']");
    return pressed ? pressed.getAttribute("data-source") : "any";
  }

  let optionKey = "";
  const choice = (name) => document.getElementById(name + "-choice");
  function populate(select, items) {
    select.replaceChildren(...items.map((item) => {
      const option = document.createElement("option");
      option.value = item.id; option.textContent = item.name; return option;
    }));
  }
  function swatches() {
    const palette = state?.options?.palettes[Number(choice("palette").value)];
    const host = document.getElementById("palette-preview"); host.replaceChildren(); if (!palette) return;
    for (const [key, label] of [["fill", "填充颜色"]]) {
      const item = document.createElement("div");
      const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      svg.setAttribute("viewBox", "0 0 64 28");
      const rect = document.createElementNS(svg.namespaceURI, "rect");
      rect.setAttribute("width", "64"); rect.setAttribute("height", "28"); rect.setAttribute("rx", "5"); rect.setAttribute("fill", "#" + palette[key]); svg.append(rect);
      const text = document.createElement("small"); text.textContent = label + " #" + palette[key]; item.append(svg, text); host.append(item);
    }
  }
  document.getElementById("preview-controls").addEventListener("submit", (event) => {
    event.preventDefault();
    const payload = {};
    for (const key of ["template", "palette"]) payload[key] = Number(choice(key).value);
    for (const key of ["personality", "mood"]) payload[key] = choice(key).value;
    run(() => command("preview", payload));
  });
  choice("palette").addEventListener("change", swatches);

  function render() {
    badge.textContent = state ? "已连接" : "未连接";
    badge.dataset.ok = state ? "1" : "0";
    status.textContent = state ? ("记录 " + revision) : "等待画面";
    if (!state) return;
    if (state.error) setError(state.error);
    filters.querySelectorAll("button").forEach((button) => {
      button.setAttribute("aria-pressed", button.getAttribute("data-source") === state.filter ? "true" : "false");
    });
    if (state.options) {
      const key = JSON.stringify(state.options);
      if (key !== optionKey) {
        populate(choice("template"), state.options.templates); populate(choice("palette"), state.options.palettes);
        populate(choice("personality"), state.options.personalities.map((name) => ({id: name, name})));
        populate(choice("mood"), state.options.moods.map((name) => ({id: name, name}))); optionKey = key;
      }
      if (state.selection) for (const [key, value] of Object.entries(state.selection)) choice(key).value = value;
      swatches();
    }
    const specimen = state.specimen;
    if (specimen && specimen.png) images.set(specimen.id, specimen.png);
    if (specimen && images.get(specimen.id)) {
      portrait.src = "data:image/png;base64," + images.get(specimen.id);
      portrait.alt = specimen.summary || "怪物立绘";
      empty.hidden = true;
    } else {
      portrait.removeAttribute("src");
      empty.hidden = false;
    }
    summary.textContent = specimen ? specimen.summary : "—";
    facts.replaceChildren();
    (specimen && specimen.facts || []).forEach((fact) => {
      const term = document.createElement("dt");
      term.textContent = fact.label;
      const value = document.createElement("dd");
      value.textContent = fact.value;
      facts.append(term, value);
    });
    parts.replaceChildren();
    (specimen && specimen.parts || []).forEach((part) => {
      const row = document.createElement("div");
      [part.order, part.name, part.art, part.at, part.mirror ? "镜像" + (part.turn ? " " + part.turn : "") : part.turn]
        .forEach((text) => {
          const cell = document.createElement("span");
          cell.textContent = text || "";
          row.append(cell);
        });
      parts.append(row);
    });
    strip.replaceChildren();
    (state.items || []).forEach((item) => {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = item.channel + " " + item.summary;
      if (item.id === state.currentId) button.setAttribute("aria-current", "true");
      button.addEventListener("click", () => run(() => command("show", { id: item.id })));
      strip.append(button);
    });
    strip.querySelector("[aria-current='true']")?.scrollIntoView({ inline: "nearest", block: "nearest" });
  }

  function neighbor(step) {
    const items = (state && state.items) || [];
    const index = items.findIndex((item) => item.id === state.currentId);
    const next = items[index + step];
    return next ? next.id : null;
  }

  async function run(action) {
    if (busy) return;
    busy = true;
    document.querySelectorAll("button").forEach((button) => { button.disabled = true; });
    try {
      await action();
      if (!(state && state.error)) setError("");
    } catch (error) {
      setError(error.message || String(error));
    } finally {
      busy = false;
      document.querySelectorAll("button").forEach((button) => { button.disabled = false; });
      render();
    }
  }

  document.getElementById("roll").addEventListener("click", () => {
    run(() => command("roll", { source: currentSource() }));
  });
  document.getElementById("pose").addEventListener("click", () => {
    if (!state || state.currentId == null) return;
    run(() => command("pose", { id: state.currentId }));
  });
  document.getElementById("previous").addEventListener("click", () => {
    const id = neighbor(-1);
    if (id != null) run(() => command("show", { id }));
  });
  document.getElementById("next").addEventListener("click", () => {
    const id = neighbor(1);
    if (id != null) run(() => command("show", { id }));
    else run(() => command("roll", { source: currentSource() }));
  });
  filters.addEventListener("click", (event) => {
    const button = event.target.closest("button");
    if (!button) return;
    filters.querySelectorAll("button").forEach((item) => {
      item.setAttribute("aria-pressed", item === button ? "true" : "false");
    });
  });
  document.addEventListener("keydown", (event) => {
    if (event.target instanceof HTMLInputElement || event.target instanceof HTMLTextAreaElement || event.target instanceof HTMLSelectElement) return;
    if (event.key === " " || event.key === "n" || event.key === "N") {
      event.preventDefault();
      document.getElementById("roll").click();
    } else if (event.key === "ArrowLeft") {
      event.preventDefault();
      document.getElementById("previous").click();
    } else if (event.key === "ArrowRight") {
      event.preventDefault();
      document.getElementById("next").click();
    } else if (event.key === "p" || event.key === "P") {
      event.preventDefault();
      document.getElementById("pose").click();
    }
  });

  async function connectStream() {
    await refreshSnapshot();
    if (!state.webSockets) { longPollLoop(); return; }
    let failed = false;
    try {
      const socket = new WebSocket((location.protocol === "https:" ? "wss://" : "ws://") + location.host + "/stream");
      socket.addEventListener("open", () => {
        socket.send(JSON.stringify({ type: "hello", token, afterRevision: revision }));
      });
      socket.addEventListener("message", (event) => {
        try { applyEnvelope(JSON.parse(event.data)); } catch { /* ignore */ }
      });
      socket.addEventListener("error", () => { failed = true; });
      socket.addEventListener("close", () => {
        if (failed) longPollLoop();
        else setTimeout(connectStream, 1000);
      });
    } catch {
      longPollLoop();
    }
  }

  async function longPollLoop() {
    if (polling) return;
    polling = true;
    for (;;) {
      try {
        const envelope = await api("/api/events?afterRevision=" + revision + "&timeoutMs=25000");
        if (envelope) applyEnvelope(envelope);
      } catch (error) {
        setError(error.message || String(error));
        await new Promise((resolve) => setTimeout(resolve, 1000));
      }
    }
  }

  if (!token) {
    badge.textContent = "没有口令";
    status.textContent = "请从菜单 TheCall/Monster/打开立绘抽查台 打开";
    return;
  }

  connectStream().catch((error) => {
    badge.textContent = "连接失败";
    setError(error.message || String(error));
  });
})();
