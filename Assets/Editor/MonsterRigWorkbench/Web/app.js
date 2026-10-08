(() => {
  const token = new URLSearchParams(location.hash.replace(/^#/, "")).get("token") || "";
  const paletteNames = ["珊瑚红", "薄荷绿", "天蓝", "琥珀黄", "薰衣草", "青瓷"];
  const roleLabel = { Primary: "主体", SubPrimary: "次主体", Secondary: "副体" };
  let revision = 0;
  let state = null;
  let zoom = 5;
  let structure = "";
  let playing = true;
  let playStarted = performance.now();
  let view = { minX: 0, maxY: 0, scale: zoom };
  let drag = null;
  let commandGate = Promise.resolve();
  const bitmaps = new Map();
  const tinted = new Map();

  const connection = document.getElementById("connection");
  const banner = document.getElementById("banner");
  const library = document.getElementById("library");
  const inspector = document.getElementById("inspector");
  const canvas = document.getElementById("view");
  const ctx = canvas.getContext("2d");
  const hint = document.getElementById("hint");
  const stageTitle = document.getElementById("stageTitle");

  function mathOk() {
    const turned = transformPoint(4, 4, 90, false, 4, 4, 8, 2);
    const mirrored = transformPoint(4, 2, 0, true, 4, 2, 8, 7);
    const swing = 10 * Math.sin(1 * Math.PI * 2 * 0.25);
    return near(turned.x, 6) && near(turned.y, 8) && near(mirrored.x, 0) && near(mirrored.y, 7) && near(swing, 10);
  }

  function near(value, expected) {
    return Math.abs(value - expected) < 0.001;
  }

  function rotate(x, y, degrees) {
    const radians = degrees * Math.PI / 180;
    const cos = Math.cos(radians);
    const sin = Math.sin(radians);
    return { x: x * cos - y * sin, y: x * sin + y * cos };
  }

  function transformPoint(originX, originY, rotation, mirror, mountX, mountY, pointX, pointY) {
    let x = pointX - mountX;
    let y = pointY - mountY;
    if (mirror) x = -x;
    const turned = rotate(x, y, rotation);
    return { x: originX + turned.x, y: originY + turned.y };
  }

  const formulaOk = mathOk();

  async function api(path, options = {}) {
    const headers = Object.assign({ Authorization: "Bearer " + token }, options.headers || {});
    const response = await fetch(path, Object.assign({}, options, { headers }));
    const text = await response.text();
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch (error) { json = null; }
    if (!response.ok) {
      const message = json && (json.error || json.message) ? (json.error || json.message) : path + " -> " + response.status;
      throw new Error(message);
    }
    return json;
  }

  let requestSeq = 0;
  function command(name, payload = {}) {
    const run = commandGate.then(() => postCommand(name, payload));
    commandGate = run.then(() => {}, () => {});
    return run;
  }

  async function postCommand(name, payload) {
    const requestId = "r" + (++requestSeq);
    const result = await api("/api/command", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ requestId, command: name, payload }),
    });
    if (!result.ok) throw new Error(result.error || name);
    revision = result.revision;
    state = result.payload;
    render();
    return result;
  }

  function applyEnvelope(envelope) {
    if (!envelope || typeof envelope.revision !== "number") return;
    if (envelope.type !== "snapshot" && envelope.payload == null) return;
    if (envelope.revision < revision) return;
    revision = envelope.revision;
    state = envelope.payload || envelope;
    render();
  }

  function partById(id) {
    return (state.parts || []).find((part) => part.id === id) || null;
  }

  function groupById(id) {
    return (state.groups || []).find((group) => group.id === id) || null;
  }

  function selectedPart() {
    return partById(state && state.selectedPartId);
  }

  function render() {
    if (!state) return;
    connection.textContent = state.blocked ? "磁盘有冲突" : (state.dirty ? "已连接 · 未保存" : "已连接 · 已保存");
    connection.dataset.ok = state.blocked ? "0" : "1";
    connection.dataset.dirty = state.dirty ? "1" : "0";
    document.getElementById("turn").classList.toggle("on", !!state.headTurnPreview);
    document.querySelectorAll("#modes button").forEach((button) => {
      button.classList.toggle("on", button.dataset.mode === state.mode);
    });
    document.querySelectorAll("#zoom button").forEach((button) => {
      button.classList.toggle("on", Number(button.dataset.zoom) === zoom);
    });
    const paletteLabel = document.querySelector(".palette");
    if (paletteLabel) paletteLabel.hidden = state.mode === "monster";
    fillPalette();
    const next = JSON.stringify({
      parts: state.parts,
      selectedPartId: state.selectedPartId,
      selectedSocketId: state.selectedSocketId,
      mode: state.mode,
      dirty: state.dirty,
      blocked: state.blocked,
      recover: state.recover,
      palette: state.palette,
      headTurnPreview: state.headTurnPreview,
      armedGroup: state.armedGroup,
      visibleGroups: state.visibleGroups,
      assignments: state.assignments,
    });
    if (next !== structure) {
      structure = next;
      renderLibrary();
      renderInspector();
    }
    renderStage();
    showBanner();
  }

  function fillPalette() {
    const select = document.getElementById("palette");
    if (select.options.length) return;
    (state.palettes || []).forEach((entry, index) => {
      const option = document.createElement("option");
      option.value = String(entry.index);
      option.textContent = paletteNames[index] || ("颜色 " + (index + 1));
      select.appendChild(option);
    });
  }

  function showBanner() {
    const lines = [];
    if (!formulaOk) lines.push("网页里的摆放公式和游戏测试不一致，先不要用这页的预览做最终判断。");
    if (state && state.blocked) lines.push("磁盘上的装配记录已经变了。");
    ((state && state.pose && state.pose.warnings) || []).forEach((line) => lines.push(line));
    banner.hidden = lines.length === 0;
    banner.textContent = lines.join(" ");
  }

  function renderLibrary() {
    const scroll = library.scrollTop;
    library.replaceChildren();
    ["Primary", "SubPrimary", "Secondary"].forEach((role) => {
      const parts = state.parts.filter((part) => part.role === role && !part.missing);
      if (!parts.length) return;
      const block = document.createElement("section");
      block.className = "folder";
      const title = document.createElement("h2");
      title.textContent = roleLabel[role] || role;
      block.appendChild(title);
      const folders = [];
      parts.forEach((part) => { if (!folders.includes(part.folder)) folders.push(part.folder); });
      folders.forEach((folder) => {
        parts.filter((part) => part.folder === folder).forEach((part) => {
          const button = document.createElement("button");
          button.type = "button";
          button.className = "part" + (part.id === state.selectedPartId ? " on" : "");
          const name = document.createElement("span");
          name.textContent = part.leaf;
          const meta = document.createElement("small");
          meta.textContent = part.folder + " · " + part.width + "×" + part.height;
          button.append(name, meta);
          button.addEventListener("click", () => command("selectPart", { id: part.id }).catch(showError));
          block.appendChild(button);
        });
      });
      library.appendChild(block);
    });
    library.scrollTop = scroll;
  }

  function renderInspector() {
    const scroll = inspector.scrollTop;
    inspector.replaceChildren();
    const part = selectedPart();
    if (!part) {
      inspector.append(paragraph("先在左边选一个部件。"));
      return;
    }
    const title = document.createElement("h3");
    title.textContent = part.folder + " / " + part.leaf;
    inspector.appendChild(title);
    if (state.mode === "monster") {
      inspector.append(paragraph("整只预览不改挂点。按住画面拖动，松手后回到待机。左边点一个部件，就回到它自己的点和分组。"));
      inspector.scrollTop = scroll;
      return;
    }
    inspector.append(paragraph(roleLabel[part.role] + " · 原图 " + part.width + "×" + part.height));
    layerControl(part);
    swingControl(part);
    if (part.kind === "Head") headTurnControl(part);
    if (part.role === "SubPrimary") facingControl(part);
    if (part.role === "Primary") {
      (part.groups || []).forEach((groupId) => inspector.appendChild(groupBlock(part, groupId)));
      exclusionBlock(part);
    } else {
      inspector.append(paragraph("十字是它挂到父部件上的那一个点。次主体和副体都只有这一个。"));
      attachmentControl(part);
    }
    if (state.recover) {
      const recover = document.createElement("button");
      recover.type = "button";
      recover.textContent = "恢复这次编辑并保存";
      recover.addEventListener("click", () => command("recoverTransient").catch(showError));
      const discard = document.createElement("button");
      discard.type = "button";
      discard.textContent = "放弃这次编辑";
      discard.addEventListener("click", () => command("discardTransient").catch(showError));
      const row = document.createElement("div");
      row.className = "row";
      row.append(recover, discard);
      inspector.appendChild(row);
    }
    inspector.scrollTop = scroll;
  }

  function layerControl(part) {
    const label = document.createElement("label");
    label.className = "muted";
    label.textContent = "图层";
    const select = document.createElement("select");
    (state.layerBands || []).forEach((band) => {
      const option = document.createElement("option");
      option.value = band.id;
      option.textContent = band.label;
      if (band.id === part.layerBand) option.selected = true;
      select.appendChild(option);
    });
    select.addEventListener("change", () => {
      command("setLayer", { partId: part.id, band: select.value }).catch(showError);
    });
    inspector.append(label, select);
  }

  function attachmentControl(part) {
    const point = { x: part.attachX, y: part.attachY };
    const send = () => command("moveAttachment", { partId: part.id, x: point.x, y: point.y }).catch(showError);
    inspector.append(
      numberField("挂点 X", point.x, (value) => { point.x = value; send(); }),
      numberField("挂点 Y", point.y, (value) => { point.y = value; send(); }),
    );
  }

  function numberField(labelText, value, onCommit) {
    const label = document.createElement("label");
    label.className = "muted";
    const input = document.createElement("input");
    input.type = "number";
    input.step = "1";
    input.value = String(value);
    input.addEventListener("change", () => onCommit(Math.round(Number(input.value))));
    label.append(document.createTextNode(labelText + " "), input);
    return label;
  }

  function swingControl(part) {
    const label = document.createElement("label");
    label.className = "muted";
    label.textContent = part.swingDegrees === 0 ? "摆动 0°，跟着父体动" : ("独立摆动 " + part.swingDegrees.toFixed(1) + "°");
    const slider = document.createElement("input");
    slider.type = "range";
    slider.min = "0";
    slider.max = String(state.swingMax || 24);
    slider.step = "0.5";
    slider.value = String(part.swingDegrees);
    slider.className = "slider";
    slider.addEventListener("change", () => {
      command("setSwing", { partId: part.id, degrees: Number(slider.value) }).catch(showError);
    });
    inspector.append(label, slider);
  }

  function headTurnControl(part) {
    const label = document.createElement("label");
    label.className = "check";
    const input = document.createElement("input");
    input.type = "checkbox";
    input.checked = !!part.headTurn;
    input.addEventListener("change", () => command("setHeadTurn", { partId: part.id, enabled: input.checked }).catch(showError));
    label.append(input, document.createTextNode("扭头：以身体对接点为轴镜像，角这类硬部件请关掉"));
    inspector.appendChild(label);
  }

  function facingControl(part) {
    const row = document.createElement("div");
    row.className = "row";
    ["Right", "Left"].forEach((facing) => {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = facing === "Right" ? "素材朝右" : "素材朝左";
      button.className = part.nativeFacing === facing ? "on" : "";
      button.addEventListener("click", () => command("setNativeFacing", { partId: part.id, facing }).catch(showError));
      row.appendChild(button);
    });
    inspector.append(paragraph("游戏里会按挂点的左右，自动镜像另一侧。"), row);
  }

  function groupBlock(part, groupId) {
    const spec = groupById(groupId) || { label: groupId, color: "#fff", sided: false, mount: false };
    const box = document.createElement("section");
    box.className = "group";
    const header = document.createElement("header");
    const swatch = document.createElement("i");
    swatch.className = "swatch";
    swatch.style.background = spec.color;
    const name = document.createElement("strong");
    name.textContent = spec.label;
    header.append(swatch, name);
    if (!spec.mount) {
      const check = document.createElement("label");
      check.className = "check";
      const input = document.createElement("input");
      input.type = "checkbox";
      input.checked = (state.visibleGroups || []).includes(groupId);
      input.addEventListener("change", () => command("setGroupVisible", { group: groupId, visible: input.checked }).catch(showError));
      check.append(input, document.createTextNode("临时预览"));
      header.appendChild(check);
    }
    box.appendChild(header);
    const sockets = (part.sockets || []).filter((socket) => socket.group === groupId);
    sockets.forEach((socket) => box.appendChild(socketRow(part, socket, spec)));
    const add = document.createElement("button");
    add.type = "button";
    add.textContent = state.armedGroup === groupId ? "在画布上点一下放下" : "添加挂点";
    add.addEventListener("click", () => command("armGroup", { group: groupId }).catch(showError));
    box.appendChild(add);
    if (sockets.length === 0) box.append(paragraph(spec.mount ? "还没有对接点。" : "这一组还是空的。"));
    return box;
  }

  function socketRow(part, socket, spec) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "socket" + (socket.id === state.selectedSocketId ? " on" : "");
    const label = document.createElement("span");
    const side = spec.sided ? (socket.accepts === "Left" ? "左" : "右") : (spec.mount ? "对接" : "挂点");
    label.textContent = side + "  " + socket.x + "," + socket.y;
    button.appendChild(label);
    button.addEventListener("click", () => command("selectSocket", { socketId: socket.id }).catch(showError));
    const row = document.createElement("div");
    row.className = "row";
    row.appendChild(button);
    if (spec.sided) {
      const flip = document.createElement("button");
      flip.type = "button";
      flip.textContent = socket.accepts === "Left" ? "改成右" : "改成左";
      flip.addEventListener("click", () => {
        command("setFacing", { socketId: socket.id, facing: socket.accepts === "Left" ? "Right" : "Left" }).catch(showError);
      });
      row.appendChild(flip);
    }
    if (spec.mount) {
      const use = document.createElement("button");
      use.type = "button";
      use.textContent = part.mountSocketId === socket.id ? "当前对接" : "用作对接";
      use.addEventListener("click", () => command("setMount", { partId: part.id, socketId: socket.id }).catch(showError));
      row.appendChild(use);
    }
    const remove = document.createElement("button");
    remove.type = "button";
    remove.textContent = "删除";
    remove.addEventListener("click", () => command("removeSocket", { socketId: socket.id }).catch(showError));
    row.appendChild(remove);
    const assigned = (state.assignments || []).find((item) => item.socketId === socket.id);
    if (!spec.mount) {
      const select = document.createElement("select");
      const empty = document.createElement("option");
      empty.value = "";
      empty.textContent = "空";
      select.appendChild(empty);
      const accepts = spec.accepts;
      state.parts.filter((candidate) => candidate.kind === accepts && !candidate.missing && !(part.excluded || []).includes(candidate.id)).forEach((candidate) => {
        const option = document.createElement("option");
        option.value = candidate.id;
        option.textContent = candidate.leaf;
        if (assigned && assigned.partId === candidate.id) option.selected = true;
        select.appendChild(option);
      });
      select.addEventListener("change", () => {
        const name = select.value ? "assign" : "clearAssign";
        command(name, { socketId: socket.id, partId: select.value }).catch(showError);
      });
      row.appendChild(select);
    }
    return row;
  }

  function exclusionBlock(part) {
    const box = document.createElement("section");
    box.className = "group";
    const title = document.createElement("strong");
    title.textContent = "排除池";
    box.append(title, paragraph("排除一只次主体时，左右镜像一起排除。"));
    const chips = document.createElement("div");
    chips.className = "chips";
    (part.excluded || []).forEach((id) => {
      const banned = partById(id);
      const chip = document.createElement("button");
      chip.type = "button";
      chip.className = "chip";
      chip.textContent = (banned ? banned.leaf : id) + " ×";
      chip.addEventListener("click", () => command("setExcluded", { partId: part.id, bannedId: id, excluded: false }).catch(showError));
      chips.appendChild(chip);
    });
    const select = document.createElement("select");
    const empty = document.createElement("option");
    empty.value = "";
    empty.textContent = "添加排除";
    select.appendChild(empty);
    state.parts.filter((candidate) => candidate.id !== part.id && !(part.excluded || []).includes(candidate.id)).forEach((candidate) => {
      const option = document.createElement("option");
      option.value = candidate.id;
      option.textContent = candidate.folder + " / " + candidate.leaf;
      select.appendChild(option);
    });
    select.addEventListener("change", () => {
      if (!select.value) return;
      command("setExcluded", { partId: part.id, bannedId: select.value, excluded: true }).catch(showError);
    });
    box.append(chips, select);
    return box;
  }

  function paragraph(text) {
    const node = document.createElement("p");
    node.className = "muted";
    node.textContent = text;
    return node;
  }

  function renderStage() {
    const part = selectedPart();
    const monster = state.mode === "monster";
    stageTitle.textContent = monster ? "整只预览" : (part ? (part.folder + " / " + part.leaf) : "没有部件");
    const pose = state.pose || { nodes: [], pins: [] };
    const nodes = pose.nodes || [];
    const pins = pose.pins || [];
    const focus = focusNode(part, nodes);
    const bounds = boundsOf(nodes, part);
    view = { minX: bounds.minX, maxY: bounds.maxY, scale: zoom };
    canvas.width = Math.max(1, Math.ceil(bounds.width * zoom));
    canvas.height = Math.max(1, Math.ceil(bounds.height * zoom));
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.imageSmoothingEnabled = false;
    drawChecker(bounds);
    nodes.forEach((node) => drawNode(node, 0, false));
    if (!monster && part && part.role === "SubPrimary" && focus)
      drawNode(focus, part.width + 16, true);
    if (!monster) pins.forEach((pin) => drawPin(pin));
    if (!monster && part && part.role !== "Primary" && focus) drawPeg(focus);
    document.getElementById("roll").hidden = monster;
    document.getElementById("viewport").style.cursor = monster ? "grab" : "crosshair";
    hint.textContent = hintText(part, nodes);
    nodes.forEach((node) => ensureArt(node.partId));
  }

  function focusNode(part, nodes) {
    if (!part) return nodes[0] || null;
    return nodes.find((node) => node.partId === part.id && !node.socketId)
      || nodes.find((node) => node.partId === part.id)
      || null;
  }

  function boundsOf(nodes, part) {
    let minX = 0;
    let minY = 0;
    let maxX = 142;
    let maxY = 102;
    if (part) {
      maxX = part.width;
      maxY = part.height;
    }
    nodes.forEach((node) => {
      corners(node).forEach((point) => {
        minX = Math.min(minX, point.x);
        minY = Math.min(minY, point.y);
        maxX = Math.max(maxX, point.x);
        maxY = Math.max(maxY, point.y);
      });
    });
    if (state.mode === "edit" && part && part.role === "SubPrimary")
      maxX += part.width + 16;
    const pad = 18;
    return { minX: minX - pad, minY: minY - pad, maxX: maxX + pad, maxY: maxY + pad, width: maxX - minX + pad * 2, height: maxY - minY + pad * 2 };
  }

  function corners(node) {
    return [[0, 0], [node.width, 0], [node.width, node.height], [0, node.height]].map(([px, py]) => {
      let x = px - node.attachX;
      let y = py - node.attachY;
      if (node.worldMirror) x = -x;
      const turned = rotate(x, y, node.worldRotation);
      return { x: node.worldAttachX + turned.x, y: node.worldAttachY + turned.y };
    });
  }

  function drawChecker(bounds) {
    const cell = 8 * zoom;
    for (let y = 0; y < canvas.height; y += cell) {
      for (let x = 0; x < canvas.width; x += cell) {
        const dark = ((Math.floor(x / cell) + Math.floor(y / cell)) % 2) === 0;
        ctx.fillStyle = dark ? "#1a1814" : "#24211c";
        ctx.fillRect(x, y, cell, cell);
      }
    }
    ctx.strokeStyle = "rgba(228,195,106,0.35)";
    ctx.strokeRect(0.5, 0.5, canvas.width - 1, canvas.height - 1);
    void bounds;
  }

  function drawNode(node, extraX, forceMirror) {
    const bitmap = bitmaps.get(node.partId);
    if (!bitmap || !bitmap.width) return;
    const mirror = forceMirror ? !node.worldMirror : node.worldMirror;
    const tint = tintFor(node);
    const source = tint ? tintedBitmap(node.partId, bitmap, tint) : bitmap;
    ctx.save();
    ctx.translate(screenX(node.worldAttachX + extraX), screenY(node.worldAttachY));
    ctx.rotate(-node.worldRotation * Math.PI / 180);
    ctx.scale(mirror ? -1 : 1, 1);
    ctx.imageSmoothingEnabled = false;
    const top = node.height - node.attachY;
    ctx.drawImage(source, -node.attachX * zoom, -top * zoom, node.width * zoom, node.height * zoom);
    ctx.restore();
  }

  function tintFor(node) {
    const part = partById(node.partId);
    if (!part || part.color === "None") return "";
    const palettes = state.palettes || [];
    if (state.mode === "monster") {
      const index = node.paletteIndex;
      if (typeof index !== "number" || index < 0) return "";
      const own = palettes[index];
      return own ? own.primary : "";
    }
    const palette = palettes[state.palette] || null;
    return palette ? palette.primary : "";
  }

  function tintedBitmap(id, bitmap, tint) {
    const key = id + tint;
    if (tinted.has(key)) return tinted.get(key);
    const off = document.createElement("canvas");
    off.width = bitmap.width;
    off.height = bitmap.height;
    const paint = off.getContext("2d");
    paint.imageSmoothingEnabled = false;
    paint.drawImage(bitmap, 0, 0);
    paint.globalCompositeOperation = "multiply";
    paint.fillStyle = tint;
    paint.fillRect(0, 0, off.width, off.height);
    paint.globalCompositeOperation = "destination-in";
    paint.drawImage(bitmap, 0, 0);
    tinted.set(key, off);
    return off;
  }

  function drawPin(pin) {
    const x = screenX(pin.worldX);
    const y = screenY(pin.worldY);
    ctx.beginPath();
    if (pin.mount) ctx.rect(x - 5, y - 5, 10, 10);
    else ctx.arc(x, y, 5, 0, Math.PI * 2);
    ctx.fillStyle = pin.color;
    ctx.fill();
    ctx.lineWidth = pin.socketId === state.selectedSocketId ? 2 : 1;
    ctx.strokeStyle = "#f3eee4";
    ctx.stroke();
    if (pin.accepts === "Left" || pin.accepts === "Right") {
      ctx.fillStyle = "#f3eee4";
      ctx.font = "11px Segoe UI";
      ctx.fillText(pin.accepts === "Left" ? "左" : "右", x + 7, y - 6);
    }
  }

  function drawPeg(node) {
    const x = screenX(node.worldAttachX);
    const y = screenY(node.worldAttachY);
    ctx.strokeStyle = "#e4c36a";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(x, y, 8, 0, Math.PI * 2);
    ctx.moveTo(x - 12, y);
    ctx.lineTo(x + 12, y);
    ctx.moveTo(x, y - 12);
    ctx.lineTo(x, y + 12);
    ctx.stroke();
    ctx.fillStyle = "#e4c36a";
    ctx.font = "12px Segoe UI";
    ctx.fillText("挂点", x + 14, y - 10);
  }

  function screenX(worldX) { return (worldX - view.minX) * view.scale; }
  function screenY(worldY) { return (view.maxY - worldY) * view.scale; }

  function hintText(part) {
    if (!part) return "";
    if (state.mode === "monster") return "按住画面拖动，看头、脚、尾巴和身体跟着摆。松手后回到待机。这次随机不会写回组件挂点。";
    if (part.role === "Primary" && state.armedGroup) return "在画布上点一下，给当前挂点组放一个点。拖动可以改位置。";
    if (part.role === "Primary") return "先选一组挂点，再在画布上点出来。勾选临时预览后，随机只在这次查看里出现。";
    if (part.role === "SubPrimary") return "左边是素材，右边是自动镜像。拖十字或改数字，对准它挂上父部件的那一个像素。";
    return "拖十字或改数字，对准它挂上父部件的那一个像素。副体不再往下挂东西。";
  }

  function ensureArt(id) {
    if (!id || bitmaps.has(id)) return;
    bitmaps.set(id, "loading");
    fetch("/api/art?id=" + encodeURIComponent(id), { headers: { Authorization: "Bearer " + token } })
      .then((response) => response.blob())
      .then((blob) => createImageBitmap(blob))
      .then((bitmap) => {
        bitmaps.set(id, bitmap);
        renderStage();
      })
      .catch(() => bitmaps.delete(id));
  }

  function eventPoint(ev) {
    const rect = canvas.getBoundingClientRect();
    const sx = (ev.clientX - rect.left) * (canvas.width / rect.width);
    const sy = (ev.clientY - rect.top) * (canvas.height / rect.height);
    return {
      sx,
      sy,
      wx: sx / view.scale + view.minX,
      wy: view.maxY - sy / view.scale,
    };
  }

  function spritePoint(point, node) {
    let x = point.wx - node.worldAttachX;
    let y = point.wy - node.worldAttachY;
    const back = rotate(x, y, -node.worldRotation);
    x = back.x;
    y = back.y;
    if (node.worldMirror) x = -x;
    return { x: Math.round(x + node.attachX), y: Math.round(y + node.attachY) };
  }

  function hitPin(point) {
    const pins = (state.pose && state.pose.pins) || [];
    let found = null;
    let best = 12 * 12;
    pins.forEach((pin) => {
      const dx = screenX(pin.worldX) - point.sx;
      const dy = screenY(pin.worldY) - point.sy;
      const distance = dx * dx + dy * dy;
      if (distance < best) {
        best = distance;
        found = pin;
      }
    });
    return found;
  }

  canvas.addEventListener("pointerdown", (ev) => {
    if (!state) return;
    const point = eventPoint(ev);
    if (state.mode === "monster") {
      drag = { kind: "motion", x: point.sx, y: point.sy, t: performance.now() };
      canvas.setPointerCapture(ev.pointerId);
      command("setDrag", { active: true, velocityX: 0, time: (drag.t - playStarted) / 1000 }).catch(showError);
      return;
    }
    const pin = hitPin(point);
    const part = selectedPart();
    const node = ((state.pose && state.pose.nodes) || []).find((item) => part && item.partId === part.id);
    if (pin) {
      drag = { kind: "socket", id: pin.socketId, partId: pin.partId };
      command("selectSocket", { socketId: pin.socketId }).catch(showError);
      canvas.setPointerCapture(ev.pointerId);
      return;
    }
    if (part && part.role !== "Primary" && node) {
      const dx = screenX(node.worldAttachX) - point.sx;
      const dy = screenY(node.worldAttachY) - point.sy;
      if (dx * dx + dy * dy < 18 * 18) {
        drag = { kind: "peg" };
        canvas.setPointerCapture(ev.pointerId);
        return;
      }
    }
    if (part && part.role === "Primary" && state.armedGroup && node) {
      const local = spritePoint(point, node);
      command("addSocket", { group: state.armedGroup, x: local.x, y: local.y }).catch(showError);
    }
  });

  canvas.addEventListener("pointermove", (ev) => {
    if (!drag || !state) return;
    const point = eventPoint(ev);
    if (drag.kind === "motion") {
      const now = performance.now();
      const dt = Math.max(16, now - drag.t);
      drag.velocityX = (point.sx - drag.x) / dt * 1000;
      drag.x = point.sx;
      drag.t = now;
      scheduleMove("setDrag", { active: true, velocityX: drag.velocityX, time: (now - playStarted) / 1000 });
      return;
    }
    const part = selectedPart();
    const node = ((state.pose && state.pose.nodes) || []).find((item) => part && item.partId === (drag.partId || part.id));
    if (!node) return;
    const local = spritePoint(point, node);
    if (drag.kind === "socket") scheduleMove("moveSocket", { socketId: drag.id, x: local.x, y: local.y });
    if (drag.kind === "peg") scheduleMove("moveAttachment", { x: local.x, y: local.y });
  });

  canvas.addEventListener("pointerup", () => {
    if (drag && drag.kind === "motion") {
      command("setDrag", { active: false, velocityX: 0, time: (performance.now() - playStarted) / 1000 }).catch(showError);
    }
    drag = null;
  });

  document.getElementById("modes").addEventListener("click", (ev) => {
    const button = ev.target.closest("button");
    if (!button) return;
    command("setMode", { mode: button.dataset.mode }).catch(showError);
  });
  document.getElementById("zoom").addEventListener("click", (ev) => {
    const button = ev.target.closest("button");
    if (!button) return;
    zoom = Number(button.dataset.zoom);
    render();
  });
  document.getElementById("turn").addEventListener("click", () => {
    command("setHeadTurnPreview", { shown: !(state && state.headTurnPreview) }).catch(showError);
  });
  document.getElementById("roll").addEventListener("click", () => command("randomize").catch(showError));
  document.getElementById("rollAll").addEventListener("click", () => command("randomizeMonster").catch(showError));
  document.getElementById("save").addEventListener("click", () => command("save").catch(showError));
  document.getElementById("revert").addEventListener("click", () => command("revert").catch(showError));
  document.getElementById("palette").addEventListener("change", (ev) => {
    command("setPalette", { index: Number(ev.target.value) }).catch(showError);
  });

  window.addEventListener("keydown", (ev) => {
    if (!state || !state.selectedSocketId) return;
    if (ev.target.matches("input, select, textarea")) return;
    const step = ev.shiftKey ? 5 : 1;
    const socket = findSocket(state.selectedSocketId);
    if (!socket) return;
    let x = socket.x;
    let y = socket.y;
    if (ev.key === "ArrowLeft") x -= step;
    else if (ev.key === "ArrowRight") x += step;
    else if (ev.key === "ArrowUp") y += step;
    else if (ev.key === "ArrowDown") y -= step;
    else if (ev.key === "Delete" || ev.key === "Backspace") {
      command("removeSocket", { socketId: socket.id }).catch(showError);
      return;
    } else if ((ev.key === "s" || ev.key === "S") && (ev.ctrlKey || ev.metaKey)) {
      ev.preventDefault();
      command("save").catch(showError);
      return;
    } else return;
    ev.preventDefault();
    command("moveSocket", { socketId: socket.id, x, y }).catch(showError);
  });

  function findSocket(id) {
    for (const part of state.parts || []) {
      for (const socket of part.sockets || []) {
        if (socket.id === id) return socket;
      }
    }
    return null;
  }

  let scheduled = null;
  let scheduleRunning = false;
  function scheduleMove(name, payload) {
    scheduled = { name, payload };
    if (scheduleRunning) return;
    scheduleRunning = true;
    (async () => {
      while (scheduled) {
        const next = scheduled;
        scheduled = null;
        try { await command(next.name, next.payload); } catch (error) { showError(error); }
      }
      scheduleRunning = false;
    })();
  }

  function showError(error) {
    banner.hidden = false;
    banner.textContent = String(error.message || error);
  }

  let lastPreviewSent = 0;
  function tick() {
    const now = performance.now();
    const monster = state && state.mode === "monster";
    const posing = drag && drag.kind === "motion";
    if (playing && state && (monster || hasSwing(state)) && now - lastPreviewSent > 140 && !(drag && !posing)) {
      lastPreviewSent = now;
      const time = (now - playStarted) / 1000;
      const sent = posing
        ? command("setDrag", { active: true, velocityX: drag.velocityX || 0, time })
        : command("setPreviewTime", { time });
      sent.catch(() => {});
    }
    requestAnimationFrame(tick);
  }

  function hasSwing(current) {
    return (current.parts || []).some((part) => part.swingDegrees > 0);
  }

  async function connect() {
    if (!token) {
      connection.textContent = "缺少口令，请从 Unity 菜单打开";
      return;
    }
    if (!formulaOk) showBanner();
    const snapshot = await api("/api/snapshot");
    applyEnvelope(snapshot);
    requestAnimationFrame(tick);
    longPoll();
  }

  async function longPoll() {
    for (;;) {
      try {
        const envelope = await api("/api/events?afterRevision=" + revision + "&timeoutMs=25000");
        if (envelope) applyEnvelope(envelope);
      } catch (error) {
        await new Promise((resolve) => setTimeout(resolve, 1000));
      }
    }
  }

  connect().catch(showError);
})();
