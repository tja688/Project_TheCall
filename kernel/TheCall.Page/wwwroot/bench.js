import { dotnet } from "./_framework/dotnet.js";

const { getAssemblyExports, getConfig } = await dotnet.create();
const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);
const api = exports.TheCall.PageExports;

const status = document.querySelector("#status");
const skillList = document.querySelector("#skill-list");
const monsterBin = document.querySelector("#monster-bin");
const toolRow = document.querySelector("#tool-row");
const levelSelect = document.querySelector("#level");
const cells = document.querySelector("#cells");
const report = document.querySelector("#report");

let catalog = null;
const monsters = [];

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

catalog = JSON.parse(api.Catalog());
if (catalog.error) {
  status.textContent = catalog.error;
  throw new Error(catalog.error);
}

status.textContent = "规则已装入。技能、工具和关卡都来自 call-book.json。";
renderSkills();
renderTools();
renderLevels();
addMonster();
renderCells();

document.querySelector("#add-monster").addEventListener("click", () => {
  addMonster();
  renderCells();
});

document.querySelector("#settle").addEventListener("click", settle);
toolRow.addEventListener("change", renderCells);

function renderSkills() {
  skillList.replaceChildren();
  for (const skill of catalog.skills) {
    const item = document.createElement("li");
    const use = skill.use === "Active" ? "主动" : "被动";
    item.textContent = skill.name + " · " + skill.rarity + " · " + use + " · " + skill.sentence;
    skillList.append(item);
  }
}

function renderTools() {
  toolRow.replaceChildren();
  for (const tool of catalog.tools) {
    const label = document.createElement("label");
    const box = document.createElement("input");
    box.type = "checkbox";
    box.value = tool.name;
    label.append(box, document.createTextNode(tool.name));
    toolRow.append(label);
  }
}

function renderLevels() {
  levelSelect.replaceChildren();
  for (const level of catalog.levels) {
    const option = document.createElement("option");
    option.value = String(level.level);
    option.textContent = "第 " + level.level + " 关 · 应交 " + level.due + " · 超额 " + level.excess;
    levelSelect.append(option);
  }
}

function addMonster() {
  const card = document.createElement("div");
  card.className = "monster";
  const title = document.createElement("span");
  title.textContent = "怪物 " + (monsters.length + 1);
  card.append(title);
  const selects = [];
  for (let slot = 0; slot < 4; slot += 1) {
    const select = document.createElement("select");
    const empty = document.createElement("option");
    empty.value = "";
    empty.textContent = "空技能";
    select.append(empty);
    for (const skill of catalog.skills) {
      const option = document.createElement("option");
      option.value = skill.name;
      option.textContent = skill.name;
      select.append(option);
    }
    if (slot === 0)
      select.value = catalog.skills[0].name;
    select.addEventListener("change", renderCells);
    selects.push(select);
    card.append(select);
  }
  monsters.push({ card, selects });
  monsterBin.append(card);
}

function selectedTools() {
  return [...toolRow.querySelectorAll("input:checked")].map((box) => box.value);
}

function monsterLabel(index) {
  const names = monsters[index].selects.map((select) => select.value).filter(Boolean);
  return "怪物 " + (index + 1) + "（" + (names.join("、") || "无技能") + "）";
}

function renderCells() {
  const previous = [...cells.querySelectorAll("select")].map((select) => select.value);
  const counted = JSON.parse(api.Cells(JSON.stringify(selectedTools())));
  cells.replaceChildren();
  if (counted.error) {
    cells.textContent = counted.error;
    return;
  }
  for (let index = 0; index < counted.cells; index += 1) {
    const row = document.createElement("div");
    row.className = "cell";
    const label = document.createElement("span");
    label.textContent = "格 " + (index + 1);
    const select = document.createElement("select");
    const empty = document.createElement("option");
    empty.value = "";
    empty.textContent = "空";
    select.append(empty);
    monsters.forEach((_, monsterIndex) => {
      const option = document.createElement("option");
      option.value = String(monsterIndex);
      option.textContent = monsterLabel(monsterIndex);
      select.append(option);
    });
    if (previous[index])
      select.value = previous[index];
    row.append(label, select);
    cells.append(row);
  }
}

function settle() {
  const row = [...cells.querySelectorAll("select")].map((select) => {
    if (select.value === "")
      return null;
    const names = monsters[Number(select.value)].selects.map((item) => item.value).filter(Boolean);
    return { skills: names };
  });
  const body = {
    level: Number(levelSelect.value),
    tools: selectedTools(),
    row,
  };
  const result = JSON.parse(api.Score(JSON.stringify(body)));
  report.replaceChildren();
  if (result.error) {
    const error = document.createElement("p");
    error.className = "miss";
    error.textContent = result.error;
    report.append(error);
    return;
  }

  const summary = document.createElement("p");
  summary.className = result.meetsDue ? "hit" : "miss";
  summary.textContent =
    "产出 " + result.produced +
    " · 应交 " + result.due + " · " + (result.meetsDue ? "达到应交" : "未达应交") +
    " · 超额 " + result.excessAt + " · " + (result.meetsExcess ? "达到超额" : "未达超额");
  report.append(summary);

  const table = document.createElement("table");
  const head = document.createElement("tr");
  for (const title of ["怪物", "技能", "报价", "侧向人数", "加项", "倍率因子", "底数", "倍率", "能量", "写回"]) {
    const cell = document.createElement("th");
    cell.textContent = title;
    head.append(cell);
  }
  table.append(head);
  for (const landing of result.landings) {
    const line = document.createElement("tr");
    const values = [
      landing.monsterId,
      landing.skillName,
      String(landing.quote),
      String(landing.sideCount),
      landing.adds.map((add) => add.label + " +" + add.amount).join("，"),
      landing.factors.map((factor) => factor.label + " ×" + factor.factor).join("，"),
      String(landing.base),
      String(landing.multiplier),
      String(landing.energy),
      String(landing.writeback),
    ];
    for (const value of values) {
      const cell = document.createElement("td");
      cell.textContent = value;
      line.append(cell);
    }
    table.append(line);
  }
  report.append(table);

  if (result.swaps.length > 0 || result.removals.length > 0) {
    const extra = document.createElement("p");
    const swaps = result.swaps.map((swap) => swap.actorId + " → " + swap.targetId + (swap.happened ? " 发生" : " 未发生"));
    const removals = result.removals.map((removal) => (removal.monsterId || "无目标") + (removal.happened ? " 已消灭" : " 未消灭"));
    extra.textContent = ["换位", ...swaps, "消灭", ...removals].join(" · ");
    report.append(extra);
  }
}
