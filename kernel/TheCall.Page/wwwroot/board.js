// 能量产出台上，描述里的能量数怎样跟着排列变。
// 新的增益：在 auraFrom 里认出效果名。句子本身改 SkillSentences.cs，把要跟着变的数写成 Energy 片段。
// 这里只改描述中的那个数，不乘一侧有几只怪物，也不写成这只怪物最终产出多少。

export function auraFrom(effects) {
  let addToOthers = 0;
  let nextBonus = 0;
  let doubleAdjacent = false;
  for (const effect of effects) {
    if (effect.kind === "AddToOthers")
      addToOthers += effect.a;
    else if (effect.kind === "NextEnergyBonus")
      nextBonus += effect.a;
    else if (effect.kind === "DoubleAdjacentEnergy")
      doubleAdjacent = true;
  }

  return { addToOthers, nextBonus, doubleAdjacent };
}

export function energyAdjust(cells, skillsOf, auraOf) {
  return cells.map((_, index) => adjustAt(cells, index, skillsOf, auraOf));
}

export function shownEnergy(base, adjust) {
  return (base + adjust.add) * adjust.scale;
}

export function spanParts(spans, adjust) {
  const parts = [];
  for (const span of spans) {
    if (!Object.prototype.hasOwnProperty.call(span, "energy")) {
      parts.push({ text: span.text, live: false });
      continue;
    }

    const value = adjust ? shownEnergy(span.energy, adjust) : span.energy;
    parts.push({
      text: String(value),
      live: Boolean(adjust) && value !== span.energy,
    });
  }

  return parts;
}

function adjustAt(cells, index, skillsOf, auraOf) {
  if (!cells[index])
    return { add: 0, scale: 1 };

  let add = 0;
  for (let cell = 0; cell < cells.length; cell += 1) {
    if (cell === index || !cells[cell])
      continue;

    for (const name of skillsOf(cells[cell]))
      add += auraOf(name).addToOthers;
  }

  for (let cell = index - 1; cell >= 0; cell -= 1) {
    if (!cells[cell])
      continue;

    for (const name of skillsOf(cells[cell]))
      add += auraOf(name).nextBonus;

    break;
  }

  return { add, scale: doubleAt(cells, index - 1, skillsOf, auraOf) * doubleAt(cells, index + 1, skillsOf, auraOf) };
}

function doubleAt(cells, index, skillsOf, auraOf) {
  if (index < 0 || index >= cells.length || !cells[index])
    return 1;

  for (const name of skillsOf(cells[index])) {
    if (auraOf(name).doubleAdjacent)
      return 2;
  }

  return 1;
}
