import importlib


def register(d):
    for name in ('designs_sandbox', 'designs_tabletop', 'designs_candy', 'designs_playroom', 'designs_pitch'):
        try:
            module = importlib.import_module(name)
        except ModuleNotFoundError as e:
            if e.name != name:
                raise
            continue
        module.register(d)
