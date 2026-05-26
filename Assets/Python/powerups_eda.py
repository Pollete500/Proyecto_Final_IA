from __future__ import annotations

import argparse
import csv
from collections import Counter, defaultdict
from pathlib import Path
from statistics import mean


FIELDNAMES = (
    "recta",
    "enemigos_delante",
    "enemigos_atras",
    "platanos_delante",
    "conchas_atras",
    "power_up_tirado",
)

NUMERIC_COLUMNS = (
    "enemigos_delante",
    "enemigos_atras",
    "platanos_delante",
    "conchas_atras",
)

CLASS_ORDER = ("banana", "shell", "mushroom", "star")
CLASS_COLORS = {
    "banana": "#f1c40f",
    "shell": "#27ae60",
    "mushroom": "#e74c3c",
    "star": "#3498db",
}


def resolve_paths() -> tuple[Path, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    data_dir = project_root / "Assets" / "Data"
    return data_dir / "powerups_sintetico.csv", data_dir / "EDA_powerups_sintetico"


def parse_bool(value: str) -> bool:
    return str(value).strip().lower() in {"true", "1", "yes", "y"}


def load_rows(input_path: Path) -> list[dict[str, object]]:
    with input_path.open("r", encoding="utf-8", newline="") as csv_file:
        reader = csv.DictReader(csv_file)
        rows: list[dict[str, object]] = []
        for row in reader:
            rows.append(
                {
                    "recta": parse_bool(row["recta"]),
                    "enemigos_delante": int(row["enemigos_delante"]),
                    "enemigos_atras": int(row["enemigos_atras"]),
                    "platanos_delante": int(row["platanos_delante"]),
                    "conchas_atras": int(row["conchas_atras"]),
                    "power_up_tirado": row["power_up_tirado"].strip().lower(),
                }
            )
        return rows


def try_import_matplotlib():
    try:
        import matplotlib.pyplot as plt  # type: ignore
        return plt
    except ImportError:
        return None


def write_summary(rows: list[dict[str, object]], output_dir: Path) -> None:
    class_counts = Counter(str(row["power_up_tirado"]) for row in rows)
    recta_counts = Counter(bool(row["recta"]) for row in rows)
    lines = [
        "# EDA - powerups dataset",
        "",
        f"- Filas totales: `{len(rows)}`",
        "",
        "## Balance de clases",
        "",
    ]

    for label in CLASS_ORDER:
        lines.append(f"- `{label}`: `{class_counts[label]}`")

    lines.extend(
        [
            "",
            "## Distribucion de recta",
            "",
            f"- `recta=true`: `{recta_counts[True]}`",
            f"- `recta=false`: `{recta_counts[False]}`",
            "",
            "## Medias por variable",
            "",
        ]
    )

    for column in NUMERIC_COLUMNS:
        values = [int(row[column]) for row in rows]
        lines.append(f"- `{column}`: media `{mean(values):0.2f}`, min `{min(values)}`, max `{max(values)}`")

    lines.extend(["", "## Medias por clase", ""])
    for label in CLASS_ORDER:
        subset = [row for row in rows if row["power_up_tirado"] == label]
        if not subset:
            continue
        lines.append(f"### {label}")
        lines.append("")
        lines.append(f"- filas: `{len(subset)}`")
        lines.append(f"- recta true: `{sum(1 for row in subset if row['recta'])}`")
        for column in NUMERIC_COLUMNS:
            lines.append(f"- {column}: `{mean(int(row[column]) for row in subset):0.2f}`")
        lines.append("")

    (output_dir / "eda_summary.md").write_text("\n".join(lines), encoding="utf-8")


def plot_class_distribution(plt, rows, output_dir: Path) -> None:
    counts = Counter(str(row["power_up_tirado"]) for row in rows)
    labels = list(CLASS_ORDER)
    values = [counts[label] for label in labels]
    colors = [CLASS_COLORS[label] for label in labels]

    fig, ax = plt.subplots(figsize=(8, 5))
    ax.bar(labels, values, color=colors)
    ax.set_title("Distribucion de clases")
    ax.set_xlabel("Power-up")
    ax.set_ylabel("Frecuencia")
    for index, value in enumerate(values):
        ax.text(index, value + 0.5, str(value), ha="center", va="bottom")
    fig.tight_layout()
    fig.savefig(output_dir / "class_distribution.png", dpi=160)
    plt.close(fig)


def plot_recta_distribution(plt, rows, output_dir: Path) -> None:
    counts_by_label = defaultdict(lambda: [0, 0])
    for row in rows:
        label = str(row["power_up_tirado"])
        counts_by_label[label][0 if bool(row["recta"]) else 1] += 1

    labels = list(CLASS_ORDER)
    recta_true = [counts_by_label[label][0] for label in labels]
    recta_false = [counts_by_label[label][1] for label in labels]

    fig, ax = plt.subplots(figsize=(8, 5))
    ax.bar(labels, recta_true, label="recta=true", color="#2ecc71")
    ax.bar(labels, recta_false, bottom=recta_true, label="recta=false", color="#95a5a6")
    ax.set_title("Distribucion de recta por power-up")
    ax.set_xlabel("Power-up")
    ax.set_ylabel("Frecuencia")
    ax.legend()
    fig.tight_layout()
    fig.savefig(output_dir / "recta_distribution.png", dpi=160)
    plt.close(fig)


def plot_histograms(plt, rows, output_dir: Path) -> None:
    fig, axes = plt.subplots(2, 2, figsize=(10, 8))
    for axis, column in zip(axes.flatten(), NUMERIC_COLUMNS):
        values = [int(row[column]) for row in rows]
        axis.hist(values, bins=range(min(values), max(values) + 2), color="#3498db", edgecolor="black", align="left")
        axis.set_title(column)
        axis.set_xlabel("Valor")
        axis.set_ylabel("Frecuencia")
    fig.tight_layout()
    fig.savefig(output_dir / "numeric_histograms.png", dpi=160)
    plt.close(fig)


def plot_feature_means(plt, rows, output_dir: Path) -> None:
    labels = list(CLASS_ORDER)
    means_by_feature = {column: [] for column in NUMERIC_COLUMNS}
    for label in labels:
        subset = [row for row in rows if row["power_up_tirado"] == label]
        for column in NUMERIC_COLUMNS:
            means_by_feature[column].append(mean(int(row[column]) for row in subset) if subset else 0)

    x_positions = list(range(len(labels)))
    width = 0.18

    fig, ax = plt.subplots(figsize=(10, 6))
    for feature_index, column in enumerate(NUMERIC_COLUMNS):
        offsets = [position + (feature_index - 1.5) * width for position in x_positions]
        ax.bar(offsets, means_by_feature[column], width=width, label=column)

    ax.set_xticks(x_positions)
    ax.set_xticklabels(labels)
    ax.set_title("Media de features por power-up")
    ax.set_ylabel("Valor medio")
    ax.legend()
    fig.tight_layout()
    fig.savefig(output_dir / "feature_means_by_powerup.png", dpi=160)
    plt.close(fig)


def plot_scatter(plt, rows, output_dir: Path, x_column: str, y_column: str, filename: str, title: str) -> None:
    fig, ax = plt.subplots(figsize=(8, 6))
    for label in CLASS_ORDER:
        subset = [row for row in rows if row["power_up_tirado"] == label]
        x_values = [int(row[x_column]) for row in subset]
        y_values = [int(row[y_column]) for row in subset]
        ax.scatter(x_values, y_values, label=label, alpha=0.75, s=50, color=CLASS_COLORS[label])

    ax.set_xlabel(x_column)
    ax.set_ylabel(y_column)
    ax.set_title(title)
    ax.legend()
    fig.tight_layout()
    fig.savefig(output_dir / filename, dpi=160)
    plt.close(fig)


def main() -> None:
    default_input, default_output_dir = resolve_paths()

    parser = argparse.ArgumentParser(description="Genera un EDA con graficos para el dataset de power-ups.")
    parser.add_argument("--input", type=Path, default=default_input, help="CSV de entrada.")
    parser.add_argument("--output-dir", type=Path, default=default_output_dir, help="Directorio de salida para graficos y resumen.")
    args = parser.parse_args()

    rows = load_rows(args.input)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    write_summary(rows, args.output_dir)

    plt = try_import_matplotlib()
    if plt is None:
        print("No se ha encontrado matplotlib en la venv actual.")
        print("Instala matplotlib para generar los graficos. El resumen markdown ya se ha generado.")
        print(f"Resumen guardado en: {args.output_dir / 'eda_summary.md'}")
        return

    plot_class_distribution(plt, rows, args.output_dir)
    plot_recta_distribution(plt, rows, args.output_dir)
    plot_histograms(plt, rows, args.output_dir)
    plot_feature_means(plt, rows, args.output_dir)
    plot_scatter(plt, rows, args.output_dir, "enemigos_delante", "enemigos_atras", "scatter_enemigos.png", "Enemigos delante vs atras")
    plot_scatter(plt, rows, args.output_dir, "platanos_delante", "conchas_atras", "scatter_hazards.png", "Hazards delante vs atras")

    print(f"EDA generado en: {args.output_dir}")


if __name__ == "__main__":
    main()
