// 加基础怪物：在 Assets/StreamingAssets/call-book.json 增加一条技能。
// 改句子：Assets/Scripts/Core/Rules/SkillSentences.cs。要跟着加成变的数写成 Energy 片段。
// 数值和取得许可由同源 C# 规则计算。JS 只编辑输入和呈现结果。

import { dotnet } from "./_framework/dotnet.js";
import { spanParts } from "./board.js";
import { monsterSvg } from "./glyphs.js";

const SKILL_SLOTS = 3;
const SLOP = 8;

const USE = { Active: "主动", Passive: "被动" };
const RARITY = { White: "白", Blue: "蓝", Gold: "金" };
const AFFIX = { Destroy: "消灭", Permanent: "永久", Capacity: "产能", Immovable: "不动" };

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.TheCall.PageExports;

const status = document.querySelector("#status");
const levelSelect = document.querySelector("#level");
const report = document.querySelector("#report");

let byName = new Map();
const monsters = new Map();
let nextId = 1;
let bench = [];
const skillSlots = [null, null, null];
const chipStates = [null, null, null];
const toolsOn = new Set();
let armed = null;
let press = null;
let drag = null;

const book = await fetch(new URL("./call-book.json", import.meta.url)).then((response) => {
  if (!response.ok)
    throw new Error("内容文件没有加载。");

  return response.text();
});

const booted = JSON.parse(api.Boot(book));
if (booted.error) {
  status.textContent = booted.error;
  throw new Error(booted.error);
}

const catalog = JSON.parse(api.Catalog());
if (catalog.error) {
  status.textContent = catalog.error;
  throw new Error(catalog.error);
}

for (const skill of catalog.skills)


byName = new Map(catalog.skills.map((skill) => [skill.name, skill]));
status.textContent = "单击拿起再放下，或直接拖。格子上的怪物按出现顺序编号。怪物回库即撤下，技能上卡即装上，技能回库即卸下。";
renderLevels();
renderTools();
fitBench(readCells());
renderBoard();
renderLedger(null, null);

document.querySelector("#settle").addEventListener("click", settle);
window.addEventListener("pointerdown", onDown);
window.addEventListener("pointermove", onMove);
window.addEventListener("pointerup", onUp);
window.addEventListener("pointercancel", cancelGesture);

function renderLevels() {
  levelSelect.replaceChildren();
  for (const level of catalog.levels) {
    const option = document.createElement("option");
    option.value = String(level.level);
    option.textContent = "第 " + level.level + " 关 · 应交 " + level.due;
    levelSelect.append(option);
  }
}

function renderTools() {
  const row = document.querySelector("#tools");
  row.replaceChildren();
  for (const tool of catalog.tools) {
    const button = el("button", "tool", tool.name);
    button.type = "button";
    button.addEventListener("click", () => {
      if (toolsOn.has(tool.name))
        toolsOn.delete(tool.name);
      else
        toolsOn.add(tool.name);

      button.classList.toggle("on", toolsOn.has(tool.name));
      fitBench(readCells());
      renderBoard();
    });
    row.append(button);
  }
}

function readCells() {
  const counted = JSON.parse(api.Cells(JSON.stringify([...toolsOn])));
  if (counted.error) {
    showReport(counted.error, false);
    return bench.length || 5;
  }

  return counted.cells;
}

function fitBench(count) {
  for (let index = count; index < bench.length; index += 1) {
    if (bench[index])
      monsters.delete(bench[index]);
  }

  const next = bench.slice(0, count);
  while (next.length < count)
    next.push(null);

  bench = next;
}

function renderBoard() {
  renderBench();
  renderLibrary();
  renderSlots();
  renderSkillLibrary();
  paintArmed();
}

function pressable(node, label) {
  node.setAttribute("role", "button");
  node.tabIndex = 0;
  node.setAttribute("aria-label", label);
}

function renderBench() {
  const cells = document.querySelector("#cells");
  const preview = JSON.parse(api.Preview(JSON.stringify(boardInput())));
  const details = Array.isArray(preview) ? preview : [];
  if (preview.error) showReport(preview.error, false);
  cells.replaceChildren();
  bench.forEach((id, index) => {
    const slot = el("div", "slot");
    slot.dataset.zone = "bench";
    slot.dataset.index = String(index);
    pressable(slot, "格子 " + (index + 1));
    slot.append(el("span", "slot-index", String(index + 1)));
    if (id)
      slot.append(buildCard(monsters.get(id), details[index], "monster"));
    else
      slot.append(el("span", "empty-mark", "?"));

    cells.append(slot);
  });
}

function renderLibrary() {
  const grid = document.querySelector("#monster-grid");
  grid.replaceChildren();
  for (const skill of catalog.skills)
    grid.append(buildCard({ skills: [skill.name] }, null, "template"));
}

function renderSlots() {
  const row = document.querySelector("#slot-row");
  row.replaceChildren();
  for (let index = 0; index < SKILL_SLOTS; index += 1) {
    const name = skillSlots[index];
    const chip = name ? skillChip(name) : el("div", "chip empty", "空");
    chip.dataset.zone = "skill-slot";
    chip.dataset.index = String(index);
    pressable(chip, name || "空技能 " + (index + 1));
    if (name)
      chip.dataset.payload = "skill-chip";

    row.append(chip);
  }
}

function renderSkillLibrary() {
  const grid = document.querySelector("#skill-grid");
  grid.replaceChildren();
  for (const skill of catalog.skills) {
    const chip = skillChip(skill.name);
    chip.dataset.payload = "skill-copy";
    grid.append(chip);
  }
}

function buildCard(monster, adjust, payload) {
  const first = byName.get(monster.skills[0]);
  const card = el("article", "card");
  card.dataset.rarity = first.rarity;
  card.dataset.payload = payload;
  if (payload === "template")
    card.dataset.skill = first.name;
  else
    card.dataset.id = monster.id;

  const named = payload === "monster";
  const head = el("div", "head");
  const glyph = el("div", "glyph");
  glyph.innerHTML = monsterSvg(first.name, first.rarity);
  const who = el("div", "who");
  if (named) {
    who.append(el("span", "kicker", "怪物"));
    who.append(el("strong", "", monsterTitle(monster)));
  } else {
    who.append(el("span", "kicker", "技能"));
    who.append(el("strong", rarityClass(first.rarity), first.name));
    who.append(el("span", "meta", metaLine(first)));
  }
  head.append(glyph, who);
  if (named)
    pressable(head, monsterTitle(monster));
  else
    pressable(card, first.name);

  const lines = el("div", "lines");
  lines.dataset.count = String(monster.skills.length);
  monster.skills.forEach((name, index) => {
    const skill = byName.get(name);
    const line = el("p", "line");
    if (named) {
      line.dataset.payload = "worn";
      line.dataset.id = monster.id;
      line.dataset.index = String(index);
      pressable(line, skill.name);
    }

    if (named || monster.skills.length > 1) {
      line.append(el("b", rarityClass(skill.rarity), skill.name));
      line.append(document.createTextNode(" "));
      line.append(el("span", "meta", metaLine(skill)));
      line.append(document.createTextNode(" "));
    }

    const words = el("span", "words");
    for (const part of (adjust?.Skills?.[index]?.Spans?.map(span => ({ text: span.Text, live: span.Shifted })) || spanParts(skill.spans, null)))
      words.append(el("span", part.live ? "live" : "", part.text));

    line.append(words);
    if (named) {
      monster.growth ||= [];
      monster.temporary ||= [];
      const controls = el("span", "instance-controls");
      const growth = document.createElement("input"); growth.type = "number"; growth.value = monster.growth[index] || 0;
      growth.setAttribute("aria-label", monsterTitle(monster) + "技能" + (index + 1) + "成长");
      growth.addEventListener("change", () => { monster.growth[index] = Number(growth.value); renderBoard(); });
      const temporary = document.createElement("input"); temporary.type = "checkbox"; temporary.checked = monster.temporary[index] || false;
      temporary.setAttribute("aria-label", monsterTitle(monster) + "技能" + (index + 1) + "当天临时");
      temporary.addEventListener("change", () => { monster.temporary[index] = temporary.checked; renderBoard(); });
      controls.append(el("small", "", "实例 " + (index + 1) + " · 成长"), growth, el("small", "", "当天临时"), temporary);
      line.append(controls);
    }
    lines.append(line);
  });
  card.append(head);
  if (named) {
    const attributes = el("div", "attributes");
    for (const [key, title, entries, fallback] of [
      ["personality", "性格", catalog.personalities, "稳定"],
      ["race", "种族", catalog.races, "森林"],
      ["mood", "心情", catalog.moods, "普通"],
    ]) {
      const label = el("label", "", title);
      const select = document.createElement("select");
      select.setAttribute("aria-label", monsterTitle(monster) + title);
      for (const entry of entries) {
        const option = new Option(entry.Name, entry.Name);
        option.title = entry.Description || entry.Element || "";
        select.append(option);
      }
      select.value = monster[key] || fallback;
      select.addEventListener("change", () => {
        const old = monster[key];
        monster[key] = select.value;
        const checked = JSON.parse(api.Preview(JSON.stringify(boardInput())));
        if (checked.error) { monster[key] = old; showReport(checked.error, false); }
        renderBoard();
      });
      label.append(select);
      attributes.append(label);
    }
    const personality = catalog.personalities.find(item => item.Name === (monster.personality || "稳定"));
    attributes.append(el("small", "attribute-note", personality?.Description || ""));
    const advanced = el("details", "advanced"); advanced.append(el("summary", "", "工作历史与亲缘"));
    for (const [key, title] of [["modifier", "当天修正"], ["workDays", "连续工作"], ["restDays", "连续休息"]]) {
      const label = el("label", "", title); const input = document.createElement("input"); input.type = "number";
      input.value = monster[key] || 0; input.setAttribute("aria-label", monsterTitle(monster) + title);
      input.addEventListener("change", () => { monster[key] = Number(input.value); renderBoard(); }); label.append(input); advanced.append(label);
    }
    const parentLabel = el("label", "", "亲本所在格（从1起，逗号分隔）"); const parentInput = document.createElement("input");
    parentInput.value = (monster.parentCells || []).map(i => i + 1).join(","); parentInput.setAttribute("aria-label", monsterTitle(monster) + "亲本格");
    parentInput.addEventListener("change", () => { monster.parentCells = parentInput.value.trim() ? parentInput.value.split(",").map(i => Number(i.trim()) - 1) : []; renderBoard(); });
    parentLabel.append(parentInput); advanced.append(parentLabel); attributes.append(advanced); card.append(attributes);
  }
  card.append(lines);
  return card;
}

function skillChip(name) {
  const skill = byName.get(name);
  const chip = el("div", "chip");
  chip.dataset.rarity = skill.rarity;
  chip.dataset.skill = name;
  chip.append(el("b", rarityClass(skill.rarity), name));
  chip.append(el("small", "", metaLine(skill)));
  pressable(chip, name);
  return chip;
}

function metaLine(skill) {
  return [USE[skill.use] || skill.use, RARITY[skill.rarity] || skill.rarity]
    .concat(skill.affix.map((flag) => AFFIX[flag] || flag))
    .join(" · ");
}

function rarityClass(rarity) {
  if (rarity === "Blue")
    return "rare-blue";
  if (rarity === "Gold")
    return "rare-gold";

  return "";
}

function skillsOf(id) {
  return monsters.get(id).skills;
}


function onDown(event) {
  if (event.button !== 0 || event.target.closest("button, select, input, label"))
    return;

  const payload = readPayload(event.target);
  if (!payload && !event.target.closest("[data-zone], #library, #skill-library, #skill-slots"))
    return;

  press = { payload, x: event.clientX, y: event.clientY, pointerId: event.pointerId };
}

function onMove(event) {
  if (!press || event.pointerId !== press.pointerId || !press.payload)
    return;

  const dx = event.clientX - press.x;
  const dy = event.clientY - press.y;
  if (!drag) {
    if (dx * dx + dy * dy < SLOP * SLOP)
      return;

    drag = { payload: press.payload, ghost: null };
    armed = null;
    paintArmed();
    showGhost(event);
  }

  moveGhost(event);
  markHot(document.elementFromPoint(event.clientX, event.clientY));
}

function onUp(event) {
  if (!press || event.pointerId !== press.pointerId)
    return;

  const under = document.elementFromPoint(event.clientX, event.clientY);
  if (drag) {
    const rejected = commit(drag.payload, zoneAt(under, drag.payload));
    clearGesture();
    renderBoard();
    if (rejected)
      flash(rejected);

    return;
  }

  if (armed) {
    if (press.payload && payloadKey(armed) === payloadKey(press.payload))
      armed = null;
    else {
      const rejected = commit(armed, zoneAt(under, armed));
      armed = null;
      clearGesture();
      renderBoard();
      if (rejected)
        flash(rejected);

      return;
    }
  } else if (press.payload) {
    armed = press.payload;
  }

  clearGesture();
  paintArmed();
}

function cancelGesture() {
  clearGesture();
  paintArmed();
}

function commit(payload, zone) {
  if (!zone)
    return null;

  if (payload.type === "template" && zone.type === "bench")
    return placeTemplate(payload.skill, zone.index);
  if (payload.type === "monster" && zone.type === "bench")
    return moveMonster(payload.id, zone.index);
  if (payload.type === "monster" && zone.type === "library") {
    removeMonster(payload.id);
    return null;
  }

  if (payload.type === "skill-copy" && zone.type === "monster")
    return equipOnto(zone.id, payload.skill) ? null : monsterFlash(zone.id);
  if (payload.type === "skill-copy" && (zone.type === "skill-slot" || zone.type === "skill-panel"))
    return fillSlot(zone.type === "skill-slot" ? zone.index : firstEmptySlot(), payload.skill);
  if (payload.type === "skill-chip" && zone.type === "monster")
    return equipChip(payload.index, zone.id);
  if (payload.type === "skill-chip" && zone.type === "skill-slot")
    return moveChip(payload.index, zone.index);
  if (payload.type === "skill-chip" && zone.type === "skill-panel")
    return moveChip(payload.index, firstEmptySlot());
  if (payload.type === "skill-chip" && zone.type === "skill-library") {
    skillSlots[payload.index] = null;
    return null;
  }

  if (payload.type === "worn" && zone.type === "monster")
    return moveWorn(payload, zone.id);
  if (payload.type === "worn" && (zone.type === "skill-slot" || zone.type === "skill-panel"))
    return parkWorn(payload, zone.type === "skill-slot" ? zone.index : firstEmptySlot());
  if (payload.type === "worn" && zone.type === "skill-library")
    return dropWorn(payload);

  return null;
}

function placeTemplate(skill, index) {
  if (bench[index])
    return slotFlash(index);

  bench[index] = createMonster(skill);
  return null;
}

function moveMonster(id, index) {
  const from = bench.indexOf(id);
  if (from < 0 || from === index)
    return null;

  const occupant = bench[index];
  bench[index] = id;
  bench[from] = occupant;
  return null;
}

function removeMonster(id) {
  const index = bench.indexOf(id);
  if (index >= 0)
    bench[index] = null;

  monsters.delete(id);
}

function createMonster(skill) {
  const number = nextId;
  nextId += 1;
  const id = String(number);
  monsters.set(id, { id, number, skills: [skill], personality: "稳定", race: "森林", mood: "普通" });
  return id;
}

function monsterTitle(monster) {
  return monster.number + "号怪物";
}

function equipOnto(id, skill) {
  const monster = monsters.get(id);
  if (!monster) return false;
  monster.skills.push(skill);
  (monster.growth ||= []).push(0); (monster.temporary ||= []).push(false);
  const checked = JSON.parse(api.Preview(JSON.stringify(boardInput())));
  if (checked.error) {
    monster.skills.pop(); monster.growth.pop(); monster.temporary.pop();
    showReport(checked.error, false);
    return false;
  }
  return true;
}

function equipChip(index, monsterId) {
  const skill = skillSlots[index];
  if (!skill || !equipOnto(monsterId, skill))
    return monsterFlash(monsterId);

  const monster = monsters.get(monsterId); const last = monster.skills.length - 1;
  monster.growth[last] = chipStates[index]?.growth || 0; monster.temporary[last] = chipStates[index]?.temporary || false;
  skillSlots[index] = null; chipStates[index] = null;
  return null;
}

function fillSlot(index, skill) {
  if (index < 0 || skillSlots[index])
    return "#skill-slots";

  skillSlots[index] = skill;
  return null;
}

function moveChip(from, to) {
  if (to < 0)
    return "#skill-slots";
  if (from === to)
    return null;

  const occupant = skillSlots[to]; const state = chipStates[to];
  skillSlots[to] = skillSlots[from]; chipStates[to] = chipStates[from];
  skillSlots[from] = occupant; chipStates[from] = state;
  return null;
}

function firstEmptySlot() {
  return skillSlots.indexOf(null);
}

function moveWorn(payload, targetId) {
  if (payload.id === targetId)
    return null;

  const source = monsters.get(payload.id);
  const skill = source && source.skills[payload.index];
  if (!source || source.skills.length <= 1)
    return monsterFlash(payload.id);
  if (!equipOnto(targetId, skill))
    return monsterFlash(targetId);
  const target = monsters.get(targetId); const last = target.skills.length - 1;
  target.growth[last] = source.growth?.[payload.index] || 0; target.temporary[last] = source.temporary?.[payload.index] || false;

  source.skills.splice(payload.index, 1);
  source.growth?.splice(payload.index, 1); source.temporary?.splice(payload.index, 1);
  return null;
}

function parkWorn(payload, slotIndex) {
  const source = monsters.get(payload.id);
  if (!source || source.skills.length <= 1)
    return monsterFlash(payload.id);
  if (slotIndex < 0 || skillSlots[slotIndex])
    return "#skill-slots";

  chipStates[slotIndex] = {growth:source.growth?.[payload.index] || 0,temporary:source.temporary?.[payload.index] || false};
  skillSlots[slotIndex] = source.skills.splice(payload.index, 1)[0];
  source.growth?.splice(payload.index, 1); source.temporary?.splice(payload.index, 1);
  return null;
}

function dropWorn(payload) {
  const source = monsters.get(payload.id);
  if (!source || source.skills.length <= 1)
    return monsterFlash(payload.id);

  source.skills.splice(payload.index, 1);
  source.growth?.splice(payload.index, 1); source.temporary?.splice(payload.index, 1);
  return null;
}

function zoneAt(node, payload) {
  if (!node || !payload)
    return null;

  if (isSkill(payload)) {
    const monster = node.closest("[data-payload='monster']");
    if (monster)
      return { type: "monster", id: monster.dataset.id };

    const slot = node.closest("[data-zone='skill-slot']");
    if (slot)
      return { type: "skill-slot", index: Number(slot.dataset.index) };
    if (node.closest("#skill-slots"))
      return { type: "skill-panel" };
    if (node.closest("#skill-library"))
      return { type: "skill-library" };

    return null;
  }

  const benchSlot = node.closest("[data-zone='bench']");
  if (benchSlot)
    return { type: "bench", index: Number(benchSlot.dataset.index) };
  if (node.closest("#library"))
    return { type: "library" };

  return null;
}

function isSkill(payload) {
  return payload.type === "skill-copy" || payload.type === "skill-chip" || payload.type === "worn";
}

function readPayload(node) {
  const host = node && node.closest ? node.closest("[data-payload]") : null;
  if (!host)
    return null;

  const type = host.dataset.payload;
  if (type === "template" || type === "skill-copy")
    return { type, skill: host.dataset.skill };
  if (type === "monster")
    return { type, id: host.dataset.id };
  if (type === "skill-chip")
    return { type, index: Number(host.dataset.index) };
  if (type === "worn")
    return { type, id: host.dataset.id, index: Number(host.dataset.index) };

  return null;
}

function payloadKey(payload) {
  if (payload.type === "template" || payload.type === "skill-copy")
    return payload.type + ":" + payload.skill;
  if (payload.type === "monster")
    return "monster:" + payload.id;
  if (payload.type === "skill-chip")
    return "skill-chip:" + payload.index;

  return "worn:" + payload.id + ":" + payload.index;
}

function paintArmed() {
  for (const node of document.querySelectorAll(".armed"))
    node.classList.remove("armed");

  if (!armed)
    return;

  const key = payloadKey(armed);
  for (const node of document.querySelectorAll("[data-payload]")) {
    const payload = readPayload(node);
    if (payload && payloadKey(payload) === key) {
      node.classList.add("armed");
      return;
    }
  }
}

function markHot(node) {
  for (const item of document.querySelectorAll(".hot"))
    item.classList.remove("hot");

  const host = zoneHost(zoneAt(node, drag.payload));
  if (host)
    host.classList.add("hot");
}

function zoneHost(zone) {
  if (!zone)
    return null;
  if (zone.type === "bench")
    return document.querySelector("[data-zone='bench'][data-index='" + zone.index + "']");
  if (zone.type === "skill-slot")
    return document.querySelector("[data-zone='skill-slot'][data-index='" + zone.index + "']");
  if (zone.type === "monster")
    return document.querySelector("[data-payload='monster'][data-id='" + zone.id + "']");
  if (zone.type === "library")
    return document.querySelector("#library");
  if (zone.type === "skill-library")
    return document.querySelector("#skill-library");
  if (zone.type === "skill-panel")
    return document.querySelector("#skill-slots");

  return null;
}

function showGhost(event) {
  const ghost = el("div", "ghost");
  ghost.append(el("b", "", ghostLabel(drag.payload)));
  document.body.append(ghost);
  drag.ghost = ghost;
  moveGhost(event);
}

function ghostLabel(payload) {
  if (payload.type === "template" || payload.type === "skill-copy")
    return payload.skill;
  if (payload.type === "monster")
    return monsterTitle(monsters.get(payload.id));
  if (payload.type === "skill-chip")
    return skillSlots[payload.index];

  return monsters.get(payload.id).skills[payload.index];
}

function moveGhost(event) {
  drag.ghost.style.left = event.clientX + 14 + "px";
  drag.ghost.style.top = event.clientY + 14 + "px";
}

function clearGesture() {
  if (drag && drag.ghost)
    drag.ghost.remove();

  drag = null;
  press = null;
  for (const node of document.querySelectorAll(".hot"))
    node.classList.remove("hot");
}

function flash(selector) {
  const node = document.querySelector(selector);
  if (!node)
    return;

  node.classList.add("reject");
}

function slotFlash(index) {
  return "[data-zone='bench'][data-index='" + index + "']";
}

function monsterFlash(id) {
  return "[data-payload='monster'][data-id='" + id + "']";
}

function boardInput() {
  return {
    level: Number(levelSelect.value),
    seed: Number(document.querySelector("#seed").value),
    initialEnergy: Number(document.querySelector("#initial-energy").value),
    tools: [...toolsOn],
    row: bench.map(id => id ? {
      skills: monsters.get(id).skills.map((name, index) => ({ name, growth: monsters.get(id).growth?.[index] || 0, temporary: monsters.get(id).temporary?.[index] || false })),
      modifier: monsters.get(id).modifier || 0,
      workDays: monsters.get(id).workDays || 0,
      restDays: monsters.get(id).restDays || 0,
      parentCells: monsters.get(id).parentCells || [],
      personality: monsters.get(id).personality,
      race: monsters.get(id).race,
      mood: monsters.get(id).mood,
    } : null),
  };
}

function settle() {
  const result = JSON.parse(api.Score(JSON.stringify(boardInput())));
  if (result.error) {
    showReport(result.error, false);
    renderLedger(null, result.error);
    return;
  }

  showReport(summaryText(result), result.meetsDue);
  renderLedger(result, null);
}

function summaryText(result) {
  return "带入 " + result.initialEnergy + " · 本轮 " + result.produced + " · 累计 " + result.accumulatedEnergy + " · 应交 " + result.due + " · " + (result.meetsDue ? "达到应交" : "未达应交");
}

function renderLedger(result, error) {
  renderSummary(result);
  const body = document.querySelector("#ledger-body");
  body.replaceChildren();
  if (error) {
    body.append(el("p", "miss", error));
    return;
  }

  if (!result) {
    body.append(el("p", "ledger-empty", "还没有结算。怪物放上格子后点结算，时间线会列在这里。"));
    return;
  }

  const steps = timelineOf(result);
  if (steps.length === 0) {
    body.append(el("p", "ledger-empty", "这次没有任何记录。"));
    return;
  }

  const labels = engineLabels(result.placed);
  let running = result.initialEnergy || 0;
  steps.forEach((step, index) => {
    const order = index + 1;
    if (step.kind === "landing") {
      running += step.energy;
      body.append(renderLanding(step, order, labels, running));
      return;
    }

    if (step.kind === "swap")
      body.append(renderSwap(step, order, labels));
    else if (step.kind === "removal")
      body.append(renderRemoval(step, order, labels));
    else if (step.kind === "payment")
      body.append(renderPayment(step, order, result));
  });
}

function renderSummary(result) {
  const host = document.querySelector("#ledger-summary");
  const rows = result
    ? [
      ["本次产出", String(result.produced), result.meetsDue ? "hit" : "miss"],
      ["本关应交", String(result.due), ""],
      ["交款", result.meetsDue ? "达到应交" : "未达应交", result.meetsDue ? "hit" : "miss"],
      ["超额线", String(result.excessAt), ""],
      ["超额", result.meetsExcess ? "达到超额" : "未达超额", result.meetsExcess ? "hit" : "miss"],
    ]
    : [
      ["本次产出", "—", ""],
      ["本关应交", "—", ""],
      ["交款", "尚未结算", ""],
      ["超额线", "—", ""],
      ["超额", "尚未结算", ""],
    ];
  host.replaceChildren();
  for (const [label, value, tone] of rows) {
    const stat = el("p", "stat");
    stat.append(el("span", "", label));
    stat.append(el("strong", tone, value));
    host.append(stat);
  }
}

function timelineOf(result) {
  if (Array.isArray(result.steps))
    return result.steps;

  const steps = [];
  for (const landing of result.landings)
    steps.push(Object.assign({ kind: "landing" }, landing));
  for (const swap of result.swaps)
    steps.push(Object.assign({ kind: "swap" }, swap));
  for (const removal of result.removals)
    steps.push(Object.assign({ kind: "removal" }, removal));

  return steps;
}

function renderLanding(step, order, labels, running) {
  const beat = el("article", "beat");
  beat.append(beatHead(order, placedName(labels, step.monsterId), step.skillName));
  const math = el("dl", "math");
  addMath(math, "报价", quoteText(step));
  addMath(math, "加项", addsText(step));
  addMath(math, "底数", baseText(step));
  addMath(math, "倍率", factorText(step));
  addMath(math, "能量", step.base + " × " + step.multiplier + " = " + step.energy, "sum");
  addMath(math, "写回", writebackText(step));
  addMath(math, "累计", "到这一步，产出合计 " + running);
  beat.append(math);
  return beat;
}

function renderSwap(step, order, labels) {
  const actor = placedName(labels, step.actorId);
  const target = placedName(labels, step.targetId);
  const text = step.happened
    ? actor + " 和 " + target + " 交换了位置。"
    : actor + " 想和 " + target + " 交换位置，这次没有换成。";
  return plainBeat(order, "换位", text);
}

function renderRemoval(step, order, labels) {
  let text;
  if (step.happened)
    text = placedName(labels, step.monsterId) + " 离开了这一排。已经落地的能量还在。";
  else if (!step.monsterId && step.sourceId)
    text = placedName(labels, step.sourceId) + " 想消灭相邻的怪物，旁边没有可以消灭的。";
  else if (!step.monsterId)
    text = "这次消灭没有找到目标。";
  else
    text = placedName(labels, step.monsterId) + " 还在这一排上，没有被消灭。";

  return plainBeat(order, "消灭", text);
}

function renderPayment(step, order, result) {
  let text;
  if (step.failed)
    text = "加班后仍差 " + step.shortfall + "，这局失败。";
  else if (step.shortfall > 0)
    text = "还差 " + step.shortfall + "，进入加班。下次只要补上这个差额。";
  else
    text = "扣除应交 " + step.deducted + "，已经付清。";

  if (result.meetsExcess)
    text += "产出达到超额线 " + result.excessAt + "。";
  else
    text += "产出未达超额线 " + result.excessAt + "。";

  if (!step.failed && step.shortfall === 0 && step.wage)
    text += "这次得到金币 " + step.wage + "。";

  return plainBeat(order, "交款", text);
}

function beatHead(order, monsterName, skillName) {
  const head = el("header", "beat-head");
  head.append(el("span", "beat-index", String(order)));
  const title = el("p", "beat-title");
  title.append(el("strong", "", monsterName));
  title.append(document.createTextNode(" 触发了技能 "));
  title.append(el("b", "skill-name", skillName));
  head.append(title);
  return head;
}

function plainBeat(order, label, text) {
  const beat = el("article", "beat plain");
  const head = el("header", "beat-head");
  head.append(el("span", "beat-index", String(order)));
  const title = el("p", "beat-title");
  title.append(el("strong", "", label));
  head.append(title);
  beat.append(head);
  beat.append(el("p", "event-line", text));
  return beat;
}

function addMath(list, term, text, className) {
  list.append(el("dt", "", term));
  list.append(el("dd", className || "", text));
}

function quoteText(step) {
  if (landingIsSide(step))
    return "每只 " + step.quote;

  return String(step.quote);
}

function addsText(step) {
  const adds = step.adds || [];
  if (adds.length === 0)
    return "没有加项";

  const parts = adds.map(describeAdd);
  if (adds.length === 1)
    return parts[0];

  return parts.join("，") + "。加项合计 " + signed(addedOf(step));
}

function describeAdd(add) {
  const amount = signed(add.amount);
  if (add.label === "宿主修正")
    return "怪物修正 " + amount;
  if (add.label === "下家")
    return "上一只怪物留下的加项 " + amount;

  const kind = { Skill: "技能", Personality: "性格", Mood: "心情", Tool: "工具", Growth: "成长修正" }[add.sourceKind] || "来源";
  return kind + "「" + add.label + "」" + amount;
}

function baseText(step) {
  const added = addedOf(step);
  const core = joined(step.quote, added);
  if (landingIsSide(step))
    return step.quote + " × " + sideWord(step.skillName) + " " + step.sideCount + " 只 + " + added + " = " + step.base;

  if (added === 0)
    return step.base + "，就是报价本身";

  return core + " = " + step.base;
}

function joined(quote, added) {
  if (added === 0)
    return String(quote);
  if (added > 0)
    return quote + " + " + added;

  return quote + " - " + Math.abs(added);
}

function factorText(step) {
  const factors = step.factors || [];
  if (factors.length === 0)
    return "没有额外倍率，按 ×1";

  const parts = factors.map((factor) => "「" + factor.label + "」×" + factor.factor);
  if (factors.length === 1)
    return parts[0];

  return parts.join("，") + "。连乘后 ×" + step.multiplier;
}

function addedOf(step) {
  return (step.adds || []).reduce((total, add) => total + add.amount, 0);
}

function writebackText(step) {
  if (!step.writeback)
    return "没有写回";

  return "写回 " + signed(step.writeback) + "，加进这张技能，下次计分读得到";
}

function signed(amount) {
  if (amount > 0)
    return "+" + amount;

  return String(amount);
}

function landingIsSide(step) {
  if (typeof step.side === "boolean")
    return step.side;

  const skill = byName.get(step.skillName);
  return Boolean(skill && skill.effects.some((effect) => effect.kind === "SideCount"));
}

function sideWord(name) {
  const skill = byName.get(name);
  if (!skill)
    return "这一侧";

  const effect = skill.effects.find((item) => item.kind === "SideCount");
  if (!effect)
    return "这一侧";

  return effect.b < 0 ? "左侧" : "右侧";
}

function engineLabels(placed) {
  const labels = new Map();
  if (!placed)
    return labels;

  placed.forEach((engineId, index) => {
    const id = bench[index];
    if (!engineId || !id)
      return;

    labels.set(engineId, monsterTitle(monsters.get(id)));
  });
  return labels;
}

function placedName(labels, id) {
  if (!id)
    return "无目标";

  return labels.get(id) || id;
}

function showReport(text, ok) {
  report.textContent = text;
  report.className = ok ? "hit" : "miss";
}

function el(tag, className, text) {
  const node = document.createElement(tag);
  if (className)
    node.className = className;
  if (text)
    node.textContent = text;

  return node;
}

for (const input of document.querySelectorAll("#seed, #initial-energy")) input.addEventListener("change", renderBoard);
