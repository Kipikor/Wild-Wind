# Wild Wind: Session Extraction Core

Status: first session-extraction vertical slice implemented in code and covered by big-test checks.
Date: 2026-05-29.
Branch: codex/session-extraction-core.

Design update 2026-06-10:

- sortie launch and return no longer spend stored coal, claudium, fuel, or other external resources;
- the sortie cost is ship wear / operational sortie resource;
- coal/fuel/claudium-related wording should be read as ship autonomy, range, internal endurance, or legacy implementation language unless explicitly stated otherwise;
- the player should not refuel or load fuel before a sortie.

## Core Shift

Wild Wind moves from an island logistics simulator toward a session-based extraction game.

The main loop is:

```text
Base -> choose sortie -> fly in a limited zone -> farm resources -> complete objective -> reach extraction boundary -> home -> spend ship wear -> process -> cascade production -> upgrade -> new sortie
```

The base is the only home dock in the core loop. The player does not freely take off from the base into an endless world. The base is a management layer for storage, fitting, processing, cascade production, repairs, research, and sortie selection.

Inter-island logistics, social needs, passenger traffic, and visible ship compartments are not part of the core loop.

## Размерные классы кораблей

Класс корабля означает размер корпуса, заметность, грузовую емкость и то, как корабль решает задачу в вылете. Это не деление на "боевой" и "небоевой": даже добывающий корабль живет в опасной локации, но разные размеры по-разному могут избегать, принимать или навязывать бой.

Числа ниже - актуальная дизайн-фиксация от 2026-06-13. Это рабочие ориентиры для баланса, а не финальная физическая таблица.

Рабочие размерности:

| Класс | Длина | Радиус заметности | Грузоподъемность | Маневренность | Главный способ игры |
| --- | ---: | ---: | ---: | --- | --- |
| Фрегат | 60-80 м | 3-5 км | около 40 т | Очень высокая | Играет скрытностью: просочился, обошел, ударил торпедой или тихо забрал цель, ушел. |
| Крейсер | 150-200 м | 8-10 км | около 400 т | Средняя, еще мобильная | Играет специализацией: меньше точек, но сложнее цель, выше отдача и лучше вооружение. |
| Линкор | крупный тяжелый класс | около 15 км | около 4000 т | Низкая | Играет маршрутом и силой: заранее проложил проход, воюет по пути, забирает крупную добычу. |

`Грузоподъемность` - это средний паспортный ориентир класса в рабочей боевой конфигурации, а не точное значение для каждого корпуса и не абсолютный физический максимум. Конкретные корабли могут отклоняться от среднего: узкоспециализированные добывающие варианты могут брать больше, но платят за это специализацией, заметностью, скоростью или боевой устойчивостью.

Отдельный линейный крейсер для базового деления не нужен: есть крейсер и есть линкор.

### Звездная эффективность

Эффективность корабля в задачах пока выражается условной шкалой от 1 до 5 звезд. Это не уровень корабля, а то, насколько хорошо конкретный корпус работает в конкретной роли: разведка, добыча руды, добыча газа, охота, взлом, бой, сбор узлов и так далее.

- Фрегат не может получить больше 3 звезд ни в одной специализации. У него маленький функциональный бюджет: если разведка на 3 звезды, то добыча руды может быть максимум на 1 звезду, и еще на что-то останется примерно 1 звезда.
- Крейсер уже может иметь 5-звездочную специализацию в одной роли и несколько посредственных дополнительных ролей.
- Линкор может иметь несколько сильных специализаций одновременно и еще набор средних ролей, но платит за это заметностью, неповоротливостью и обязательным боем.

### Угроза в вылете

В вылет выходит один активный корабль игрока. В локации уже есть враги, цели и видимые события. Типовая локация смешанная: враги + руда, враги + газ, враги + газ + руда и другие похожие комбинации.

Угроза растет от боя и шумных действий. Чем выше угроза, тем сильнее реакция врагов. Скрытность - это не отдельный тип миссии, а ранняя низкошумная фаза того же боевого вылета.

Малый корабль может не вступать в бой сразу. Фрегат способен обойти врагов, тихо забрать ресурс, поставить действие или ударить важную цель торпедой издалека и уйти. Крупный корабль чаще превращает любую локацию в прямое столкновение.

### Радиус заметности

Скорость сама по себе заметность не меняет. Заметность растет от размера корпуса, боя, громких рабочих модулей и других шумных действий.

| Корпус | Рабочий радиус заметности | Что это значит |
| --- | ---: | --- |
| Фрегат | 3-5 км | Может держать тихую фазу и выбирать, когда вступать в контакт. |
| Крейсер | 8-10 км | Прятаться уже трудно, но мобильность еще позволяет выбирать маршрут и дистанцию. |
| Линкор | около 15 км | На текущих размерах карт его почти всегда видят; скрытного режима фактически нет. |

Активные действия фрегата: стрельба, громкие рабочие модули и другие шумные действия. Запуск торпеды остается тихим; взрыв торпеды поднимает угрозу в районе цели.

У линкора нет скрытного режима. Враги всегда знают, что линкор находится в районе вылета.

### Фрегат

Фрегат - небольшой корабль примерно 60-80 м со средней грузоподъемностью около 40 т. Его ценность не в открытом обмене залпами, а в том, что он может не входить в бой сразу.

Фрегат может пройти часть вылета тихо:

- подойти к точке взлома или атаки вне радиуса обнаружения;
- обойти врагов и забрать ресурс из-под носа;
- выпустить тихую торпеду по тяжелой или важной цели;
- уйти до того, как угроза после взрыва догонит его;
- при срыве скрытности выживать скоростью, уклонением и дистанцией.

Фрегат может быть почти безоружным: маленькая пушка, торпеды, сервисные модули, разведка или добычная оснастка. Серьезного тяжелого вооружения он не несет.

Провал фрегата - быть замедленным или зажатым. Магнитные замедлители и близкие перехватчики опасны именно потому, что забирают его скорость.

Специализация фрегата узкая. Он не может быть абсолютным мастером на 5 звезд: слишком много корпуса уходит в ход, уклонение, скрытность и маневр. Потолок фрегата - 3 звезды в одной роли и несколько слабых добавок.

### Крейсер

Крейсер - корабль примерно 150-200 м со средней грузоподъемностью около 400 т. Он уже заметен на 8-10 км, поэтому ему намного труднее играть в невидимость, но он все еще достаточно маневренный, чтобы выбирать маршрут, дистанцию и порядок целей.

Крейсер не должен быть просто серединой между фрегатом и линкором. Его смысл: он может стать настоящим мастером одной задачи. Он не объедет столько точек, сколько фрегат, но может взять более сложную цель и вытащить из нее больше.

Крейсер уже значительно лучше вооружен и крепче фрегата. У него больше прочности, больше места под защиту и больше способов пережить ошибку, но скрытная фаза у него короче и рискованнее.

Пример со взломом:

- фрегат быстрее обходит много простых точек;
- крейсер медленнее перемещается между точками, но быстрее ломает сложные замки;
- крейсер может брать замки выше уровнем и доставать больше данных из одной точки;
- линкор взламывает примерно на уровне хорошего крейсера, но ему трудно ездить по множеству точек.

Специализация крейсера: одна сильная 5-звездочная роль и несколько посредственных дополнительных ролей. Если крейсер стал сильным добытчиком руды, разведчиком или взломщиком, это его основной выбор, а не бесплатная добавка ко всему. Обычно рядом остаются 3-4 средние или слабые специализации.

### Линкор

Линкор - тяжелый крупный класс со средней грузоподъемностью около 4000 т. Радиус заметности около 15 км делает его практически всегда видимым на текущих размерах карт.

Линкор входит в вылет уже видимым и уже провоцирует эскалацию. Он не может по-настоящему избежать боя: если в локации есть враги, линкор почти наверняка будет сражаться.

Его базовый цикл:

```text
проложил маршрут -> вошел видимым -> держит эскалацию -> работает под огнем -> выходит по плану
```

Сильные стороны линкора:

- огромная живучесть;
- встроенный ремонт как базовая линкорная особенность;
- главный калибр, опасный почти любой цели;
- много автоматического ПМК, где фрегатский калибр становится вторичным оружием линкора;
- хорошая боеспособность даже у не полностью боевых линкоров;
- большой груз и широкая миссионная емкость.

Линкор всегда бронирован и всегда вооружен. Если фрегат может быть почти безоружной тихой машиной, то линкор всегда остается крепкой и опасной платформой.

Взлом линкора - это боевой взлом. Он работает рядом с целью, пока ПМК отстреливает угрозы. Легкий вес информационных ресурсов не решает его главную проблему: линкору тяжело быстро ездить между многими точками.

Специализация линкора широкая: несколько сильных способностей и несколько посредственных. Линкор может быть отличным добытчиком газа и отличным добытчиком руды одновременно, а остальные задачи закрывать средне. Добывающий линкор все равно хорошо дерется; дорогой поздний линкор может быть одновременно сильным добытчиком, сильным сборщиком и сильным боевым кораблем.

Плата линкора - маневренность. Он не бегает между множеством точек. Ему нужен заранее понятный маршрут: одна точка, вторая точка, выход.

### Способы выживания

Способы выживания не привязаны жестко к одному размерному классу. Разные корпуса и поставщики комбинируют их по-своему.

| Способ | Как работает | Дизайн-смысл |
| --- | --- | --- |
| Броня | Внешний слой защиты. Сильно снижает фугасный урон; бронебойный снаряд может не пробить броню или уйти в рикошет. | Защищает от поверхностного урона и делает направление/угол попадания важными. |
| Запас прочности | Внутренняя живучесть корпуса: корабль может впитать много урона даже без сильной брони. | Позволяет делать большие "мягкие" корабли, которые живут объемом, а не бронепоясом. |
| Цитадель | Уязвимая внутренняя зона. Мощный бронебойный снаряд, добравшийся до цитадели, наносит огромный урон. | Не дает одной прочности решать все: плохо спрятанная цитадель делает корабль смертельно уязвимым. |
| Ремонт | Корабль может восстанавливать часть повреждений в бою или после боя, по логике World of Warships. | Дает вторую линию выживания и ценность правильному таймингу, а не только толщине брони. |

Броня и прочность - разные параметры. Корабль может быть бронированным, но не очень объемным; может быть с огромным запасом прочности, но с плохой броней; может хорошо ремонтироваться, но страдать от открытой цитадели.

### Задачи по размерностям

| Задача | Фрегат | Крейсер | Линкор |
| --- | --- | --- | --- |
| Прямой бой | Не любит долгую прямую драку; держится скоростью, уклонением, торпедами и выбором момента. | Зависит от специализации: может быть почти небоевым на уровне фрегата или крепким боевым кораблем. | Почти всегда крепкий боевик: держит удар, стреляет главным калибром и ПМК, хорошо живет в эскалации. |
| Разведка и сканирование | Силен за счет близкого тихого подхода; тихий скан чаще всего не увеличивает заметность. | Может быть тихим разведчиком или мощной подсветкой; сильный скан поднимает заметность до активного радиуса. | Всегда мощный скан без скрытности; дальний скан слабее из-за дистанции, близкий скан очень сильный. |
| Взлом | Обходит много простых точек. | Берет меньше точек, но сложнее замки и больше данных с точки. | Взламывает примерно как хороший крейсер, но плохо ездит между точками. |
| Газ | Берет образцы и малые объемы; около 40 т быстро забиваются концентратом. | Нормальная добыча: около 400 т уже дают заметный объем. | Массовая добыча: около 4000 т, место под тяжелое оборудование и сепарацию. |
| Руда | Берет малые партии ценной руды и может забрать их без боя. | Нормальная промышленная добыча с возможной 5-звездочной специализацией. | Массовая добыча и вывоз тяжелой руды, часто сразу с сильной боевой защитой. |
| Левиафаны | Плохой самостоятельный охотник: обычный левиафан крепкий, бронированный и движется; чаще надо уходить или пытаться торпедировать. | Хороший базовый охотник: может драться с обычным левиафаном почти на равных. | Тяжелый доминатор: для большинства левиафанов сам становится главной угрозой, кроме самых огромных особей. |
| Узлы автоматонов | Охотится за мелкими дорогими узлами с высокой ценностью на тонну. | Снимает больше узлов и лучше работает со сложными крупными обломками. | Может снять очень много и очень качественно, но ограничен маршрутом и не бегает за каждой мелочью. |

В газе, руде и левиафанах размер прежде всего дает объем вывоза и возможность поставить тяжелое оборудование. Фрегат может быть полезен, но чаще как разведчик, доборщик образцов или точечный сборщик дорогой мелочи.

Полевой salvage и базовая разборка автоматонов - разные этапы.

- В вылете корабль не увозит целого врага своего размера; он снимает узлы с островов, обломков и крупных автоматонов.
- Узлы: ядра, сенсоры, сервоприводы, оружейные блоки, память, редкие регуляторы и другие ценные сборки.
- Малые узлы лучше по ценности на тонну; фрегат охотится за ними, потому что быстро облетает много мест.
- Большие узлы тяжелее и хуже по концентрации ценности, но суммарно дороже; крейсер и линкор могут брать их нормально.
- На базе разборка автоматонов перерабатывает привезенные узлы в материалы, механизмы, инструменты, ядра и опыт.
- Вылет решает, что игрок сумел снять под угрозой; база решает, во что это будет разобрано.

Сканирование работает как свет: чем дальше цель, тем слабее результат. Близкий тихий подход фрегата поэтому ценен сам по себе, а мощный скан крейсера или линкора является заметным действием.

Постановка маяков, зарядов, датчиков, ловушек и временных устройств пока не считается отдельной core-активностью карты. Выход из района тоже не отдельная специализация, а общая обязательная часть каждого вылета.

### Боевые почерки поставщиков

Поставщик не означает узкую профессию корабля. Любой поставщик может иметь боевые, добычные, разведывательные и взломные корпуса. Поставщик задает боевое правило: как корабль стреляет, держит дистанцию, маскируется, сканирует, танкует или входит в пик эффективности.

Рабочие решения:

| Поставщик | Боевой почерк |
| --- | --- |
| Аэролит | Мобильность: быстрый ход, хорошее ускорение, маневр, смена дистанции и выход из плохой позиции. Плата - пониженный боевой ресурсный трюм. |
| Горизонт | Дальность и точность: стрельба, взлом, рабочие модули, захват и другие действия получают увеличенную дистанцию применения; дальний бой и дальняя подсветка сильнее обычного. |
| Каптаж | Искусственные облака, дымовая маскировка, поджоги и более активное использование дронов. Корабли создают окна видимости, сбрасывают захват и работают из-за облака; их снаряды лучше вызывают пожар. |
| Бастион | Хорошая бронекомпоновка, наклонная броня, сильное ПМК. Главный калибр мощный, но с большой перезарядкой, поэтому корабль часто живет вторичкой. |
| Старатели | Сплошные снаряды и орудия-инструменты: хороший пробой брони, корки руды и твердых оболочек, но пониженный разрывной урон. |
| Архивариус | Сильные сканеры, скрытность и торпеды. Дальний и точный скан быстрее раскрывает информацию, слабые места и дает подсказки для стрельбы. Даже крупные корпуса могут иметь скрытое сканирование. |
| Верфь-17 | Пики эффективности: заряжаемые режимы или боевые инструкции, которые временно сильно улучшают корабль, например скорострельность, ход, ремонт или работу модулей. |
| Лагуна | Магнитный захват и химические снаряды. Хорошо замедляет, удерживает цель и накладывает коррозию вместо обычного пожара. Без регена как поставщицкой особенности. |

Аэролит и Горизонт не одно и то же. Аэролит про мобильность и смену дистанции. Горизонт про дальность и точность применения систем.

Дроны есть в игре как общий инструмент, но Каптаж использует их активнее остальных: для работы из облака, подсветки, сброса захвата, отвлечения и поддержки модулей.

## Sortie Zone

A sortie happens inside a limited cylindrical play area.

- Default radius: 5 km for the first implementation.
- Horizontal boundary: circular extraction ring.
- Vertical top: unrestricted for now.
- Bottom: storm layer remains dangerous.
- The player farms inside the cylinder.
- Extraction is only possible near the circular boundary.
- Runtime behavior does not physically clamp the ship against the horizontal ring; crossing it counts as reaching/passing the extraction threshold.
- The flight compass shows the base/exit side during an active sortie as a `BASE SLIP 12s` marker. The marker points outward from the sortie center through the ship, which is the direction the ship must move while outside the cylinder.

The sortie is not infinite in every direction. The boundary is part of the play: after completing the objective, the player must reach the extraction side and hold the exit conditions to return home.

## Return Home

Returning home is a calculated extraction, not a free button.

The ship must be near the sortie boundary after the sortie objective is complete. Return is gated by position, sortie state, ship control state, and ship wear/validity, not by stored fuel reserves.

Return uses the ship's march engines, not the direct player-control engines.

March engines are strategic cruise drives:

- they are very efficient over long distances;
- they need a long stable claudium-slipstream alignment before handoff;
- the player cannot use them for manual flight, combat, mining, catching fragments, or hazard dodging;
- they activate only after the ship leaves the active cylinder and holds claudium slipstream on the base vector, then take over for off-screen travel such as extraction home, travel to a distant sortie theater, or late expedition relocation;
- they let even the R0 fallback handle roughly 200 km strategic return, while large expedition ships can cross about 5000 km in one to two days.

Activation rule:

1. The ship must be at or outside the circular sortie cylinder edge and try to leave the active mission area.
2. The ship must be above the storm layer.
3. The sortie objective must be complete, unless this is a special failure/evacuation rule.
4. Claudium slipstream must be active.
5. The ship's horizontal movement or nose direction must stay within `15 degrees` of the base/outward vector, so the compass marker is usable as the exit aim.
6. The ship must maintain that slipstream exit condition for `12 seconds`.

Claudium slipstream uses a relative speed threshold: by default the ship must reach `80%` of its clean base ход. If it slows below that threshold, the mode shuts off. It ramps the ship's available maximum ход over `20 seconds`; it no longer changes aerodynamic drag or claudium burn.

When the activation timer completes, the manual sortie ends and the march-engine extraction handoff begins. If the ship stops moving toward home, re-enters the mission area, disables claudium slipstream, enters the storm, or loses the required sortie state, the timer resets.

Return calculation uses ship stats, not stored fuel:

- distance to base, for example 220 km or 1000 km;
- estimated march cruise speed;
- estimated return time;
- ship autonomy/range profile;
- loaded mass and cargo risk;
- ship wear/operational state.

Example:

```text
Distance to base: 220 km
Estimated speed: 35 m/s
Return time: 104 min
Ship autonomy: sufficient
Operational resource: 8/10 sorties
Extraction possible.
```

If extraction is blocked, the UI must explain the actual state problem: objective incomplete, wrong boundary position, storm layer, interrupted slipstream, invalid/lost ship, or another non-fuel rule.

On successful extraction:

- the completed sortie spends ship wear / operational resource;
- sortie cargo transfers to base storage;
- the game returns to base mode;
- progress is saved.

## Failure

If the ship is destroyed during a sortie:

- the player returns to base;
- all extracted resources from the sortie are lost;
- the active ship, its installed rigs, active loadout state, and cargo are destroyed or marked lost;
- the player is never soft-locked.

The fallback ship is the Pioneer.

## Pioneer Fallback

Pioneer is the reserve airfield of the game economy.

- It is weak.
- It is free.
- It is always available when the player has no usable ship.
- It does not need a free fuel/refuel step.
- In core mode, if a save points at a missing or invalid hull, the base treats it as a Pioneer fallback case and restores the starter hull.
- It can always attempt safe starter sorties.
The Pioneer keeps failure meaningful without forcing a full restart.

## Starter Resource Loop

The first safe sortie is not abstract cargo loading. It uses falling ore from resource boulders.

Current runtime rule:

- the safe ore sortie spawns overhead boulders inside the sortie cylinder;
- boulders periodically shed physical ore fragments;
- the player catches falling fragments by positioning the ship under them;
- starter safe ore can be caught by ordinary cargo space, while later impact mining modules can remain useful for heavier or dangerous fragments.

The player:

1. Flies to a safe ore field.
2. Holds the ship under a shedding ore boulder.
3. Catches falling fragments in the cargo hold.
4. Watches ship autonomy, mass, cargo capacity, and wear state.
5. Reaches the boundary.
6. Extracts home if the objective and exit-state rules are satisfied.
7. Processes ore at base.
8. Uses processed materials for upgrades.

This makes the first loop about positioning, mass, resources, and extraction timing.

## Ship Autonomy

Coal and claudium are no longer external sortie consumables.

They remain useful as ship design language:

- coal/fuel can describe a ship's autonomy, range, or endurance profile;
- claudium can describe lift, storm safety, slipstream stability, or special construction requirements;
- neither should require player-facing pre-sortie refueling;
- neither should be consumed from base storage by launching or returning from a sortie.

The player pays for completed sorties through ship wear / operational resource.

## Ship Loadout

Visible compartments and High/Mid/Low fitting bands are removed from the active design direction.

Ships use a simpler loadout model:

- rigs: permanent/passive ship modifications, shown as 7 slots in the dock;
- economic consumables: one-sortie reward boosters;
- field/combat consumables: one-sortie tactical tools;
- perks: a commander/player build with limited points and active slots.

Ship visuals do not need to reflect every installed rig or active perk. Loadout affects stats, actions, risk, and reward preview.

Current design rule:

- fixed infrastructure slots such as maneuver engine, march engine, propeller, and claudium loop may remain required ship internals;
- these internals are not player-facing High/Mid/Low equipment slots;
- activity capability should come from ship type, rigs, perks, and maybe later dedicated systems, not from High/Mid/Low module bands;
- legacy `utility`, High, Mid, and Low slot data may remain in old catalog/setup assets for non-core compatibility, but active dock UI ignores those slots;
- the active player-facing slot UI is rigs, economic consumables, field consumables, and perks.
- the old free hull selector is blocked in core mode; ship replacement belongs to Pioneer fallback and base assembly/cascade systems instead of legacy catalog picking.

## Removed From Core Loop

These systems may remain as experiments or future layers, but they are not required for the first playable core:

- social needs;
- passenger traffic;
- inter-island logistics;
- local island supply chains;
- visible ship compartments;
- deck-grid construction;
- city-like base needs.

Current runtime rule:

- `MetaGameState.sessionExtractionCoreMode` is on by default;
- this mode syncs into `PlayerProgress.sessionExtractionCoreMode`;
- new saves and default gameplay sessions start docked at `capital`;
- legacy free flight and legacy flight missions are blocked while core mode is active; flight begins through sorties;
- the old starter food/aerolite delivery seed is not created for core-mode new games;
- core runtime spawns only the base/capital dock from config islands and suppresses legacy free-world gas clouds, mining rocks, and leviathans; sortie resources are spawned by sortie controllers instead;
- legacy island production/consumption, island social needs, passenger traffic, inter-island logistics, autonomous scout/gas/mining fleets, ambient mining world spawning, and direct flagship social/expedition APIs do not advance in the core runtime;
- public legacy flagship expedition return is also blocked in core mode and clears stale expedition state instead of teleporting the player to a legacy dock;
- starting a core sortie clears stale legacy flagship expedition state and does not auto-start old expedition gameplay;
- legacy dock timed processes such as idle mining, iron smelting, and timed missions are blocked in core mode so resources enter through sorties, base processing, and cascade production;
- stale legacy timed-process jobs loaded from older saves are stopped in core mode without paying old resources, money, experience, or mission completion;
- legacy flight mission start/completion rewards are blocked in core mode, so old mission routes cannot bypass sortie extraction;
- legacy XP research and money purchases in the old tech tree are blocked in core mode; unlocks come from base resource technology cycles and cascade production;
- legacy money, direct resource, and ship-XP rewards are blocked in core mode; research output is represented by base resources such as `fundamental_experience` and `design_experience`;
- runtime cargo collection in core mode is accepted only during an active sortie and only for that sortie's configured resource; outside a sortie, cargo can enter the base through extraction, processing, and cascade flows rather than hidden ship-cargo injection;
- legacy personal inventory is migrated into base storage in core mode, and new core starts do not seed the old personal ore/iron inventory or starting paper;
- legacy shop refresh seed/timer is disabled in core mode;
- legacy island cargo loading, unloading, and timed cargo-transfer jobs are blocked in core mode; ship cargo enters the loop through sortie collection and returns through extraction;
- normal docking is restricted to the base in core mode; non-base legacy `DockAt` calls are blocked, and an active sortie can end through boundary extraction or ship loss, not through legacy docking;
- the legacy simulators remain available for old tests and experiments when core mode is disabled;
- the old docked debug HUD that showed base processing inputs, materials, tanks, processing/cascade summaries, Refuel, Next sortie, Start selected sortie, Run cascade, Port, and Menu is quarantined and hidden by default; those runtime actions may remain as internal methods/test hooks until the new island-port UI exposes only the needed actions through buildings, docks, windows, and bubbles;
- the old HUD dock action is replaced by boundary extraction while a core sortie is active, and is enabled only when sortie completion and exit-state rules allow extraction;
- the debug dock UI also hides the legacy assembly/compartment panel and foregrounds sortie launch, rigs/loadout, base storage, base processing, cascade production, and upgrades instead of legacy free flight and island-city controls.

## Base

The base is a progression machine, not a social city sim.

It contains:

- storage;
- hangar and fitting;
- repairs;
- processing;
- cascade production;
- research and recipe unlocks;
- sortie selection.

Current runtime rule:

- the base has an explicit `BaseExtractionIndustryState`;
- it always contains the five processing branches and eight cascade production types;
- the player can select among five starter sortie zones: ore boulders, gas condensate, automaton wrecks, leviathan remains, and survey ruins;
- each starter sortie is a bounded 5 km radius cylinder for the first implementation, with its own entry point, primary processing branch, and starter resource item;
- the first starter sortie catalog currently represents a close safe theater; autonomy/range is a ship characteristic, not a stored fuel gate;
- non-ore starter sorties should be gated by ship role, rigs, perks, activity ratings, and mission rules, not by High/Mid/Low module bands;
- starter resource caches shed collectible fragments in non-ore starter sorties, while the safe ore sortie keeps the overhead boulder behavior;
- ore processing consumes extracted ore from base storage and outputs minerals from `Ore_type.csv`;
- gas processing consumes cloud condensates and outputs gas/material fractions from `Gas_cloud_type.csv`;
- automaton dismantling consumes `broken_automaton` salvage and outputs mechanisms, tools, automaton cores, and design experience;
- leviathan processing consumes carcasses and outputs configured butchery materials: meat, fat, hide, mineral shell, direct claudium, ichor, leviathan sinew, nerve substrate, and bone plates;
- cybernetic deciphering consumes `rock_info`, `cloud_info`, or `leviathan_info` and outputs research experience;
- starter cascade orders are represented as production orders with inputs, outputs, load per production type, estimated bottleneck, and estimated time;
- the starter catalog currently includes legacy airframe/module/munition orders; active design should translate these into ship improvements, rigs, or loadout support rather than High/Mid/Low modules;
- the base overview exposes all five processing branches, all eight cascade production lines, and the next cascade order/bottleneck to make the home layer read as the main progression machine;
- base processing and cascade production lines have levels and upgradeable capacity; upgrades spend processed base materials such as ferron, silvate, and charcoal, then raise the line level and throughput;
- the starter airframe order consumes minerals and charcoal, produces `airframe_kit`, and records load against all eight production types.
- the legacy starter module order should be replaced or reinterpreted as starter rigs/loadout unlocks for gas, automaton salvage, leviathan, and survey branches.
- the starter munition bundle can remain as an industrial output only if it supports the combat loop without becoming a pre-sortie loading gate.
- the old Low-slot cargo rack upgrade is superseded by ship stats, rigs, or hull improvements.

## Five Processing Branches

Processing branches turn extracted raw resources into usable industrial inputs.

1. Ore processing.
2. Gas processing.
3. Automaton dismantling.
4. Leviathan processing.
5. Cybernetic deciphering of information.

Processing is a flow layer. It feeds storage and cascade production.

## Eight Cascade Production Types

Cascade production uses production orders and production capacities, not hand-run micro-recipes.

1. Construction production
   - Beams, panels, frames, armor plates, hull structures, cargo holds, tanks.

2. Metallurgical production
   - Alloys, treated metals, aerolite materials, wire, billets, metal stock.

3. Mechanical production
   - Drives, reducers, bearings, engines, pumps, drills, winches, harpoon mechanisms.

4. Instrumentation production
   - Scanners, radars, communication systems, navigation, autopilots, sensors.

5. Chemical reactor production
   - Polymers, lubricants, liquids, reagents, fuels, adhesives.

6. Automaton production
   - Automatons, logic blocks, servo cores, autonomous work nodes.

7. Electrical production
   - Wiring, generators, batteries, coils, power circuits, electric motors.

8. Assembly production
   - Final assembly of ships, modules, rigs, base installations, ammunition, and upgrades.

## Cascade Principle

The player chooses a major goal, not every intermediate item.

Examples:

- build a ship;
- build a module;
- build a rig;
- upgrade a base installation;
- assemble an expedition-capable component.

The cascade planner unfolds the recipe tree, checks storage, estimates required production load, time, missing materials, and bottlenecks.

Current runtime rule:

- `CascadeProductionOrderDefinition` stores order inputs, outputs, and load per production type;
- the base estimates whether an order can run from storage;
- the estimate exposes the bottleneck production type and approximate time;
- core runtime accepts only cascade orders from the base production catalog; ad hoc public orders are rejected instead of becoming direct resource rewards;
- running an order spends inputs, adds outputs, and records load on each production line.

The main question is:

```text
Which production capacity is the bottleneck?
```

Not:

```text
Which tiny component should the player craft manually next?
```

## First Vertical Slice

The first implementation target is:

1. Base screen/state with storage, rigs/loadout actions, ship wear state, and safe sortie launch.
2. Starter sortie catalog for ore, gas, automaton, leviathan, and survey/info branches.
3. Bounded sortie cylinders with falling starter resources.
4. Ship cargo collection.
5. Ship wear / operational resource spent by completed sorties.
6. Boundary extraction with slipstream handoff and no external fuel reserve gate.
7. Cargo transfer to base storage.
8. Ore processing into early material.
9. Starter rig/loadout upgrades using cascade output.
10. Big test coverage for the full loop and all five processing branches.

This is the new playable spine.

Current verification rule:

The verification list should keep the useful vertical-slice coverage, but update any old fuel/refuel reserve checks and legacy fitting-band checks to the active design.

- the big test contains a `Session extraction core` section;
- it verifies base start, fresh core seed cleanup, legacy-mode blocking, save/job cleanup, dock interaction blocking during active sorties, base home overview for all five processing branches, cascade production outputs, sortie catalog coverage, HUD sortie cycling, launch gating, ship-cargo collection, cargo return, processing branches, sortie-loss recovery through the Pioneer fallback, missing-hull restoration through the Pioneer fallback, and starter rig/loadout upgrades from cascade output;
- it must not require external coal, claudium, fuel, refuel, or reserve spending to start or finish a sortie;
- it should gate sorties through ship wear, ship role, range/autonomy stats, rigs, economic consumables, field consumables, perks, activity ratings, and mission rules;
- it should not verify legacy utility slots or wrong-band module installs as the active player-facing model.
