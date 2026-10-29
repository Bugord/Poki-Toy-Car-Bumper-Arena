"""Сводка выгрузки Poki (Analytics/v<версия>/v<версия>_*.csv) и сравнение с прошлой версией.

Запуск: python -I Tools/analytics_report.py <версия> [<прошлая версия>]   (версия без "v", например 0.2.0)
Печатает Markdown: воронку по раундам (переход, победы, поражения, уходы посреди раунда), исходы, места и нокауты
в 1-м раунде, время на арене, причины выбывания, ускорения, управление и кнопку PLAY AGAIN.
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


def rounds(progress):
    out = {}
    for event, row in progress.items():
        m = re.fullmatch(r"round-(\d+(?:plus)?)", event)
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
        "first_move": other(o, r"controls-(move-[a-z0-9-]+|never)-first-move"),
        "place1": other(o, r"result-place-(\d+)-r1-place"),
        "ko1": other(o, r"result-knockouts-(\d+)-r1-knockouts"),
        "again": i.get("result-play-again"),
        "progress": p,
    }
    # с v0.2: на раунд (R = 1, 2, 3, 4plus)
    s["cars"] = other(o, r"result-cars-(\d+-r[0-9a-z]+)-cars")
    s["round_time"] = other(o, r"result-((?:early|mid|late|full)-r[0-9a-z]+)-round-time")
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
        ("Первое движение, с после старта", "first_move", None),
        ("Место в 1-м раунде", "place1", sorted(keys_of("place1"), key=int)),
        ("Нокауты игрока в 1-м раунде", "ko1", sorted(keys_of("ko1"), key=int)),
        ("Машинок в раунде (с v0.2)", "cars", None),
        ("Длительность раунда (с v0.2)", "round_time", None),
        ("Причина самовылета (с v0.2.0)", "fall_cause", None),
        ("REVENGE (с v0.2.0)", "revenge", None),
    ):
        keys = order if order is not None else keys_of(key)
        if keys:
            out += table(title, cur, old, version, previous, keys, share(key))

    out += ["", "## PLAY AGAIN (нажали / показано, последний показ)", "", head, sep]
    out.append(line("result-play-again", lambda s: f"{num(s['again'], 'interacted')} / {num(s['again'], 'seen')} ({pct(num(s['again'], 'interacted'), num(s['again'], 'seen'))})"))
    print("\n".join(out))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    report(sys.argv[1].lstrip("v"), sys.argv[2].lstrip("v") if len(sys.argv) > 2 else None)
