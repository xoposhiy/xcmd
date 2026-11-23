"""Render separate target and action confusion matrices for the two frozen reviews."""
import json
from collections import Counter

from review import HERE, MANIFEST, call_labels, read_archive


UNPAIRED = "Unpaired"


def confusion(a, b, group):
    counts = Counter()
    cases = Counter()
    for key in a:
        left, right = (call_labels(review[key], group) for review in (a, b))
        counts.update((label, label) for label in left & right)
        only_a, only_b = left - right, right - left
        if left == right:
            cases["equal"] += 1
        elif len(only_a) == len(only_b) == 1:
            counts[next(iter(only_a)), next(iter(only_b))] += 1
            cases["substitution"] += 1
        else:
            cases["ambiguous" if only_a and only_b else "one_sided"] += 1
            counts.update((label, UNPAIRED) for label in only_a)
            counts.update((UNPAIRED, label) for label in only_b)
    return counts, cases


def render(group, order, counts, cases):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.ticker import PercentFormatter

    active = {label for cell in counts for label in cell}
    labels = [label for label in order if label in active] + [UNPAIRED]
    totals = [sum(counts[row, col] for col in labels) for row in labels]
    percents = [[100 * counts[row, col] / total if total else 0 for col in labels]
                for row, total in zip(labels, totals)]
    size = len(labels)
    fig, ax = plt.subplots(figsize=(2.9 + size * 0.83, 2.3 + size * 0.65),
                           layout="constrained")
    img = ax.imshow(percents, vmin=0, vmax=100, cmap="Blues", aspect="auto")
    ax.set_xticks(range(size), labels)
    ax.set_yticks(range(size), [f"{label}  ({total})" for label, total in zip(labels, totals)])
    ax.xaxis.tick_top()
    ax.xaxis.set_label_position("top")
    ax.set_xlabel("Reviewer B", labelpad=12, fontweight="bold")
    ax.set_ylabel("Reviewer A  (row total)", labelpad=12, fontweight="bold")
    ax.tick_params(length=0, pad=6)
    ax.axvline(size - 1.5, color="#638098", linewidth=1)
    ax.axhline(size - 1.5, color="#638098", linewidth=1)
    for i, row in enumerate(labels):
        for j, col in enumerate(labels):
            n, pct = counts[row, col], percents[i][j]
            text = f"{pct:.1f}%\n({n})" if n else "·"
            color = "white" if pct >= 60 else "#123450" if n else "#9cabb5"
            ax.text(j, i, text, ha="center", va="center", color=color, fontsize=10)
    bar = fig.colorbar(img, ax=ax, fraction=0.03, pad=0.03, ticks=[0, 50, 100],
                       format=PercentFormatter(xmax=100, decimals=0))
    bar.outline.set_visible(False)
    fig.suptitle(f"Tool call {group}s\n{cases['equal']} of {sum(cases.values())} calls have identical label sets",
                 fontsize=15, fontweight="bold")
    fig.supxlabel("Cells show row percentage and count. A dot means zero.\n"
                  "Unpaired = a label with no unique counterpart after matching shared labels.",
                  fontsize=9, color="#374960")
    fig.savefig(HERE / f"{group}-confusion.png", dpi=170, facecolor="white")
    plt.close(fig)


if __name__ == "__main__":
    a, b = read_archive(json.loads(MANIFEST.read_text(encoding="utf8")))
    for group, order in [
        ("target", ["tooling", "docs", "unknown", "source", "tests", "git", "project", "tracker"]),
        ("action", ["read", "write", "other", "unknown"]),
    ]:
        counts, cases = confusion(a, b, group)
        render(group, order, counts, cases)
        print(group, dict(cases))
