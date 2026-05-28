import fs from "node:fs";
import path from "node:path";

const outDir = path.resolve("Docs/UIWindows");
fs.mkdirSync(outDir, { recursive: true });

const W = 1920;
const H = 1080;

function esc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function wrap(value, max = 48) {
  const words = String(value ?? "").split(/\s+/).filter(Boolean);
  const lines = [];
  let line = "";
  for (const word of words) {
    const next = line ? `${line} ${word}` : word;
    if (next.length > max && line) {
      lines.push(line);
      line = word;
    } else {
      line = next;
    }
  }
  if (line) lines.push(line);
  return lines;
}

function rect(x, y, w, h, cls = "", extra = "") {
  return `<rect x="${x}" y="${y}" width="${w}" height="${h}" ${cls ? `class="${cls}"` : ""} ${extra}/>`;
}

function line(x1, y1, x2, y2, cls = "line", extra = "") {
  return `<line x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}" class="${cls}" ${extra}/>`;
}

function circle(cx, cy, r, cls = "", extra = "") {
  return `<circle cx="${cx}" cy="${cy}" r="${r}" ${cls ? `class="${cls}"` : ""} ${extra}/>`;
}

function text(x, y, value, cls = "txt", extra = "") {
  return `<text x="${x}" y="${y}" class="${cls}" ${extra}>${esc(value)}</text>`;
}

function multiline(x, y, value, cls = "txt", max = 48, lh = 24) {
  return wrap(value, max)
    .map((lineText, index) => text(x, y + index * lh, lineText, cls))
    .join("\n");
}

function panel(x, y, w, h, title = "", body = "", cls = "panel") {
  const head = title ? text(x + 24, y + 42, title, "h1") : "";
  return `<g class="shadow">${rect(x, y, w, h, cls, 'rx="10"')}${head}${body}</g>`;
}

function chip(x, y, value, tone = "chip") {
  return `<g>${rect(x, y, Math.max(74, value.length * 9 + 28), 30, tone, 'rx="15"')}${text(x + 14, y + 21, value, "chipText")}</g>`;
}

function button(x, y, w, h, value, primary = false) {
  const labelClass = primary && w >= 300 && value.length <= 12 ? "buttonTextBig" : "buttonText";
  return `<g>${rect(x, y, w, h, primary ? "btnPrimary" : "btn", 'rx="8"')}${text(x + w / 2, y + h / 2 + 8, value, labelClass, 'text-anchor="middle"')}</g>`;
}

function bar(x, y, w, pct, tone = "green", label = "") {
  const fill = Math.max(0, Math.min(1, pct)) * (w - 4);
  return `<g>${rect(x, y, w, 12, "barBg", 'rx="6"')}${rect(x + 2, y + 2, fill, 8, tone, 'rx="4"')}${label ? text(x + w + 12, y + 12, label, "small") : ""}</g>`;
}

function metric(x, y, w, label, value, tone = "accent") {
  return `<g>${rect(x, y, w, 74, "panelSoft", 'rx="8"')}${text(x + 18, y + 25, label, "small")}${text(x + 18, y + 57, value, `num ${tone}`)}</g>`;
}

function row(x, y, w, title, meta, pct = null, tone = "green") {
  const narrow = w < 280;
  const p = pct == null ? "" : bar(x + w - (narrow ? 136 : 178), y + 19, narrow ? 88 : 120, pct, tone, `${Math.round(pct * 100)}%`);
  return `<g>${rect(x, y, w, 58, "row", 'rx="7"')}${text(x + 16, y + 24, title, "txt")}${text(x + 16, y + 46, meta, "small")}${p}</g>`;
}

function topBar(title, subtitle, mode = "07:42 · день 163") {
  return `<g class="shadow">
    ${rect(24, 20, 1872, 74, "topBar", 'rx="10"')}
    ${text(54, 58, title, "title")}
    ${text(54, 82, subtitle, "small")}
    ${text(1540, 58, mode, "h2", 'text-anchor="end"')}
    ${button(1574, 31, 156, 52, "СОХРАНИТЬ")}
    ${button(1744, 31, 62, 52, "⚙")}
  </g>`;
}

const navItems = [
  "ГОРОД",
  "КАРТА",
  "АНГАР",
  "СБОРКА",
  "ПРОИЗВОДСТВО",
  "ИССЛЕДОВАНИЯ",
  "ФЛОТ",
  "РАЗВЕДКА",
  "ЭНЦИКЛОПЕДИЯ"
];

function nav(active) {
  const items = navItems.map((item, i) => {
    const y = 134 + i * 76;
    const cls = item === active ? "navActive" : "navItem";
    return `<g>${rect(34, y, 244, 58, cls, 'rx="8"')}${text(58, y + 37, item, "navText")}</g>`;
  }).join("\n");
  return `<g class="shadow">${rect(20, 112, 278, 736, "panel", 'rx="10"')}${items}</g>`;
}

function bottomShipBar(action = "ВЗЛЕТЕТЬ") {
  const body = [
    metric(50, 924, 210, "ДОК", "Столица"),
    metric(276, 924, 230, "КОРАБЛЬ", "Пионер"),
    metric(522, 924, 190, "КОРПУС", "92%", "green"),
    metric(728, 924, 220, "ГРУЗ", "312/420 кг", "gold"),
    metric(964, 924, 220, "ТОПЛИВО", "1240 кг", "green"),
    metric(1200, 924, 220, "КЛАВДИЙ", "480 кг", "blue"),
    button(1534, 922, 328, 86, action, true)
  ].join("\n");
  return panel(22, 898, 1876, 130, "", body);
}

function cloudSky() {
  return `<rect width="${W}" height="${H}" fill="url(#sky)"/>
    <rect width="${W}" height="${H}" fill="url(#storm)"/>
    <ellipse class="cloud" cx="340" cy="250" rx="360" ry="120"/>
    <ellipse class="cloud2" cx="1120" cy="190" rx="470" ry="140"/>
    <ellipse class="cloud" cx="1510" cy="540" rx="420" ry="140"/>
    <ellipse class="cloud2" cx="700" cy="580" rx="540" ry="150"/>`;
}

function darkBackdrop() {
  return `<rect width="${W}" height="${H}" fill="#121923"/>
    <rect width="${W}" height="${H}" fill="url(#gridFade)"/>
    ${Array.from({ length: 19 }, (_, i) => line(0, 120 + i * 50, W, 120 + i * 50, "grid")).join("")}
    ${Array.from({ length: 20 }, (_, i) => line(60 + i * 96, 0, 60 + i * 96, H, "grid")).join("")}`;
}

function island(cx, cy, name, tone = "gold") {
  return `<g class="shadow">
    <ellipse cx="${cx}" cy="${cy + 28}" rx="116" ry="38" class="rock"/>
    <rect x="${cx - 92}" y="${cy - 12}" width="184" height="44" class="island" rx="18"/>
    ${circle(cx - 38, cy - 28, 10, tone)}
    ${circle(cx + 30, cy - 18, 7, "lamp")}
    ${text(cx, cy + 70, name, "mapLabel", 'text-anchor="middle"')}
  </g>`;
}

function shipSilhouette(x, y, scale = 1, tone = "ship") {
  return `<g transform="translate(${x} ${y}) scale(${scale})">
    <ellipse class="${tone}" cx="260" cy="78" rx="244" ry="60"/>
    <rect class="shipDark" x="72" y="100" width="350" height="68" rx="22"/>
    <rect class="shipDark" x="160" y="36" width="140" height="58" rx="20"/>
    <line class="line" x1="30" y1="78" x2="520" y2="78"/>
    <circle class="lamp" cx="440" cy="106" r="7"/>
  </g>`;
}

function mapNetwork() {
  return `<g>
    ${line(900, 470, 610, 610, "route")}
    ${line(900, 470, 1180, 610, "route")}
    ${line(900, 470, 1130, 310, "route")}
    ${line(610, 610, 1180, 610, "route routeDim")}
    ${island(900, 470, "Столица", "gold")}
    ${island(610, 610, "Ферма отца", "green")}
    ${island(1180, 610, "Аэролит", "blue")}
    ${island(1130, 310, "Угольный причал", "rust")}
    ${island(1370, 420, "Клавдиевая гряда", "blue")}
  </g>`;
}

function shell({ title, subtitle, active, center = "", right = "", bottom = true, backdrop = "sky", mode }) {
  const bg = backdrop === "dark" ? darkBackdrop() : cloudSky();
  return svg(`${bg}${topBar(title, subtitle, mode)}${active ? nav(active) : ""}${center}${right}${bottom ? bottomShipBar() : ""}`);
}

function svg(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">
  <defs>
    <style>
      .topBar,.panel{fill:#12100d;stroke:#8a6334;stroke-width:2}
      .panelSoft,.row{fill:#1b1711;stroke:#5f4627;stroke-width:1.4}
      .row{fill:#181d20}
      .navItem{fill:#17140f;stroke:#5f4627;stroke-width:1.4}
      .navActive{fill:#33230f;stroke:#d79a43;stroke-width:2.2}
      .btn{fill:#241b10;stroke:#b47a38;stroke-width:2}
      .btnPrimary{fill:#3a260f;stroke:#d79a43;stroke-width:3}
      .chip{fill:#24302a;stroke:#789c63;stroke-width:1.3}
      .chipBlue{fill:#1d2c35;stroke:#5db4d8;stroke-width:1.3}
      .chipWarn{fill:#3a241d;stroke:#c85b42;stroke-width:1.3}
      .barBg{fill:#17140f;stroke:#5f4627;stroke-width:1}
      .green{fill:#7fbc48}.gold,.accent{fill:#e5c17a}.blue{fill:#5db4d8}.rust{fill:#c85b42}.yellow{fill:#dca94a}.red{fill:#c85b42}
      .cloud{fill:#f4dfb5;opacity:.18}.cloud2{fill:#d9c8aa;opacity:.24}
      .rock{fill:#20252b}.island{fill:#30353a;stroke:#80613a;stroke-width:2}
      .ship{fill:#2d3440;stroke:#b68446;stroke-width:2}.shipDark{fill:#151a21;stroke:#775736;stroke-width:2}
      .lamp{fill:#f2b45f;opacity:.88}
      .line{stroke:#8a6334;stroke-width:2}.route{stroke:#d79a43;stroke-width:4;fill:none;stroke-dasharray:12 10}.routeDim{opacity:.4}
      .grid{stroke:#6f7f82;stroke-width:1;opacity:.09}
      .title{font:700 30px Georgia,'Times New Roman',serif;fill:#f0d28c}
      .h1{font:700 24px Georgia,'Times New Roman',serif;fill:#f0d28c}
      .h2{font:700 18px Inter,Arial,sans-serif;fill:#e5c17a}
      .txt{font:500 16px Inter,Arial,sans-serif;fill:#e2d3ad}
      .small{font:500 13px Inter,Arial,sans-serif;fill:#a79369}
      .micro{font:500 11px Inter,Arial,sans-serif;fill:#a79369}
      .num{font:700 22px Inter,Arial,sans-serif;fill:#f0d28c}
      .buttonText{font:700 15px Inter,Arial,sans-serif;fill:#e5c17a}
      .buttonTextBig{font:700 30px Georgia,'Times New Roman',serif;fill:#f0d28c}
      .navText{font:700 15px Inter,Arial,sans-serif;fill:#e5c17a}
      .chipText{font:700 13px Inter,Arial,sans-serif;fill:#d9e3cf}
      .mapLabel{font:700 16px Inter,Arial,sans-serif;fill:#f0d28c}
      .shadow{filter:url(#shadow)}
    </style>
    <filter id="shadow" x="-10%" y="-10%" width="120%" height="130%">
      <feDropShadow dx="0" dy="5" stdDeviation="6" flood-color="#000" flood-opacity=".45"/>
    </filter>
    <linearGradient id="sky" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#526b83"/><stop offset=".52" stop-color="#d7b782"/><stop offset="1" stop-color="#273747"/>
    </linearGradient>
    <linearGradient id="storm" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#2b4054" stop-opacity=".05"/><stop offset="1" stop-color="#091016" stop-opacity=".82"/>
    </linearGradient>
    <linearGradient id="gridFade" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#2f4c56" stop-opacity=".35"/><stop offset="1" stop-color="#090b0e" stop-opacity=".8"/>
    </linearGradient>
  </defs>
  ${body}
</svg>
`;
}

function cityDock() {
  const center = `
    <g opacity=".86">
      <path class="rock" d="M670 570 C880 505 1275 505 1480 600 L1400 748 L780 748 Z"/>
      <rect class="island" x="700" y="530" width="730" height="55" rx="18"/>
      ${rect(815, 388, 92, 148, "island")}
      ${rect(950, 330, 118, 210, "island")}
      ${rect(1110, 370, 96, 168, "island")}
      ${rect(1240, 410, 120, 126, "island")}
      ${line(770, 585, 1490, 720)}${line(720, 610, 1505, 610)}
    </g>
    ${shipSilhouette(360, 650, 1)}
    ${panel(338, 116, 445, 140, "Столица · Док-7", multiline(370, 178, "Главный хаб: снабжение, верфь, исследования, маршруты. Город стабилен, но комфорт и безопасность проседают.", "txt", 48))}
    ${panel(338, 278, 1118, 160, "Быстрые действия", `
      ${button(370, 326, 190, 54, "РЫНОК")}${button(580, 326, 210, 54, "МАСТЕРСКАЯ")}${button(812, 326, 214, 54, "МАРШРУТЫ")}
      ${button(1048, 326, 220, 54, "ИССЛЕДОВАНИЯ")}${button(1290, 326, 130, 54, "КАРТА")}
    `)}
    ${panel(338, 462, 1118, 382, "Состояние дока", `
      ${row(370, 520, 500, "Погрузка", "312 кг груза на борту, скорость дока 0.8 сек/предмет", .74, "yellow")}
      ${row(370, 594, 500, "Заправка", "уголь 1240 кг, клавдий 480 кг", .82, "green")}
      ${row(370, 668, 500, "Текущий заказ", "аэролитовый остров → столица", .46, "blue")}
      ${metric(910, 520, 230, "ДЕНЬГИ", "12 480")}
      ${metric(1160, 520, 230, "ОПЫТ", "фунд. 36")}
      ${metric(910, 616, 230, "СКЛАД", "18 типов")}
      ${metric(1160, 616, 230, "ДОКИ", "3 активны")}
    `)}
  `;
  const right = `${panel(1508, 118, 384, 400, "Потребности города", `
    ${row(1540, 182, 316, "Рабочая сила", "еда", .91, "green")}
    ${row(1540, 248, 316, "Здоровье", "медикаменты", .82, "green")}
    ${row(1540, 314, 316, "Безопасность", "оружие", .68, "yellow")}
    ${row(1540, 380, 316, "Комфорт", "ткань", .61, "yellow")}
    ${row(1540, 446, 316, "Ремонт", "инструменты", .72, "yellow")}
  `)}${panel(1508, 538, 384, 300, "Активные проекты", `
    ${row(1540, 600, 316, "Корабельный док II", "осталось 02:34:18", .46, "yellow")}
    ${row(1540, 674, 316, "Больница II", "осталось 01:15:42", .72, "green")}
    ${row(1540, 748, 316, "Лаборатория", "нужна бумага: 6", .33, "blue")}
  `)}`;
  return shell({ title: "ГОРОД / ДОК", subtitle: "доковый режим, склад, потребности и старт вылета", active: "ГОРОД", center, right });
}

function startMenu() {
  const body = `${cloudSky()}
    <g opacity=".72">${shipSilhouette(1050, 420, 1.15)}</g>
    <g class="shadow">
      ${rect(84, 118, 560, 812, "panel", 'rx="12"')}
      ${text(124, 214, "WILD WIND", "title")}
      ${text(126, 252, "летучая сеть островов", "h2")}
      ${button(126, 350, 456, 68, "НОВЫЙ МИР", true)}
      ${button(126, 444, 456, 64, "ПРОДОЛЖИТЬ")}
      ${button(126, 528, 456, 64, "НАСТРОЙКИ")}
      ${button(126, 612, 456, 64, "ВЫЙТИ")}
      ${multiline(126, 772, "Текущий фон стартового экрана: широкий город на острове, старый Пионер у дока и первые маршруты к ферме и аэролиту.", "txt", 44, 24)}
    </g>
    ${panel(1180, 120, 570, 230, "Последнее сохранение", `
      ${metric(1212, 186, 160, "ДЕНЬ", "163")}
      ${metric(1392, 186, 160, "РЕЖИМ", "Док")}
      ${metric(1572, 186, 140, "ДОК", "Столица")}
      ${text(1214, 300, "Автосохранение: Wild Wind / slot_01", "txt")}
    `)}`;
  return svg(body);
}

function saveSlotsSettings() {
  const body = `${darkBackdrop()}${topBar("СОХРАНЕНИЯ И НАСТРОЙКИ", "слоты мира, язык, управление, звук", "меню")}
    ${panel(84, 128, 760, 812, "Продолжить", `
      ${row(122, 194, 686, "Слот 1 · Столица", "день 163, Пионер, доковый режим", .92, "green")}
      ${row(122, 274, 686, "Слот 2 · Аэролитовый остров", "день 41, доставка еды завершена", .54, "blue")}
      ${row(122, 354, 686, "Слот 3 · пусто", "новая история", .06, "rust")}
      ${button(122, 808, 220, 58, "НАЗАД")}
      ${button(588, 808, 220, 58, "ЗАГРУЗИТЬ", true)}
    `)}
    ${panel(900, 128, 936, 812, "Настройки", `
      ${row(938, 194, 820, "Язык", "Русский / English", .50, "blue")}
      ${row(938, 274, 820, "Громкость музыки", "спокойная тема дока", .68, "green")}
      ${row(938, 354, 820, "Громкость мира", "ветер, двигатели, док", .74, "green")}
      ${row(938, 434, 820, "Управление", "WASD, Q/E высота, ПКМ орбита, колесо зум", .82, "yellow")}
      ${row(938, 514, 820, "Автосохранение при стыковке", "включено", .95, "green")}
      ${button(938, 808, 220, 58, "СБРОСИТЬ")}
      ${button(1538, 808, 220, 58, "ПРИМЕНИТЬ", true)}
    `)}`;
  return svg(body);
}

function islandDock() {
  const center = `${mapNetwork()}
    ${panel(338, 116, 1118, 240, "Аэролитовый остров", `
      ${multiline(370, 178, "Остров добывает аэролит и просит еду, инструменты и связь со столицей. На поздних стадиях открывает рудные линии и локальный ангар.", "txt", 80)}
      ${metric(370, 264, 180, "АРХЕТИП", "руда")}
      ${metric(572, 264, 180, "ДОК", "130 м")}
      ${metric(774, 264, 180, "ПОГРУЗКА", "0.8 c")}
      ${metric(976, 264, 210, "СТАДИЯ", "II")}
      ${metric(1208, 264, 200, "ПИЛОТЫ", "1/3")}
    `)}
    ${panel(338, 388, 1118, 460, "Островная инфраструктура", `
      ${row(370, 454, 500, "Базовая выработка", "аэролит +12 кг/час", .72, "blue")}
      ${row(370, 528, 500, "Склад острова", "аэролит 184, еда 20, инструменты 4", .62, "yellow")}
      ${row(370, 602, 500, "Стройка", "грузовой док II, нужен металл", .36, "rust")}
      ${row(370, 676, 500, "Локальный ангар", "один Пионер выполняет короткие рейсы", .55, "green")}
      ${button(928, 478, 210, 58, "ПОГРУЗКА")}${button(1160, 478, 210, 58, "СТРОИТЬ")}
      ${button(928, 558, 210, 58, "МАРШРУТ")}${button(1160, 558, 210, 58, "АНГАР")}
    `)}`;
  const right = `${panel(1508, 118, 384, 732, "Потребности острова", `
    ${row(1540, 182, 316, "Еда", "ускоряет добычу", .42, "rust")}
    ${row(1540, 248, 316, "Инструменты", "ремонт и рост", .28, "rust")}
    ${row(1540, 314, 316, "Ткань", "комфорт рабочих", .61, "yellow")}
    ${row(1540, 380, 316, "Связь", "пассажиры в столицу", .76, "green")}
    ${chip(1540, 478, "лучший следующий рейс", "chipBlue")}
    ${multiline(1540, 540, "Привезти 20 еды с Фермы отца. Это поднимет базовую выработку и откроет запас аэролита для столицы.", "txt", 34)}
  `)}`;
  return shell({ title: "ОСТРОВ / ДОК", subtitle: "локальные потребности, стройка, склад и ангар", active: "ГОРОД", center, right });
}

function flightHud() {
  const body = `${cloudSky()}
    <g opacity=".9">${shipSilhouette(650, 430, 1.25)}</g>
    <g class="shadow">
      ${rect(80, 24, 1760, 90, "topBar", 'rx="10"')}
      ${text(112, 60, "ПОЛЁТ", "title")}
      ${text(280, 54, "Миссия: доставка аэролита в столицу", "h2")}
      ${text(280, 86, "Маршрут: Аэролитовый остров → Столица · дистанция 4.1 км", "small")}
      ${text(1580, 76, "курс 045°", "num", 'text-anchor="middle"')}
    </g>
    <g class="shadow">
      ${rect(540, 138, 840, 76, "panel", 'rx="10"')}
      ${line(600, 176, 1320, 176, "line")}
      ${["330","345","N","015","030","045","060"].map((v, i) => text(620 + i * 110, 166, v, i === 2 ? "h2" : "small", 'text-anchor="middle"')).join("")}
      ${line(1040, 146, 1040, 202, "route")}
      ${text(1040, 130, "ЦЕЛЬ", "small", 'text-anchor="middle"')}
    </g>
    ${panel(42, 682, 490, 284, "Журнал полёта", `
      ${row(76, 744, 420, "Автопилот", "цель из активной задачи", .80, "green")}
      ${row(76, 816, 420, "Стыковка", "автостыковка включена", .60, "blue")}
      ${row(76, 888, 420, "Предупреждение", "ветер боковой, компенсируется", .34, "yellow")}
    `)}
    ${panel(560, 736, 800, 230, "Управление", `
      ${metric(596, 792, 150, "ВЫСОТА", "2550 м", "blue")}
      ${metric(764, 792, 150, "ЦЕЛЬ", "2600 м", "blue")}
      ${metric(932, 792, 150, "СКОРОСТЬ", "34 м/с")}
      ${metric(1100, 792, 150, "ТЯГА", "3/5", "green")}
      ${button(596, 890, 120, 46, "←")}${button(724, 890, 120, 46, "↑")}${button(852, 890, 120, 46, "→")}
      ${button(1010, 890, 160, 46, "ФОРСАЖ")}${button(1184, 890, 140, 46, "МЕНЮ")}
    `)}
    ${panel(1482, 164, 330, 802, "Мощность", `
      ${bar(1524, 236, 226, .76, "green", "двиг.")}
      ${bar(1524, 304, 226, .58, "blue", "контур")}
      ${bar(1524, 372, 236, .42, "yellow", "винт")}
      ${row(1524, 460, 248, "Высота", "Assist", .50, "blue")}
      ${row(1524, 532, 248, "Курс", "Autopilot", .95, "green")}
      ${row(1524, 604, 248, "Скорость", "Autopilot", .95, "green")}
      ${button(1524, 862, 248, 58, "СТЫКОВАТЬСЯ", true)}
    `)}`;
  return svg(body);
}

function worldMapRoutes() {
  const center = `${panel(318, 116, 870, 732, "Карта маршрутов", `${mapNetwork()}${shipSilhouette(760, 690, .55)}`)}
    ${panel(1214, 116, 306, 732, "Выбранный маршрут", `
      ${text(1244, 180, "Еда, аэролит, столица", "h2")}
      ${row(1244, 222, 238, "1. Ферма", "загрузить еду x20", .95, "green")}
      ${row(1244, 292, 238, "2. Аэролит", "выгрузить еду, взять аэролит", .48, "yellow")}
      ${row(1244, 362, 238, "3. Столица", "выгрузить аэролит", .10, "blue")}
      ${metric(1244, 468, 238, "ПЕТЛЯ", "вкл.")}
      ${metric(1244, 560, 238, "ETA", "18:42")}
      ${button(1244, 748, 238, 58, "РЕДАКТИРОВАТЬ")}
    `)}`;
  const right = `${panel(1548, 116, 344, 732, "Маршруты", `
    ${row(1580, 182, 280, "Грузовичок 01", "летит к Аэролиту", .62, "green")}
    ${row(1580, 252, 280, "Топливщик", "ждёт уголь", .18, "rust")}
    ${row(1580, 322, 280, "Вахта", "готов к запуску", .80, "blue")}
    ${row(1580, 392, 280, "Фургонщик", "нет маршрута", .02, "yellow")}
    ${button(1580, 748, 280, 58, "НОВЫЙ МАРШРУТ", true)}
  `)}`;
  return shell({ title: "КАРТА / МАРШРУТЫ", subtitle: "острова, рейсы, автопилоты и петли снабжения", active: "КАРТА", center, right, backdrop: "dark" });
}

function cargoTransfer() {
  const center = `${panel(338, 116, 1118, 732, "Погрузка и склад", `
    ${text(382, 182, "Склад острова", "h1")}${text(1040, 182, "Трюм Пионера", "h1")}
    ${["еда 340","аэролит 184","уголь 620","бумага 90","инструменты 12"].map((v, i) => row(382, 226 + i * 70, 420, v, "доступно на доке", .25 + i * .12, i % 2 ? "blue" : "green")).join("")}
    ${["еда 20","аэролит 12","уголь 80","клавдий 30"].map((v, i) => row(1040, 226 + i * 70, 330, v, "на борту", .45 + i * .1, i % 2 ? "yellow" : "green")).join("")}
    ${line(840, 270, 1002, 270, "route")}${line(840, 410, 1002, 410, "route routeDim")}
    ${button(814, 596, 174, 54, "ВЗЯТЬ →")}${button(814, 668, 174, 54, "← ВЫГРУЗИТЬ")}
    ${bar(382, 776, 988, .74, "yellow", "312 / 420 кг")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "План операции", `
    ${row(1540, 180, 316, "1. Загрузить еду", "20 кг, 16 сек", .80, "green")}
    ${row(1540, 250, 316, "2. Дозаправить уголь", "до 120 кг", .62, "green")}
    ${row(1540, 320, 316, "3. Клавдий", "до 50 кг", .40, "blue")}
    ${row(1540, 390, 316, "Ограничение", "масса взлёта в норме", .92, "green")}
    ${button(1540, 716, 316, 58, "ЗАПУСТИТЬ ПОГРУЗКУ", true)}
  `)}`;
  return shell({ title: "ПОГРУЗКА", subtitle: "склад острова, трюм, топливо, клавдий и очередь операций", active: "ГОРОД", center, right });
}

function hangarCatalog() {
  const center = `${panel(338, 116, 1118, 732, "Каталог кораблей", `
    ${["Пионер","Водомерка","Опора","Топливщик","Паровоз","Булат","Шершень","Егерь","Жидковоз","Глетчер","Вахта","Фургонщик","Стапель"].map((v, i) => {
      const x = 370 + (i % 3) * 340; const y = 190 + Math.floor(i / 3) * 120;
      return `<g>${rect(x, y, 300, 92, "panelSoft", 'rx="8"')}${text(x + 20, y + 34, v, "h2")}${text(x + 20, y + 62, i < 1 ? "R0 · стартовый" : i < 8 ? "R1 · специализированный" : "R2 · тендер", "small")}${bar(x + 20, y + 72, 180, (i % 5 + 3) / 8, i % 2 ? "blue" : "green")}</g>`;
    }).join("")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Пионер", `
    ${shipSilhouette(1540, 184, .55)}
    ${metric(1540, 370, 146, "РАНГ", "R0")}
    ${metric(1710, 370, 146, "РОЛЬ", "универс.")}
    ${row(1540, 470, 316, "Груз", "420 кг", .72, "green")}
    ${row(1540, 540, 316, "Скорость", "26 м/с", .48, "blue")}
    ${row(1540, 610, 316, "Топливо", "уголь", .62, "yellow")}
    ${button(1540, 748, 316, 58, "ОТКРЫТЬ СБОРКУ", true)}
  `)}`;
  return shell({ title: "АНГАР", subtitle: "дерево кораблей, роли и готовность к покупке/сборке", active: "АНГАР", center, right, backdrop: "dark" });
}

function shipAssembly() {
  const slots = ["Корпус","Двигатель","Винт","Клавдиевый контур","Грузовой стеллаж","Навигация","Свободный слот"].map((v, i) =>
    row(382, 210 + i * 72, 430, v, i < 5 ? "установлено" : "пусто", i < 5 ? .9 : .05, i < 5 ? "green" : "rust")).join("");
  const center = `${panel(338, 116, 1118, 732, "Сборка корабля", `
    ${shipSilhouette(860, 256, 1.2)}
    ${text(382, 180, "Слоты Пионера", "h1")}${slots}
    ${metric(910, 540, 180, "МАССА", "1180 кг")}
    ${metric(1110, 540, 180, "ЛИМИТ", "1700 кг", "green")}
    ${metric(1310, 540, 120, "МОЖНО", "да", "green")}
    ${button(910, 656, 250, 58, "АВТОЗАПОЛНИТЬ")}${button(1180, 656, 250, 58, "ПРИМЕНИТЬ", true)}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Модули", `
    ${row(1540, 180, 316, "Грузовой стеллаж", "стартовый", .80, "green")}
    ${row(1540, 250, 316, "Газосборщик", "для облаков", .54, "blue")}
    ${row(1540, 320, 316, "Рудный трюм", "для глыб", .46, "yellow")}
    ${row(1540, 390, 316, "Гарпунный пост", "охота", .30, "rust")}
    ${row(1540, 460, 316, "Наблюдатели", "разведка", .62, "blue")}
    ${multiline(1540, 570, "Сборка проверяет грузоподъёмность, обязательные слоты, баки топлива/клавдия и доступность технологий.", "txt", 34)}
  `)}`;
  return shell({ title: "СБОРКА", subtitle: "корпус, узлы, модули и проверка возможности взлёта", active: "СБОРКА", center, right, backdrop: "dark" });
}

function production() {
  const center = `${panel(338, 116, 1118, 732, "Каскадное производство", `
    ${text(370, 184, "Цель: Кувалда · R3/T2 рудный корвет", "h1")}
    ${["Конструкционный цех 8.4 ч","Узловой цех 1.4 ч","Моторостроительный 0.4 ч","Крупноузловой 0.7 ч","Лёгкая промышленность 0.2 ч","Корабельный док 1.5 ч"].map((v, i) =>
      row(370, 238 + i * 68, 620, v, i === 0 ? "главное узкое место" : "нагрузка 1x", [1,.17,.05,.08,.03,.18][i], i === 0 ? "rust" : "blue")).join("")}
    ${panel(1030, 238, 390, 420, "Смета", `
      ${text(1060, 300, "балка 765 · ферма 328", "txt")}
      ${text(1060, 336, "обшивка 238 · лист 170", "txt")}
      ${text(1060, 372, "шестерни 112 · трубки 78", "txt")}
      ${text(1060, 408, "кальцит 296 · сильвин 163", "txt")}
      ${text(1060, 444, "инструменты 12 · медикаменты 20", "txt")}
      ${metric(1060, 514, 150, "ИТОГО", "9.9 ч")}
      ${metric(1230, 514, 150, "СБОРКА", "1.5 ч")}
    `)}
    ${button(370, 740, 240, 58, "УЛУЧШИТЬ ЦЕХ")}${button(630, 740, 240, 58, "ЗАПУСТИТЬ", true)}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Потоковые линии", `
    ${row(1540, 180, 316, "Бумажная мастерская", "генерация", .90, "green")}
    ${row(1540, 250, 316, "Дробилка руды", "переработка", .68, "yellow")}
    ${row(1540, 320, 316, "Перегонка конденсата", "газ", .42, "blue")}
    ${row(1540, 390, 316, "Пороховая реакция", "риск партии", .33, "rust")}
    ${row(1540, 460, 316, "Клавдиевый маховик", "конверсия", .72, "blue")}
    ${row(1540, 530, 316, "Сборка корпуса", "assembly", .55, "yellow")}
  `)}`;
  return shell({ title: "ПРОИЗВОДСТВО", subtitle: "потоки, каскад, смета, bottleneck и запуск крупных целей", active: "ПРОИЗВОДСТВО", center, right, backdrop: "dark" });
}

function researchTree() {
  function node(x, y, name, state = "open") {
    const cls = state === "done" ? "chip" : state === "locked" ? "chipWarn" : "chipBlue";
    return `<g>${rect(x, y, 190, 58, cls, 'rx="10"')}${text(x + 95, y + 36, name, "chipText", 'text-anchor="middle"')}</g>`;
  }
  const center = `${panel(338, 116, 1118, 732, "Древо технологий", `
    ${node(390, 200, "Воздухоплавание", "done")}
    ${node(640, 200, "Аэродинамика", "open")}
    ${node(890, 200, "Математика", "open")}
    ${node(1140, 200, "Насосы", "open")}
    ${line(580, 229, 640, 229, "route")}${line(830, 229, 890, 229, "route")}${line(1080, 229, 1140, 229, "route")}
    ${node(520, 340, "Несущая обшивка", "open")}
    ${node(780, 340, "Пароклавдиевая теория", "open")}
    ${node(1040, 340, "Опорная платформа", "locked")}
    ${line(735, 258, 610, 340, "route routeDim")}${line(980, 258, 875, 340, "route routeDim")}
    ${node(520, 500, "Рудный сборщик", "locked")}
    ${node(780, 500, "Разведка", "locked")}
    ${node(1040, 500, "Охотничий корпус", "locked")}
    ${metric(390, 666, 210, "АКТИВНО", "Математика")}
    ${metric(620, 666, 210, "ЦИКЛОВ", "1 / 3")}
    ${metric(850, 666, 210, "ЦЕНА", "бумага 6")}
    ${button(1100, 674, 240, 58, "ИССЛЕДОВАТЬ", true)}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Математика", `
    ${multiline(1540, 184, "Усиливает генерацию фундаментального опыта, открывает автопилоты, маршрутные таблицы и разведку.", "txt", 34)}
    ${row(1540, 300, 316, "Требования", "опыт 5", .55, "yellow")}
    ${row(1540, 370, 316, "Цикл", "20 сек", .35, "blue")}
    ${row(1540, 440, 316, "Открывает", "Паровоз, маршруты", .75, "green")}
    ${row(1540, 510, 316, "Сырьё", "бумага", .48, "yellow")}
  `)}`;
  return shell({ title: "ИССЛЕДОВАНИЯ", subtitle: "технологии, циклы, стоимость и открываемые системы", active: "ИССЛЕДОВАНИЯ", center, right, backdrop: "dark" });
}

function fleetLogistics() {
  const center = `${panel(338, 116, 1118, 732, "Диспетчер флота", `
    ${row(370, 184, 1038, "Грузовичок 01", "Loading · Island2 · еда→аэролит→столица · ETA 08:12", .64, "green")}
    ${row(370, 264, 1038, "Топливщик", "WaitingForResources · нужен уголь 48 кг", .18, "rust")}
    ${row(370, 344, 1038, "Вахта", "Idle · может закрыть рабочую силу и здоровье", .80, "blue")}
    ${row(370, 424, 1038, "Фургонщик", "Finished · маршрут завершён", .98, "green")}
    ${row(370, 504, 1038, "Стапель", "Locked · нужен field_flying_dock", .05, "yellow")}
    ${mapNetwork()}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Расчёт рейса", `
    ${metric(1540, 184, 142, "ТОПЛИВО", "31 кг")}
    ${metric(1714, 184, 142, "КЛАВДИЙ", "14 кг")}
    ${metric(1540, 284, 142, "ГРУЗ", "32 кг")}
    ${metric(1714, 284, 142, "МАССА", "82%")}
    ${row(1540, 402, 316, "Остановка 1", "загрузить еду x20", .90, "green")}
    ${row(1540, 472, 316, "Остановка 2", "выгрузить, взять аэролит", .44, "yellow")}
    ${row(1540, 542, 316, "Остановка 3", "выгрузить в столицу", .10, "blue")}
    ${button(1540, 748, 316, 58, "ПРИМЕНИТЬ МАРШРУТ", true)}
  `)}`;
  return shell({ title: "ФЛОТ / ЛОГИСТИКА", subtitle: "автономные маршруты, статусы, запасы и ошибки рейса", active: "ФЛОТ", center, right, backdrop: "dark" });
}

function gasHarvesting() {
  const center = `${panel(338, 116, 1118, 732, "Газосбор", `
    ${circle(760, 410, 170, "blue", 'opacity=".25"')}${circle(780, 394, 112, "blue", 'opacity=".35"')}${circle(710, 450, 84, "blue", 'opacity=".28"')}
    ${shipSilhouette(905, 420, .7)}
    ${line(1010, 508, 760, 410, "route")}
    ${text(615, 250, "Туманное облако · condensate 740 кг", "h1")}
    ${row(390, 632, 420, "Gas Harvester 01", "летит к облаку", .56, "green")}
    ${row(840, 632, 420, "Сбор", "цикл 10 сек, цель 35 кг", .32, "blue")}
    ${row(390, 706, 420, "Резерв возврата", "топливо 18, клавдий 7", .74, "green")}
    ${row(840, 706, 420, "Разгрузка", "0.5 сек/кг в столице", .20, "yellow")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Состав облака", `
    ${row(1540, 182, 316, "Вода", "водосбор", .58, "blue")}
    ${row(1540, 252, 316, "Конденсат", "перегонка", .42, "yellow")}
    ${row(1540, 322, 316, "Токсичность", "низкая", .18, "green")}
    ${row(1540, 392, 316, "Засорённость", "средняя", .50, "yellow")}
    ${button(1540, 748, 316, 58, "НАЗНАЧИТЬ СБОР", true)}
  `)}`;
  return shell({ title: "ФЛОТ / ГАЗ", subtitle: "поиск облака, сбор концентрата, резерв на возврат", active: "ФЛОТ", center, right, backdrop: "dark" });
}

function miningOperations() {
  const center = `${panel(338, 116, 1118, 732, "Добыча рудных глыб", `
    ${circle(870, 366, 118, "rust", 'opacity=".38"')}${circle(910, 330, 52, "gold", 'opacity=".8"')}
    ${shipSilhouette(760, 520, .62)}
    ${line(850, 600, 870, 366, "route")}
    ${text(690, 228, "Глыба: сильвин-кальцит · осталось 420 кг", "h1")}
    ${row(390, 632, 420, "Mining Kamaz 01", "ждёт осыпь под глыбой", .48, "yellow")}
    ${row(840, 632, 420, "Ударный трюм", "20 / 60 кг", .33, "rust")}
    ${row(390, 706, 420, "Безопасная высота", "выше штормовой границы", .82, "green")}
    ${row(840, 706, 420, "Возврат", "резерв топлива достигнут через 04:20", .62, "blue")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Паспорт глыбы", `
    ${row(1540, 182, 316, "Руда", "aerolite / calcite", .66, "blue")}
    ${row(1540, 252, 316, "Осыпание", "1.8 кг/мин", .38, "yellow")}
    ${row(1540, 322, 316, "До разрушения", "42 мин", .54, "green")}
    ${row(1540, 392, 316, "Шторм", "граница снизу", .22, "rust")}
    ${button(1540, 748, 316, 58, "НАЗНАЧИТЬ МАЙНЕР", true)}
  `)}`;
  return shell({ title: "ФЛОТ / ДОБЫЧА", subtitle: "рудные глыбы, осыпь, ударный трюм и возврат", active: "ФЛОТ", center, right, backdrop: "dark" });
}

function scoutSurvey() {
  const center = `${panel(338, 116, 1118, 732, "Разведка и сведения", `
    ${mapNetwork()}
    ${circle(1130, 310, 230, "blue", 'opacity=".10"')}${circle(1370, 420, 260, "rust", 'opacity=".12"')}
    ${shipSilhouette(970, 260, .5)}
    ${row(370, 696, 500, "Разведчик 01", "наблюдает туманное облако", .58, "blue")}
    ${row(900, 696, 500, "Бумага", "42 / 60 кг на борту", .70, "yellow")}
    ${row(370, 770, 500, "Информация", "cloud_info 18 кг", .32, "green")}
    ${row(900, 770, 500, "Опасность", "левиафан в 310 м", .46, "rust")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Цель наблюдения", `
    ${row(1540, 182, 316, "Координаты", "открыты", .100, "green")}
    ${row(1540, 252, 316, "Факты", "37 / 45", .82, "blue")}
    ${row(1540, 322, 316, "Инфо-потенциал", "90 кг", .44, "yellow")}
    ${row(1540, 392, 316, "КПД бумаги", "1:1", .90, "green")}
    ${multiline(1540, 500, "Разведка открывает координаты, затем факты для промысловых автопилотов, затем снимает научную информацию в груз.", "txt", 34)}
  `)}`;
  return shell({ title: "РАЗВЕДКА", subtitle: "координаты, факты, бумага и научная информация", active: "РАЗВЕДКА", center, right, backdrop: "dark" });
}

function leviathanHunt() {
  const center = `${panel(338, 116, 1118, 732, "Охота на левиафана", `
    <path d="M760 400 C880 260 1160 300 1210 420 C1120 520 900 560 720 500 C650 476 642 438 760 400 Z" fill="#2b3440" stroke="#b68446" stroke-width="3"/>
    ${circle(1020, 388, 18, "lamp")}
    ${shipSilhouette(560, 582, .7)}
    ${line(700, 650, 880, 465, "route")}
    ${text(710, 250, "Малый штормовой левиафан · масса 1.8 т", "h1")}
    ${row(390, 708, 420, "Гарпун", "трос держит, дистанция 180 м", .64, "green")}
    ${row(840, 708, 420, "Тревога", "растёт от шума и выстрелов", .58, "rust")}
    ${row(390, 782, 420, "Корпус цели", "здоровье 62%", .62, "yellow")}
    ${row(840, 782, 420, "Добыча", "туша, жир, биоматериалы", .30, "blue")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Команды охоты", `
    ${button(1540, 190, 316, 58, "ВЫСТРЕЛ ГАРПУНОМ")}
    ${button(1540, 268, 316, 58, "ВЫСТРЕЛИТЬ")}
    ${button(1540, 346, 316, 58, "ЗАБРАТЬ ТУШУ", true)}
    ${row(1540, 454, 316, "Условие", "цель разведана", .86, "green")}
    ${row(1540, 524, 316, "Масса", "влезает в трюм", .52, "yellow")}
    ${row(1540, 594, 316, "Риск", "таран при тревоге", .44, "rust")}
  `)}`;
  return shell({ title: "ОХОТА", subtitle: "гарпун, тревога, здоровье цели и трофеи", active: "ФЛОТ", center, right, backdrop: "dark" });
}

function encyclopedia() {
  const center = `${panel(338, 116, 1118, 732, "Энциклопедия", `
    ${["Все записи","Обзор","Корабли","Компоненты","Технологии","Ресурсы","Острова","Производства","Рецепты","Мир"].map((v, i) => {
      const x = 370 + (i % 2) * 190; const y = 180 + Math.floor(i / 2) * 58;
      return chip(x, y, v, i === 0 ? "chip" : "chipBlue");
    }).join("")}
    ${row(780, 180, 610, "Несущая обшивка", "Технология · вода и корпус", .80, "blue")}
    ${row(780, 250, 610, "Аэролитовый остров", "Остров · руда", .70, "green")}
    ${row(780, 320, 610, "Кувалда", "Каскад · R3/T2", .46, "yellow")}
    ${row(780, 390, 610, "Туманное облако", "Мир · газовое поле", .58, "blue")}
    ${panel(780, 500, 610, 260, "Детали записи", `
      ${multiline(812, 562, "Энциклопедия показывает не только описание, но и связи: где появляется, что открывает, чем производится, какие корабли и технологии используют запись.", "txt", 58)}
    `)}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Поиск", `
    ${rect(1540, 180, 316, 52, "panelSoft", 'rx="8"')}${text(1560, 213, "аэролит", "txt")}
    ${row(1540, 270, 316, "Найдено", "12 записей", .60, "blue")}
    ${row(1540, 340, 316, "Страница", "1 / 2", .50, "yellow")}
    ${button(1540, 748, 146, 58, "НАЗАД")}${button(1710, 748, 146, 58, "ДАЛЬШЕ")}
  `)}`;
  return shell({ title: "ЭНЦИКЛОПЕДИЯ", subtitle: "категории, поиск, записи и связи контента", active: "ЭНЦИКЛОПЕДИЯ", center, right, backdrop: "dark" });
}

function flagshipConstructor() {
  const decks = Array.from({ length: 4 }, (_, d) => Array.from({ length: 12 }, (_, c) => rect(456 + c * 74, 250 + d * 86, 66, 66, (c + d) % 2 ? "panelSoft" : "row", 'rx="6"')).join("")).join("");
  const rooms = [
    [456, 250, 140, "Мостик", "chipBlue"],
    [752, 250, 214, "Столовая", "chip"],
    [1010, 336, 214, "Ремонтная", "chip"],
    [456, 422, 140, "Склад", "chip"],
    [974, 508, 140, "Двигатели", "chipWarn"]
  ].map(([x, y, w, label, cls]) => `<g>${rect(x, y, w, 66, cls, 'rx="8"')}${text(x + w / 2, y + 40, label, "chipText", 'text-anchor="middle"')}</g>`).join("");
  const center = `${panel(338, 116, 1118, 732, "Конструктор отсеков флагмана", `
    ${decks}${rooms}
    ${text(456, 210, "Палубы универсальные · R3 Горизонт", "h1")}
    ${button(456, 720, 220, 58, "АВТОКОМПЛЕКТ")}${button(700, 720, 220, 58, "СОХРАНИТЬ", true)}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Каталог отсеков", `
    ${row(1540, 180, 316, "Склад", "1 клетка", .60, "green")}
    ${row(1540, 250, 316, "Медпункт", "1 клетка", .45, "blue")}
    ${row(1540, 320, 316, "Столовая", "2 клетки", .70, "yellow")}
    ${row(1540, 390, 316, "Каюты", "2 клетки", .66, "blue")}
    ${row(1540, 460, 316, "Мастерская", "3 клетки", .40, "rust")}
    ${multiline(1540, 570, "Системные отсеки нельзя заменить. Обычные перетаскиваются по сетке и работают в режимах 25/50/100%.", "txt", 34)}
  `)}`;
  return shell({ title: "ФЛАГМАН / ОТСЕКИ", subtitle: "палубы, режимы комнат, поломки и автономность", active: "СБОРКА", center, right, backdrop: "dark" });
}

function expeditionSelection() {
  const center = `${panel(338, 116, 1118, 732, "Экспедиции флагмана", `
    ${row(370, 188, 470, "Тестовая окраина", "R3+ · мораль x1.25", .90, "green")}
    ${row(370, 266, 470, "Штормовая кромка", "R4+ · мораль x1.6", .25, "rust")}
    ${panel(880, 188, 520, 520, "Тестовая окраина", `
      ${metric(912, 254, 150, "ВХОД", "R3+")}
      ${metric(1082, 254, 150, "МОРАЛЬ", "x1.25")}
      ${metric(1252, 254, 110, "СЦЕНА", "World")}
      ${multiline(912, 370, "Первый каркас отдельной экспедиции: старт из столицы, возврат домой и мораль как таймер региона.", "txt", 48)}
      ${button(912, 620, 200, 58, "НАЗАД")}${button(1160, 620, 200, 58, "НАЧАТЬ", true)}
    `)}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Флагман", `
    ${metric(1540, 184, 146, "РАНГ", "R3")}
    ${metric(1710, 184, 146, "МОРАЛЬ", "100%")}
    ${row(1540, 300, 316, "Комнаты", "6 активных", .70, "green")}
    ${row(1540, 370, 316, "Ремонт", "1 слот", .46, "yellow")}
    ${row(1540, 440, 316, "Экипаж", "6 человек", .82, "green")}
  `)}`;
  return shell({ title: "ЭКСПЕДИЦИИ", subtitle: "выбор региона, требования и старт флагмана", active: "КАРТА", center, right, backdrop: "dark" });
}

function expeditionRegion() {
  const center = `${panel(338, 116, 1118, 732, "Регион экспедиции", `
    ${mapNetwork()}
    ${row(370, 690, 500, "Флагман Горизонт", "мораль падает 4/ч · режим экспедиции", .76, "yellow")}
    ${row(900, 690, 500, "Малый флот", "добыча, ремонт, разведка, снабжение", .58, "blue")}
    ${row(370, 764, 500, "Событие", "гироскопический дрейф на мостике", .34, "rust")}
    ${row(900, 764, 500, "Задача", "вернуть разведчика с cloud_info", .48, "green")}
  `)}`;
  const right = `${panel(1508, 116, 384, 732, "Автономность", `
    ${row(1540, 182, 316, "Силы", "need_workforce", .72, "green")}
    ${row(1540, 252, 316, "Здоровье", "need_health", .84, "green")}
    ${row(1540, 322, 316, "Порядок", "need_safety", .66, "yellow")}
    ${row(1540, 392, 316, "Комфорт", "need_comfort", .52, "yellow")}
    ${row(1540, 462, 316, "Фокус", "need_creativity", .58, "blue")}
    ${row(1540, 532, 316, "Ремонт", "need_repair", .40, "rust")}
    ${row(1540, 602, 316, "Мораль", "таймер возврата", .68, "yellow")}
    ${button(1540, 748, 316, 58, "ВЕРНУТЬСЯ В СТОЛИЦУ", true)}
  `)}`;
  return shell({ title: "ЭКСПЕДИЦИЯ / РЕГИОН", subtitle: "флагман в рейде, мораль, комнаты, события и малый флот", active: "ФЛОТ", center, right, backdrop: "dark" });
}

function dialogue() {
  const body = `${cloudSky()}${topBar("ДИАЛОГ", "сюжетная реплика при стыковке", "стыковка")}
    ${shipSilhouette(720, 380, 1.1)}
    <g class="shadow">
      ${rect(70, 740, 1780, 250, "panel", 'rx="12"')}
      ${rect(110, 778, 190, 170, "panelSoft", 'rx="10"')}
      ${text(205, 870, "РЕН", "title", 'text-anchor="middle"')}
      ${text(340, 804, "Рен", "h1")}
      ${multiline(340, 856, "Нам нужен треугольник поставок: ферма, аэролитовый остров и столица. Три стареньких Пионера, один понятный поток товаров.", "txt", 94, 30)}
      ${button(1570, 910, 230, 54, "ДАЛЬШЕ", true)}
    </g>`;
  return svg(body);
}

function pauseMenu() {
  const body = `${cloudSky()}<rect width="${W}" height="${H}" fill="#05070a" opacity=".62"/>
    <g class="shadow">
      ${rect(680, 300, 560, 430, "panel", 'rx="14"')}
      ${text(730, 374, "ПАУЗА", "title")}
      ${button(730, 430, 460, 62, "ПРОДОЛЖИТЬ", true)}
      ${button(730, 514, 460, 62, "СОХРАНИТЬ И ВЫЙТИ")}
      ${button(730, 598, 460, 62, "ВЫЙТИ БЕЗ СОХРАНЕНИЯ")}
      ${text(730, 694, "Игра остановлена, процессы мира сохранятся при выходе.", "small")}
    </g>`;
  return svg(body);
}

const screens = new Map([
  ["01_StartMenu.svg", startMenu()],
  ["02_SaveSlotsSettings.svg", saveSlotsSettings()],
  ["03_CityDock.svg", cityDock()],
  ["04_IslandDock.svg", islandDock()],
  ["05_FlightHud.svg", flightHud()],
  ["06_WorldMapRoutes.svg", worldMapRoutes()],
  ["07_CargoTransfer.svg", cargoTransfer()],
  ["08_HangarCatalog.svg", hangarCatalog()],
  ["09_ShipAssembly.svg", shipAssembly()],
  ["10_Production.svg", production()],
  ["11_ResearchTree.svg", researchTree()],
  ["12_FleetLogistics.svg", fleetLogistics()],
  ["13_GasHarvesting.svg", gasHarvesting()],
  ["14_MiningOperations.svg", miningOperations()],
  ["15_ScoutSurvey.svg", scoutSurvey()],
  ["16_LeviathanHunt.svg", leviathanHunt()],
  ["17_Encyclopedia.svg", encyclopedia()],
  ["18_FlagshipConstructor.svg", flagshipConstructor()],
  ["19_ExpeditionSelection.svg", expeditionSelection()],
  ["20_ExpeditionRegion.svg", expeditionRegion()],
  ["21_Dialogue.svg", dialogue()],
  ["22_PauseMenu.svg", pauseMenu()]
]);

for (const [fileName, content] of screens) {
  fs.writeFileSync(path.join(outDir, fileName), content, "utf8");
}

const indexHtml = `<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Wild Wind UI Windows</title>
  <style>
    :root { color-scheme: dark; font-family: Inter, Arial, sans-serif; background: #0b1118; color: #e9f0ee; }
    body { margin: 0; padding: 28px; background: #0b1118; }
    h1 { margin: 0 0 18px; font-size: 24px; font-weight: 760; letter-spacing: 0; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(420px, 1fr)); gap: 18px; }
    .screen { border: 1px solid rgba(157, 177, 182, .18); background: #121b24; border-radius: 8px; overflow: hidden; }
    .screen h2 { margin: 0; padding: 12px 14px; font-size: 14px; font-weight: 700; color: #c9d5d2; border-bottom: 1px solid rgba(157, 177, 182, .14); }
    .screen img { display: block; width: 100%; height: auto; background: #071018; }
  </style>
</head>
<body>
  <h1>Wild Wind UI Windows</h1>
  <div class="grid">
${[...screens.keys()].map((fileName) => `    <section class="screen">
      <h2>${fileName.replace(".svg", "")}</h2>
      <img src="./${fileName}" alt="${fileName}">
    </section>`).join("\n")}
  </div>
</body>
</html>`;

fs.writeFileSync(path.join(outDir, "index.html"), indexHtml, "utf8");

console.log(`Generated ${screens.size} SVG UI windows and index.html in ${outDir}`);
