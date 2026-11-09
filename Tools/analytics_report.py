"""Сводка выгрузки Poki (Analytics/v<версия>/v<версия>_*.csv) и сравнение с прошлой версией.

Запуск: python -I Tools/analytics_report.py <версия> [<прошлая версия>]   (версия без "v", например 0.2.0)
Печатает Markdown: воронку по раундам (переход, победы, поражения, уходы посреди раунда), исходы, места и нокауты
в 1-м раунде, время сессии (после v0.14.1), время на арене, причины выбывания, ускорения, управление, прогресс, гараж и выбор машинок; для сборок
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
        # до v0.14.1; с v0.2 то же по раундам в round-out-cause
        "cause": other(o, r"result-(pushed|fell)-out-cause"),
        "round_cause": other(o, r"result-((?:pushed|fell)-r(?:1|2|3|4plus))-round-out-cause"),
        "fall_cause": other(o, r"result-(fell-[a-z]+-r[0-9a-z]+)-fall-cause"),
        "revenge": other(o, r"result-(revenge-[a-z]+-r[0-9a-z]+)-revenge"),
        "time": other(o, r"result-(\d+-\d+)-time"),
        "boosts": other(o, r"result-(boosts-[a-z0-9-]+?)-boosts"),  # до v0.14.1
        # после v0.14.1: минуты на странице и в раундах (SessionTime), визиты, дошедшие до каждой
        "session_time": {int(k): v for k, v in other(o, r"session-time-(\d+)m-reached").items()},
        "session_play": {int(k): v for k, v in other(o, r"session-play-(\d+)m-reached").items()},
        # после v0.15.0: номер визита игрока и дни с прошлого (Visits)
        "visits": other(o, r"session-visit-(1|2|3|4plus)-visit"),
        "back": other(o, r"session-back-(d0|d1|d2-7|d8plus|unknown)-visit"),
        "input": other(o, r"controls-([a-z]+)-input"),
        "boost_input": other(o, r"controls-boost-(button|tap|finger|key|mouse|gamepad|none)-boost-input"),
        "first_move": other(o, r"controls-(move-[a-z0-9-]+|never)-first-move"),
        "swipes": other(o, r"controls-swipes-(none|few|many)-swipes"),
        # после v0.13.5: первый стартовый экран визита показан, тронули, и через сколько секунд после старта движка
        "start": i.get("controls-start"),
        "start_time": other(o, r"controls-ready-([0-9a-z-]+)-start-time"),
        # с v0.16.0: главное меню визитов 2+ (показ, через сколько секунд после старта движка, что нажали), HOME на результате
        "menu": i.get("menu-shown"),
        "menu_time": other(o, r"menu-ready-([0-9a-z-]+)-start-time"),
        "menu_press": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"menu-(play|garage|missions|mode|leagues|boost|boost-no|daily|daily-no)", e))},
        # после v0.16.0: попап NEW LEAGUE! в меню, по лиге (раз на игрока и лигу)
        "menu_league": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"menu-league-(bronze|silver|gold|legend)", e))},
        # после feature/result-popups: тот же попап на результате раунда, который поднял лигу (меню показывает его, только если результат не показал)
        "result_league": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"result-league-(bronze|silver|gold|legend)", e))},
        "home": i.get("result-home"),
        "place1": other(o, r"result-place-(\d+)-r1-place"),
        "ko1": other(o, r"result-knockouts-(\d+)-r1-knockouts"),
        "again": i.get("result-play-again"),
        "garage": i.get("garage-button"),
        "lobby": i.get("lobby-shown"),  # до v0.14.1
        # после v0.5.1: NEXT MATCH по исходу и раунду, медленное лобби по арене, раунды по варианту арены
        "again_by": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"result-again-([a-z]+-r(?:1|2|3|4plus))", e))},
        # после v0.6.0: NEXT MATCH после первого матча визита по устройству и по тому, ускорялся ли игрок
        "again_first": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"controls-again-([a-z]+-(?:boost|noboost))", e))},
        "slow": {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"lobby-slow-([a-z]+)", e))},
        "arenas": {m.group(1): row for e, row in p.items() if (m := re.fullmatch(r"arena-([a-z]+-[a-z]+)", e))},
        # после v0.15.0: особые раунды (start / complete / fail) и их исход, в раунде босса взят ли он
        "specials": {m.group(1): row for e, row in p.items()
                     if (m := re.fullmatch(r"special-(mega|hot-potato|boss|ice|low-gravity)", e))},
        "special_outcome": other(o, r"special-((?:mega|hot-potato|boss|ice|low-gravity)-(?:won|out|survived))-outcome"),
        "boss": other(o, r"special-boss-(taken|missed)-boss"),
        "scoreboard": i.get("scoreboard-shown"),
        "ad_commercial": i.get("ad-commercial"),
        "ad_double": i.get("ad-double"),
        "ad_double_end": other(o, r"ad-double-(granted|unavailable|dismissed)"),
        # после v0.16.0: XP BOOST в меню (плитка с предложением показана, WATCH, чем кончилась реклама) и раунды под бустом
        "ad_boost": i.get("ad-boost"),
        "ad_boost_end": other(o, r"ad-boost-(granted|unavailable|dismissed)"),
        "boosted": other(o, r"result-boosted-(r(?:1|2|3|4plus))-xp-boost"),
        # после v0.16.0: ежедневный подарок в меню (попап с подарком показан, день календаря, сброс серии, шапка, CLAIM x2)
        "daily_offer": i.get("daily-offer"),
        "daily_offer_result": i.get("daily-offer-result"),
        "result_daily_no": i.get("result-daily-no"),
        "daily_claim": other(o, r"daily-(day-[0-9]+|reset|hat)-claim"),
        "ad_daily": i.get("ad-daily"),
        "ad_daily_end": other(o, r"ad-daily-(granted|unavailable|dismissed)"),
        "levels": other(o, r"progress-level-(2|3|4|5|6|7|8|9|10|10plus)-level-up"),
        "unlocks": other(o, r"progress-(?!map-)([a-z]+(?:-[a-z]+)*)-unlock"),
        "map_unlocks": other(o, r"progress-map-([a-z]+)-unlock"),
        "maps": other(o, r"result-map-([a-z]+-r(?:1|2|3|4plus))-map"),
        "variants": other(o, r"result-variant-([a-z]+-[a-z]+-r(?:1|2|3|4plus))-map-variant"),
        "equips": other(o, r"garage-(?!topper-)([a-z]+(?:-[a-z]+)*)-equip"),
        "driven": other(o, r"result-car-([a-z]+(?:-[a-z]+)*-r(?:1|2|3|4plus))-car"),
        "colors": other(o, r"garage-color-(green|red|blue|yellow|purple|orange|pink|dark)-color"),
        "played_colors": other(o, r"result-color-((?:green|red|blue|yellow|purple|orange|pink|dark)-r(?:1|2|3|4plus))-color"),
        "round_boosts": other(o, r"result-boosts-((?:none|few|many)-r(?:1|2|3|4plus))-round-boosts"),
        "auto_boosts": other(o, r"result-auto-((?:none|few|many)-r(?:1|2|3|4plus))-auto-boosts"),
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
    # после v0.7.1 то же в раундах «последний оставшийся» под result
    s["match_pickups"] = other(o, r"(?:match|result)-pickups-([0-9a-z]+-r[0-9a-z]+)-pickups")
    s["match_item_get"] = other(o, r"(?:match|result)-item-((?:heavy|mega|shockwave|shield|turbo|bomb|glove|bowling|balloon|oil)-r[0-9a-z]+)-item-get")
    s["match_item_use"] = other(o, r"(?:match|result)-item-((?:heavy|mega|shockwave|shield|turbo|bomb|glove|bowling|balloon|oil)-r[0-9a-z]+)-item-use")
    # v0.10: миссии, серии побед, монеты и шапки (toppers)
    s["missions_done"] = other(o, r"mission-done-([0-9a-z]+-r[0-9a-z]+)-missions")
    s["mission_complete"] = other(o, r"mission-([a-z]+(?:-[a-z]+)*)-complete")
    s["streak"] = other(o, r"result-streak-([0-9a-z]+-r[0-9a-z]+)-streak")
    s["coins"] = other(o, r"result-coins-((?:few|mid|many)-r[0-9a-z]+)-coins")
    s["topper_buy"] = other(o, r"shop-(?!car-)([a-z]+(?:-[a-z]+)*)-buy")
    s["car_buy"] = other(o, r"shop-car-([a-z]+(?:-[a-z]+)*)-buy")
    s["car_offer"] = {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"shop-offer-([a-z]+(?:-[a-z]+)*)", e))}
    s["car_equip"] = {m.group(1): row for e, row in i.items() if (m := re.fullmatch(r"shop-equip-([a-z]+(?:-[a-z]+)*)", e))}
    s["topper_equip"] = other(o, r"garage-topper-([a-z]+(?:-[a-z]+)*)-equip")
    s["missions_shown"] = i.get("mission-shown")
    s["toppers_tab"] = i.get("garage-toppers")
    s["again_streak"] = {m.group(1): row for e, row in i.items()
                         if (m := re.fullmatch(r"result-again-([a-z]+-(?:lost|streak|nostreak)-r(?:1|2|3|4plus))", e))}
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
    if any(s and s["start"] for s in (cur, old)):
        out.append(line("Стартовый экран показан, от загрузки (после v0.13.5)",
                        lambda s: f"{num(s['start'], 'seen')} ({pct(num(s['start'], 'seen'), s['loads'][1])})" if s["start"] else "-"))
        out.append(line("Тронули стартовый экран, от показа",
                        lambda s: f"{num(s['start'], 'interacted')} ({pct(num(s['start'], 'interacted'), num(s['start'], 'seen'))})" if s["start"] else "-"))

    # Poki shows the session length and engaged (past 3 min) only for a Player Fit Test; SessionTime sends its own.
    # The mean is a lower bound: the sum over milestones of the share that reached it times the minutes since the last.
    if any(s and s["session_time"] for s in (cur, old)):
        out += ["", "## Время сессии (после v0.14.1; визиты, от начавших загрузку)", "", head, sep]
        minutes = sorted({m for s in (cur, old) if s for key in ("session_time", "session_play") for m in s[key]})

        def reached(s, key, m):
            return f"{s[key].get(m, 0)} ({pct(s[key].get(m, 0), s['loads'][0])})" if s[key] else "-"

        def mean(s, key):
            if not s[key] or not s["loads"][0]:
                return "-"
            total, last = 0.0, 0
            for m in sorted(s[key]):
                total += s[key][m] / s["loads"][0] * (m - last)
                last = m
            return f"не меньше {total:.1f} мин"

        out.append(line("Engaged: 3 мин на странице", lambda s: reached(s, "session_time", 3)))
        out.append(line("Средняя сессия на странице (оценка снизу)", lambda s: mean(s, "session_time")))
        out.append(line("Средняя игра в раундах (оценка снизу)", lambda s: mean(s, "session_play")))
        for m in minutes:
            out.append(line(f"{m} мин на странице", lambda s, m=m: reached(s, "session_time", m)))
        for m in minutes:
            out.append(line(f"{m} мин в раундах", lambda s, m=m: reached(s, "session_play", m)))

    if any(s and s["visits"] for s in (cur, old)):
        out += ["", "## Повторные визиты (после v0.15.0; визиты, от начавших загрузку)", "", head, sep]
        total = lambda s: sum(s["visits"].values())
        for v in ("1", "2", "3", "4plus"):
            out.append(line(f"Визит {v}", lambda s, v=v: f"{s['visits'].get(v, 0)} ({pct(s['visits'].get(v, 0), total(s))})" if s["visits"] else "-"))
        back = lambda s: sum(s["back"].values())
        for b, name in (("d0", "в тот же день"), ("d1", "на следующий день"), ("d2-7", "через 2–7 дней"),
                        ("d8plus", "через 8 дней и позже"), ("unknown", "игрок до v0.16, день неизвестен")):
            out.append(line(f"Вернулись {name}", lambda s, b=b: f"{s['back'].get(b, 0)} ({pct(s['back'].get(b, 0), back(s))})" if s["back"] else "-"))

    # The menu opens visits 2+ (from v0.16); its presses are counted per menu shown, HOME per result that showed it.
    if any(s and (s["menu"] or s["home"]) for s in (cur, old)):
        out += ["", "## Главное меню (с v0.16.0; визиты 2+)", "", head, sep]
        shown = lambda s: num(s["menu"], "seen")
        out.append(line("Меню показано", lambda s: str(shown(s)) if s["menu"] else "-"))
        for key, name in (("play", "PLAY"), ("garage", "GARAGE"), ("missions", "MISSIONS"), ("mode", "MODE (до feature/ux-fixes: потом это плашка, не кнопка)"),
                          ("leagues", "значок или шкалу опыта (лиги; после v0.16.0)")):
            out.append(line(f"Нажали {name}, от показов меню",
                            lambda s, k=key: (lambda n: f"{n} ({pct(n, shown(s))})")(num(s["menu_press"].get(k), "interacted"))
                            if s["menu"] else "-"))
        times = lambda s: sum(s["menu_time"].values())
        for bucket in ("0-3", "3-6", "6-10", "10plus"):
            out.append(line(f"Меню на экране через {bucket} с после старта движка",
                            lambda s, b=bucket: f"{s['menu_time'].get(b, 0)} ({pct(s['menu_time'].get(b, 0), times(s))})" if s["menu_time"] else "-"))
        for league in ("bronze", "silver", "gold", "legend"):
            out.append(line(f"Попап NEW LEAGUE! {league.upper()} показан (после v0.16.0)",
                            lambda s, l=league: str(num(s["menu_league"].get(l), "seen")) if s["menu"] else "-"))
        for league in ("bronze", "silver", "gold", "legend"):
            out.append(line(f"Попап NEW LEAGUE! {league.upper()} на результате (после feature/result-popups)",
                            lambda s, l=league: str(num(s["result_league"].get(l), "seen")) if s["result_league"] else "-"))
        out.append(line("HOME на результате: показан / нажат",
                        lambda s: f"{num(s['home'], 'seen')} / {num(s['home'], 'interacted')} ({pct(num(s['home'], 'interacted'), num(s['home'], 'seen'))})" if s["home"] else "-"))

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
        ("Причина выбывания (до v0.14.1)", "cause", None),
        ("Причина выбывания по раундам (с v0.2)", "round_cause", None),
        ("Время на арене", "time", sorted(keys_of("time"), key=lambda k: int(k.split("-")[0]))),
        ("Ускорения (до v0.14.1)", "boosts", None),
        ("Чем рулили впервые", "input", None),
        ("Чем впервые ускорились (с v0.2.0; после v0.14.1 только в сборке с бустами)", "boost_input", None),
        ("Первое движение, с после старта", "first_move", None),
        ("Свайпы в 1-м раунде (none, few 1–3, many 4+)", "swipes", ["none", "few", "many"]),
        ("Стартовый экран через N с после старта движка (после v0.13.5)", "start_time",
         [k for k in ("0-3", "3-6", "6-10", "10plus") if any(s and k in s["start_time"] for s in (cur, old))]),
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
        ("Ящики: сколько разбил игрок за матч или раунд (до ящиков: способностей; визиты)", "match_pickups", None),
        ("Предметы: какой выпал из ящика (item-get; визиты)", "match_item_get", None),
        ("Предметы: какой игрок применил (item-use; визиты)", "match_item_use", None),
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

    def arena(s, k, key="arenas"):
        row = s[key].get(k)
        if not row:
            return "-"
        starts = num(row, "starts")
        return (f"{starts} / {num(row, 'completes')} / {num(row, 'fails')} / {num(row, 'lefts')} "
                f"(ушли {pct(num(row, 'lefts'), starts)}); раундов {num(row, 'total_starts')}")

    for title, key, fmt in (
        ("NEXT MATCH по исходу и раунду (после v0.5.1; нажали / показано)", "again_by", lambda s, k: pressed(s, "again_by", k)),
        ("NEXT MATCH после первого матча по устройству и бусту (после v0.6.0; нажали / показано)", "again_first",
         lambda s, k: pressed(s, "again_first", k)),
        ("NEXT MATCH по исходу и серии побед (v0.10; нажали / показано)", "again_streak",
         lambda s, k: pressed(s, "again_streak", k)),
        ("Попап новой машинки с ценой (v0.10; купили / показано)", "car_offer", lambda s, k: pressed(s, "car_offer", k)),
        ("Попап новой бесплатной машинки (после v0.13.1; EQUIP / показано)", "car_equip",
         lambda s, k: pressed(s, "car_equip", k)),
        ("Медленное лобби, загрузка дольше 4 с (после v0.5.1; дождались / медленных)", "slow", lambda s, k: pressed(s, "slow", k)),
        # В долгом матче вариант сменяется с каждым циклом пола: старт и complete на цикл, поражений нет, «ушли посреди»
        # это ушли посреди цикла этого варианта, а «раундов» это циклов.
        ("Раунды по варианту арены (после v0.5.1; визиты: старты / победы / поражения / ушли посреди; "
         "в матче по циклам пола)", "arenas", arena),
        ("Особые раунды (после v0.15.0; визиты: старты / победы / поражения / ушли посреди)", "specials",
         lambda s, k: arena(s, k, "specials")),
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
        ("Особые раунды по исходу (после v0.15.0; визиты с outcome)", "special_outcome"),
        ("Раунд босса: столкнул ли игрок босса (после v0.15.0; визиты)", "boss"),
        ("Свои рывки по раундам (после v0.7.1, после v0.14.1 только в сборке с бустами; визиты с round-boosts)", "round_boosts"),
        ("Рывки автобуста по раундам (после v0.7.1, после v0.14.1 только в сборке с бустами; визиты с auto-boosts)", "auto_boosts"),
        ("Миссии: сколько выполнено за раунд (v0.10, визиты с missions)", "missions_done"),
        ("Миссии: какие выполнены (v0.10, визиты с complete)", "mission_complete"),
        ("Серия побед после раунда (v0.10, визиты с streak; lost: серия прервана)", "streak"),
        ("Монеты после раунда против цен шапок (v0.10 до v0.14.1, визиты с coins)", "coins"),
        ("Покупка шапок (v0.10, визиты с buy)", "topper_buy"),
        ("Покупка машинок за монеты (v0.10, визиты с buy)", "car_buy"),
        ("Шапка надета в гараже (v0.10, визиты с equip; none: снята)", "topper_equip"),
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
    out.append(line("garage-toppers (вкладка TOPPERS, v0.10)", lambda s: f"{num(s['toppers_tab'], 'interacted')} / {num(s['toppers_tab'], 'seen')} ({pct(num(s['toppers_tab'], 'interacted'), num(s['toppers_tab'], 'seen'))})"))
    out.append(line("mission-shown (миссии на результате, v0.10; визиты)", lambda s: str(num(s["missions_shown"], "seen"))))
    out += ["", "## Лобби (показы, до v0.14.1)", "", head, sep]
    out.append(line("lobby-shown", lambda s: str(num(s["lobby"], "seen"))))
    out += ["", "## Таблица раунда (показы)", "", head, sep]
    out.append(line("scoreboard-shown", lambda s: str(num(s["scoreboard"], "seen"))))
    out += ["", "## Реклама (v0.12)", "", head, sep]
    out.append(line("commercial (перерыв после NEXT MATCH; визиты)", lambda s: str(num(s["ad_commercial"], "seen"))))
    out.append(line("CLAIM x2 (нажали / показано)", lambda s: f"{num(s['ad_double'], 'interacted')} / {num(s['ad_double'], 'seen')} ({pct(num(s['ad_double'], 'interacted'), num(s['ad_double'], 'seen'))})"))
    for how in ("granted", "unavailable", "dismissed"):
        out.append(line(f"CLAIM x2: {how} (визиты)", lambda s, how=how: str(s["ad_double_end"].get(how, 0))))

    # XP BOOST: the tile's offer counts per menu shown with it; the popup and NO THANKS are menu presses.
    if any(s and s["ad_boost"] for s in (cur, old)):
        out += ["", "## XP-буст за рекламу (после v0.16.0; x2 опыта на 5 матчей)", "", head, sep]
        offered = lambda s: num(s["ad_boost"], "seen")
        opened = lambda s: num(s["menu_press"].get("boost"), "interacted")
        out.append(line("Предложение показано в меню", lambda s: str(offered(s))))
        out.append(line("Открыли попап, от показов", lambda s: f"{opened(s)} ({pct(opened(s), offered(s))})"))
        out.append(line("WATCH, от открытых попапов", lambda s: (lambda n: f"{n} ({pct(n, opened(s))})")(num(s["ad_boost"], "interacted"))))
        out.append(line("NO THANKS или закрыли, от открытых попапов",
                        lambda s: (lambda n: f"{n} ({pct(n, opened(s))})")(num(s["menu_press"].get("boost-no"), "interacted"))))
        for how in ("granted", "unavailable", "dismissed"):
            out.append(line(f"Реклама буста: {how} (визиты)", lambda s, how=how: str(s["ad_boost_end"].get(how, 0))))
        for r in ("r1", "r2", "r3", "r4plus"):
            out.append(line(f"Раунды под бустом, {r.upper()} (визиты)", lambda s, r=r: str(s["boosted"].get(r, 0))))

    # The daily gift: the popup with a gift to claim, its days, a streak broken, the hat, and CLAIM x2.
    if any(s and s["daily_offer"] for s in (cur, old)):
        out += ["", "## Ежедневный подарок (после v0.16.0; меряем visit-2+ и back-d1 в «Повторных визитах»)", "", head, sep]
        shown = lambda s: num(s["daily_offer"], "seen")
        out.append(line("Попап с подарком показан (визиты)", lambda s: str(shown(s))))
        out.append(line("Плитка DAILY нажата", lambda s: str(num(s["menu_press"].get("daily"), "interacted"))))
        out.append(line("Закрыли в меню, не забрав", lambda s: (lambda n: f"{n} ({pct(n, shown(s))})")(num(s["menu_press"].get("daily-no"), "interacted"))))
        on_result = lambda s: num(s.get("daily_offer_result"), "seen")
        out.append(line("Из них над первым результатом визита", lambda s: f"{on_result(s)} ({pct(on_result(s), shown(s))})"))
        out.append(line("Закрыли над результатом, не забрав", lambda s: (lambda n: f"{n} ({pct(n, on_result(s))})")(num(s.get("result_daily_no"), "interacted"))))
        for day in range(1, 8):
            out.append(line(f"Забрали день {day} (визиты)", lambda s, day=day: str(s["daily_claim"].get(f"day-{day}", 0))))
        out.append(line("Серия сброшена (визиты)", lambda s: str(s["daily_claim"].get("reset", 0))))
        out.append(line("Шапка недели (визиты)", lambda s: str(s["daily_claim"].get("hat", 0))))
        out.append(line("CLAIM x2 (нажали / показано)", lambda s: f"{num(s['ad_daily'], 'interacted')} / {num(s['ad_daily'], 'seen')} ({pct(num(s['ad_daily'], 'interacted'), num(s['ad_daily'], 'seen'))})"))
        for how in ("granted", "unavailable", "dismissed"):
            out.append(line(f"CLAIM x2 подарка: {how} (визиты)", lambda s, how=how: str(s["ad_daily_end"].get(how, 0))))
    print("\n".join(out))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    report(sys.argv[1].lstrip("v"), sys.argv[2].lstrip("v") if len(sys.argv) > 2 else None)
