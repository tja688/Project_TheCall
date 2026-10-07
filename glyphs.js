// 怪物图案。想给技能指定形状，就在 MARKS 里加一行技能名。没写的按名字取一个几何形。

const INK = {
  White: "#d5ebe3",
  Blue: "#8ec5ff",
  Gold: "#f0c460",
};

const DARK = "#102028";

const MARKS = {
  能量吐息: rays,
  左能量体: () => tri(46, 38, 22, 30, 22, 46),
  右能量体: () => tri(18, 38, 42, 30, 42, 46),
  增量小手: () => plus(32, 38, 6),
  增量大手: () => plus(32, 38, 10),
  残留提取腺体: drop,
  孤独心: gem,
  吞噬大嘴: mouth,
  双重吐息: () => `<circle cx="26" cy="38" r="4" fill="${DARK}"/><circle cx="38" cy="38" r="4" fill="${DARK}"/>`,
  时间操控器官: clock,
  再回首头: turn,
  分享之手: () => `<rect x="22" y="32" width="8" height="8" fill="${DARK}"/><rect x="34" y="36" width="8" height="8" fill="${DARK}"/>`,
  太阳能头: sun,
  换位手: swap,
  鼓励嘴: cheer,
};

const FALLBACK = [rays, gem, drop, mouth, clock, sun, cheer, swap];

export function monsterSvg(name, rarity) {
  const ink = INK[rarity] || INK.White;
  const mark = MARKS[name] || FALLBACK[hash(name) % FALLBACK.length];
  return `<svg viewBox="0 0 64 64" aria-hidden="true"><circle cx="32" cy="16" r="9" fill="${ink}"/><rect x="16" y="24" width="32" height="28" rx="12" fill="${ink}"/>${mark()}</svg>`;
}

function hash(name) {
  let value = 0;
  for (const char of name)
    value = (value * 33 + char.codePointAt(0)) >>> 0;

  return value;
}

function plus(x, y, reach) {
  return `<path d="M${x} ${y - reach}v${reach * 2}M${x - reach} ${y}h${reach * 2}" stroke="${DARK}" stroke-width="3" stroke-linecap="square"/>`;
}

function tri(x1, y1, x2, y2, x3, y3) {
  return `<polygon points="${x1},${y1} ${x2},${y2} ${x3},${y3}" fill="${DARK}"/>`;
}

function rays() {
  return `<path d="M44 34h8M42 40h8M44 46h6" stroke="${DARK}" stroke-width="3" stroke-linecap="square"/>`;
}

function drop() {
  return `<path d="M32 30c4 5 7 8 7 11a7 7 0 0 1-14 0c0-3 3-6 7-11z" fill="${DARK}"/>`;
}

function gem() {
  return `<polygon points="32,30 40,38 32,48 24,38" fill="${DARK}"/>`;
}

function mouth() {
  return `<path d="M22 36h20v3a10 8 0 0 1-20 0z" fill="${DARK}"/>`;
}

function clock() {
  return `<circle cx="32" cy="38" r="8" fill="none" stroke="${DARK}" stroke-width="3"/><path d="M32 38V33M32 38l4 3" stroke="${DARK}" stroke-width="2"/>`;
}

function turn() {
  return `<path d="M24 42a8 8 0 1 1 8 6" fill="none" stroke="${DARK}" stroke-width="3"/><polygon points="32,44 32,52 26,48" fill="${DARK}"/>`;
}

function sun() {
  return `<circle cx="32" cy="16" r="4" fill="${DARK}"/><path d="M32 4v4M20 10l3 3M44 10l-3 3M32 34v6" stroke="${DARK}" stroke-width="3" stroke-linecap="square"/>`;
}

function swap() {
  return `<polygon points="18,34 28,34 28,30 36,38 28,46 28,42 18,42" fill="${DARK}"/><polygon points="46,42 36,42 36,46 28,38 36,30 36,34 46,34" fill="${DARK}" opacity="0.55"/>`;
}

function cheer() {
  return `<path d="M24 44l8-12 8 12" fill="none" stroke="${DARK}" stroke-width="4" stroke-linejoin="round"/>`;
}
