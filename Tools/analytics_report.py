"""Сводка выгрузки Poki (Analytics/v<версия>/v<версия>_*.csv) и сравнение с прошлой версией.

Запуск: python -I Tools/analytics_report.py <версия> [<прошлая версия>]   (версия без "v", например 0.2.0)
Печатает Markdown: воронку по раундам (переход, победы, поражения, уходы посреди раунда), исходы, места и нокауты
в 1-м раунде, время на арене, причины выбывания, ускорения, управление, прогресс, гараж и выбор машинок; для сборок
с долгим матчем ещё воронку матчей, места, нокауты, падения и REVENGE по матчу.
"""
import csv
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent / "Analytics"


def load(version):
    data = {}
    for name in ("progress", "other", "interaction"):
        path = ROOT / f"v{version}" / f"v{version}_{name}-events.csv"
        if not path.exists():
            # Poki не всегда отдаёт все три выгрузки (на Player Fit Test только progress)
            data[name] = {}
            continue
        with path.open(newline="", encoding="utf-8") as f:
            data[name] = {row["event"]: row for row in csv.DictReader(f)}
    return data


def num(row, key):
    return int(row[key]) if row and row.get(key) else 0


def pct(a, b):
    return f"{100 * a / b:.0f}%" if b else "-"


def rounds(progress, category="round"):
    out = {}
    for event, row in progress.items():
        m = re.fullmatch(category + r"-(\d+(?:plus)?)", event)
        if m:
            out[m.group(1)] = (num(row, "starts"), num(row, "completes"), num(row, "fails"), num(row, "lefts"))
    return out


def other(o, pattern):
    """События other-events по шаблону с одной группой: {значение группы: gameplays}."""
    out = {}
    for event, row in o.items():
        m = re.fullmatch(pattern, event)
        if m:
            out[m.group(1)] = num(row, "gameplays")
    return out


def summarise(data):
    p, o, i = data["progress"], data["other"], data["interaction"]
    loading = p.get("game-loading")
    s = {
        "loads": (num(loading, "starts"), num(loading, "completes")),
        "rounds": rounds(p),
        "outcome": other(o, r"result-(won|out|survived)-outcome"),
        "cause": other(o, r"result-(pushed|fell)-out-cause"),
        "fall_cause": other(o, r"result-(fell-[a-z]+-r[0-9a-z]+)-fall-cause"),
        "revenge": other(o, r"result-(revenge-[a-z]+-r[0-9a-z]+)-revenge"),
        "time": other(o, r"result-(\d+-\d+)-time"),
        "boosts": other(o, r"result-(boosts-[a-z0-9-]+?)-boosts"),
        "input": other(o, r"controls-([a-z]+)-input"),
        "boost_input": other(o, r"controls-boost-(button|tap|finger|key|mouse|gamepad|none)-boost-input"),
        "first_move": other(o, r"controls-(move-[a-z0-9-]+|never)-first-move"),
        "place1": other(o, r"result-place-(\d+)-r1-place"),
        "ko1": other(o, r"result-knockouts-(\d+)-r1-knockouts"),
        "again": i.get("result-play-again"),
        "garage": i.get("garage-button"),
        "lobby": i.get("lobby-shown"),
        # после v0.5.1: NEXT MATCH по исходу и раунду, медленное лобби по арене, раунды по варианту арены
        "again_by": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"result-again-([a-z]+-r(?:1|2|3|4plus))", e))},
        "slow": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"lobby-slow-([a-z]+)", e))},
        "arenas": {m.group(1): row for e, row in p.items() if (m := re.fullmatch(r"arena-([a-z]+-[a-z]+)", e))},
        "scoreboard": i.get("scoreboard-shown"),
        "levels": other(o, r"progress-level-(2|3|4|5|6|7|8|9|10|10plus)-level-up"),
        "unlocks": other(o, r"progress-(?!map-)([a-z]+(?:-[a-z]+)*)-unlock"),
        "map_unlocks": other(o, r"progress-map-([a-z]+)-unlock"),
        "maps": other(o, r"result-map-([a-z]+-r(?:1|2|3|4plus))-map"),
        "variants": other(o, r"result-variant-([a-z]+-[a-z]+-r(?:1|2|3|4plus))-map-variant"),
        "equips": other(o, r"garage-([a-z]+(?:-[a-z]+)*)-equip"),
        "driven": other(o, r"result-car-([a-z]+(?:-[a-z]+)*-r(?:1|2|3|4plus))-car"),
        "colors": other(o, r"garage-color-(green|red|blue|yellow|purple|orange|pink|dark)-color"),
        "played_colors": other(o, r"result-color-((?:green|red|blue|yellow|purple|orange|pink|dark)-r(?:1|2|3|4plus))-color"),
        "progress": p,
    }
    # с v0.2: на раунд (R = 1, 2, 3, 4plus)
    s["cars"] = other(o, r"result-cars-(\d+-r[0-9a-z]+)-cars")
    s["round_time"] = other(o, r"result-((?:early|mid|late|full)-r[0-9a-z]+)-round-time")
    # долгий матч (режим Match): свои старты и итоги вместо round / result
    s["matches"] = rounds(p, "match")
    s["match_place"] = other(o, r"match-place-(\d+-r[0-9a-z]+)-place")
    s["match_knockouts"] = other(o, r"match-knockouts-([0-9a-z]+-r[0-9a-z]+)-knockouts")
    s["match_falls"] = other(o, r"match-falls-([0-9a-z]+-r[0-9a-z]+)-falls")
    s["match_pushed"] = other(o, r"match-pushed-([0-9a-z]+-r[0-9a-z]+)-pushed")
    s["match_revenge"] = other(o, r"match-revenge-((?:got|missed)-r[0-9a-z]+)-revenge")
    # способности матча до ящиков (v0.6): что подобрал игрок (kind-rR)
    s["match_pickup"] = other(o, r"match-pickup-((?:heavy|shockwave|shield|turbo)-r[0-9a-z]+)-pickup")
    # ящики матча: сколько разбил за матч (N-rR), какой предмет выпал (item-get) и какой применил (item-use)
    s["match_pickups"] = other(o, r"match-pickups-([0-9a-z]+-r[0-9a-z]+)-pickups")
    s["match_item_get"] = other(o, r"match-item-((?:heavy|shockwave|shield|turbo|bomb|glove)-r[0-9a-z]+)-item-get")
    s["match_item_use"] = other(o, r"match-item-((?:heavy|shockwave|shield|turbo|bomb|glove)-r[0-9a-z]+)-item-use")
    return s


def table(title, cur, old, version, previous, keys, fmt=None):
    head = f"| | {version} |" + (f" {previous} |" if old else "")
    sep = "|---|---|" + ("---|" if old else "")
    out = ["", f"## {title}", "", head, sep]
    for key in keys:
        row = f"| {key} | {fmt(cur, key)} |"
        if old:
            row += f" {fmt(old, key)} |"
        out.append(row)
    return out


def report(version, previous):
    cur = summarise(load(version))
    old = summarise(load(previous)) if previous else None
    head = f"| | {version} |" + (f" {previous} |" if old else "")
    sep = "|---|---|" + ("---|" if old else "")
    out = [f"# Аналитика v{version}" + (f" vs v{previous}" if old else "")]

    def line(label, fn):
        return f"| {label} | {fn(cur)} |" + (f" {fn(old)} |" if old else "")

    out += ["", "## Загрузка и раунд 1", "", head, sep]
    out.append(line("Начали грузиться / загрузились", lambda s: f"{s['loads'][0]} / {s['loads'][1]} ({pct(s['loads'][1], s['loads'][0])})"))
    r1 = lambda s: s["rounds"].get("1", (0, 0, 0, 0))
    out.append(line("Первый ввод (round-1 start), от загрузки", lambda s: f"{r1(s)[0]} ({pct(r1(s)[0], s['loads'][1])})"))
    out.append(line("Победы в 1-м раунде", lambda s: f"{r1(s)[1]} ({pct(r1(s)[1], r1(s)[0])})"))
    out.append(line("Поражения в 1-м раунде", lambda s: f"{r1(s)[2]} ({pct(r1(s)[2], r1(s)[0])})"))
    out.append(line("Ушли посреди 1-го раунда", lambda s: f"{r1(s)[3]} ({pct(r1(s)[3], r1(s)[0])})"))
    r2 = lambda s: s["rounds"].get("2", (0, 0, 0, 0))
    out.append(line("Дошли до 2-го раунда, от 1-го", lambda s: pct(r2(s)[0], r1(s)[0])))

    names = sorted({k for s in (cur, old) if s for k in s["rounds"]}, key=lambda k: (k.endswith("plus"), int(re.sub(r"\D", "", k))))
    out += ["", "## Воронка по раундам (старты / победы / поражения / ушли; далее = доля, перешедшая в следующий)", "", head, sep]

    def step(s, name):
        r = s["rounds"].get(name)
        if not r:
            return "-"
        nxt = s["rounds"].get(str(int(name) + 1)) if name.isdigit() else None
        go = f"; далее {pct(nxt[0], r[0])}" if nxt else ""
        return f"{r[0]} / {r[1]} / {r[2]} / {r[3]}{go}"

    for name in names:
        out.append(line(f"round-{name}", lambda s, n=name: step(s, n)))

    # total_* считает раунды, а не визиты: в round-10plus у одного визита много раундов
    wins = lambda s: sum(num(row, "total_completes") for e, row in s["progress"].items() if e.startswith("round-"))
    starts = lambda s: sum(num(row, "total_starts") for e, row in s["progress"].items() if e.startswith("round-"))
    out += ["", "## По всем раундам", "", head, sep]
    out.append(line("Победы / раунды", lambda s: f"{wins(s)} / {starts(s)} ({pct(wins(s), starts(s))})"))

    # Долгий матч всегда доигрывается до конца времени, поэтому completes = доигранные матчи, fails нет.
    matches = sorted({k for s in (cur, old) if s for k in s["matches"]}, key=lambda k: (k.endswith("plus"), int(re.sub(r"\D", "", k))))
    if matches:
        out += ["", "## Воронка по матчам (старты / доиграли / ушли посреди; далее = доля, перешедшая в следующий)", "", head, sep]

        def match_step(s, name):
            r = s["matches"].get(name)
            if not r:
                return "-"
            nxt = s["matches"].get(str(int(name) + 1)) if name.isdigit() else None
            go = f"; далее {pct(nxt[0], r[0])}" if nxt else ""
            return f"{r[0]} / {r[1]} / {r[3]} (ушли {pct(r[3], r[0])}){go}"

        for name in matches:
            out.append(line(f"match-{name}", lambda s, n=name: match_step(s, n)))

    # Visits lost at each step of the first minutes. The sum through R1 matches the 0-1 min bar of the Poki dashboard.
    def steps(s):
        loads, loaded = s["loads"]
        r = lambda n: s["rounds"].get(str(n), (0, 0, 0, 0))
        out = [("загрузка", loads - loaded), ("загрузились, нет ввода", loaded - r(1)[0])]
        for n in range(1, 6):
            done = r(n)[1] + r(n)[2]
            out += [(f"посреди R{n}", r(n)[0] - done), (f"после R{n}", done - r(n + 1)[0])]
        return out, loads

    def lost(s, i):
        rows, loads = steps(s)
        before = sum(n for _, n in rows[:i + 1])
        return f"{rows[i][1]} ({pct(rows[i][1], loads)}; всего {pct(before, loads)})"

    out += ["", "## Где уходят (визиты, от начавших загрузку; всего = накопленно)", "", head, sep]
    for i, (label, _) in enumerate(steps(cur)[0]):
        out.append(line(label, lambda s, i=i: lost(s, i)))

    # Totals count rounds, not visits: presses of NEXT MATCH (lobby since v0.4.0) whose next round never started.
    later = lambda s: starts(s) - s["rounds"].get("1", (0,))[0]
    out.append(line("нажали NEXT MATCH, раунд не начался (события)", lambda s: str(num(s["again"], "total_interacted") - later(s))))
    out.append(line("ушли с первого результата, не нажав", lambda s: f"{num(s['again'], 'seen') - num(s['again'], 'interacted')} / {num(s['again'], 'seen')}"))

    def share(key):
        return lambda s, k: f"{s[key].get(k, 0)} ({pct(s[key].get(k, 0), sum(s[key].values()))})"

    def keys_of(key):
        return sorted({k for s in (cur, old) if s for k in s[key]})

    for title, key, order in (
        ("Исход (outcome)", "outcome", None),
        ("Причина выбывания", "cause", None),
        ("Время на арене", "time", sorted(keys_of("time"), key=lambda k: int(k.split("-")[0]))),
        ("Ускорения", "boosts", None),
        ("Чем рулили впервые", "input", None),
        ("Чем впервые ускорились (с v0.2.0)", "boost_input", None),
        ("Первое движение, с после старта", "first_move", None),
        ("Место в 1-м раунде", "place1", sorted(keys_of("place1"), key=int)),
        ("Нокауты игрока в 1-м раунде", "ko1", sorted(keys_of("ko1"), key=int)),
        ("Машинок в раунде (с v0.2)", "cars", None),
        ("Длительность раунда (с v0.2)", "round_time", None),
        ("Причина самовылета (с v0.2.0; ball: мяч арены tabletop)", "fall_cause", None),
        ("REVENGE (с v0.2.0)", "revenge", None),
        ("Матч: место по матчу (визиты)", "match_place", None),
        ("Матч: нокауты игрока по матчу (визиты)", "match_knockouts", None),
        ("Матч: падения игрока по матчу (визиты)", "match_falls", None),
        ("Матч: из них столкнули по матчу (визиты)", "match_pushed", None),
        ("Матч: REVENGE по матчу (визиты)", "match_revenge", None),
        ("Матч: подобранные способности до ящиков (v0.6; heavy, shockwave, shield, turbo; визиты)", "match_pickup", None),
        ("Матч: сколько ящиков разбил игрок за матч (до ящиков: способностей; визиты)", "match_pickups", None),
        ("Матч: какой предмет выпал из ящика (item-get; визиты)", "match_item_get", None),
        ("Матч: какой предмет игрок применил (item-use; визиты)", "match_item_use", None),
    ):
        keys = order if order is not None else keys_of(key)
        if keys:
            out += table(title, cur, old, version, previous, keys, share(key))

    out += ["", "## PLAY AGAIN (нажали / показано, последний показ)", "", head, sep]
    out.append(line("result-play-again", lambda s: f"{num(s['again'], 'interacted')} / {num(s['again'], 'seen')} ({pct(num(s['again'], 'interacted'), num(s['again'], 'seen'))})"))

    def pressed(s, field, k):
        row = s[field].get(k)
        if not row:
            return "-"
        seen, shows = num(row, "seen"), num(row, "total_seen")
        return (f"{num(row, 'interacted')} / {seen} визитов ({pct(num(row, 'interacted'), seen)}), "
                f"{num(row, 'total_interacted')} / {shows} показов ({pct(num(row, 'total_interacted'), shows)})")

    def arena(s, k):
        row = s["arenas"].get(k)
        if not row:
            return "-"
        starts = num(row, "starts")
        return (f"{starts} / {num(row, 'completes')} / {num(row, 'fails')} / {num(row, 'lefts')} "
                f"(ушли {pct(num(row, 'lefts'), starts)}); раундов {num(row, 'total_starts')}")

    for title, key, fmt in (
        ("NEXT MATCH по исходу и раунду (после v0.5.1; нажали / показано)", "again_by", lambda s, k: pressed(s, "again_by", k)),
        ("Медленное лобби, загрузка дольше 4 с (после v0.5.1; дождались / медленных)", "slow", lambda s, k: pressed(s, "slow", k)),
        # В долгом матче вариант сменяется с каждым циклом пола: старт и complete на цикл, поражений нет, «ушли посреди»
        # это ушли посреди цикла этого варианта, а «раундов» это циклов.
        ("Раунды по варианту арены (после v0.5.1; визиты: старты / победы / поражения / ушли посреди; "
         "в матче по циклам пола)", "arenas", arena),
    ):
        keys = keys_of(key)
        if keys:
            out += table(title, cur, old, version, previous, keys, fmt)

    # Milestones count gameplays containing an event, as the rest of other-events above; they are not event totals.
    for title, key in (
        ("Новые уровни (визиты с level-up)", "levels"),
        ("Открытия машинок (визиты с unlock)", "unlocks"),
        ("Выбор машинок в гараже (визиты с equip)", "equips"),
        ("На чём ездят по раундам (визиты с car)", "driven"),
        ("Выбор цвета в гараже (визиты с color)", "colors"),
        ("Цвет машинки по раундам (визиты с color)", "played_colors"),
        ("Открытия арен (визиты с map unlock)", "map_unlocks"),
        ("Арены по раундам (визиты с map)", "maps"),
        ("Варианты арен по раундам (визиты с map-variant)", "variants"),
    ):
        keys = keys_of(key)
        if key == "levels":
            keys.sort(key=lambda k: (k.endswith("plus"), int(k.removesuffix("plus"))))
        out += table(title, cur, old, version, previous, keys,
                     lambda s, k, field=key: str(s[field].get(k, 0)))
        if not keys:
            out.append(line("Нет событий в выгрузке", lambda s: "-"))

    out += ["", "## GARAGE (нажали / показано, последний показ)", "", head, sep]
    out.append(line("garage-button", lambda s: f"{num(s['garage'], 'interacted')} / {num(s['garage'], 'seen')} ({pct(num(s['garage'], 'interacted'), num(s['garage'], 'seen'))})"))
    out += ["", "## Лобби (показы)", "", head, sep]
    out.append(line("lobby-shown", lambda s: str(num(s["lobby"], "seen"))))
    out += ["", "## Таблица раунда (показы)", "", head, sep]
    out.append(line("scoreboard-shown", lambda s: str(num(s["scoreboard"], "seen"))))
    print("\n".join(out))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    report(sys.argv[1].lstrip("v"), sys.argv[2].lstrip("v") if len(sys.argv) > 2 else None)
