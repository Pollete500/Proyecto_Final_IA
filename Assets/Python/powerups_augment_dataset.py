from __future__ import annotations

import argparse
import csv
import random
from collections import Counter, defaultdict
from pathlib import Path


FIELDNAMES = (
    "recta",
    "enemigos_delante",
    "enemigos_atras",
    "platanos_delante",
    "conchas_atras",
    "power_up_tirado",
)

POWER_UPS = ("banana", "shell", "mushroom", "star")
PROFILE_TARGET_RATIOS = {
    "balanced": {"banana": 0.25, "shell": 0.25, "mushroom": 0.25, "star": 0.25},
    "agresivo": {"banana": 0.35, "shell": 0.35, "mushroom": 0.25, "star": 0.05},
    "pacifico": {"banana": 0.05, "shell": 0.05, "mushroom": 0.50, "star": 0.40},
}


def project_paths() -> tuple[Path, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    data_dir = project_root / "Assets" / "Data"
    return data_dir / "powerups_sintetico.csv", data_dir / "powerups_sintetico_augmented.csv"


def parse_bool(value: str) -> bool:
    return str(value).strip().lower() in {"true", "1", "yes", "y"}


def clamp_int(value: int, minimum: int, maximum: int) -> int:
    return max(minimum, min(maximum, int(value)))


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


def choose_power_up(
    recta: bool,
    enemigos_delante: int,
    enemigos_atras: int,
    platanos_delante: int,
    conchas_atras: int,
) -> str:
    total_hazards = platanos_delante + conchas_atras

    if total_hazards >= 4:
        return "star"

    if total_hazards >= 3 and not recta:
        return "star"

    if enemigos_delante >= enemigos_atras + 1 and enemigos_delante > 0:
        return "shell"

    if enemigos_atras >= enemigos_delante + 1 and enemigos_atras > 0:
        return "banana"

    if recta and total_hazards <= 1:
        return "mushroom"

    if total_hazards >= 2:
        return "star"

    if enemigos_delante > 0:
        return "shell"

    if enemigos_atras > 0:
        return "banana"

    return "mushroom" if recta else "star"


def jitter_base_row(base_row: dict[str, object], rng: random.Random) -> dict[str, object]:
    return {
        "recta": base_row["recta"] if rng.random() < 0.8 else not bool(base_row["recta"]),
        "enemigos_delante": clamp_int(int(base_row["enemigos_delante"]) + rng.randint(-1, 1), 0, 5),
        "enemigos_atras": clamp_int(int(base_row["enemigos_atras"]) + rng.randint(-1, 1), 0, 5),
        "platanos_delante": clamp_int(int(base_row["platanos_delante"]) + rng.randint(-1, 1), 0, 4),
        "conchas_atras": clamp_int(int(base_row["conchas_atras"]) + rng.randint(-1, 1), 0, 4),
    }


def bias_candidate_for_label(candidate: dict[str, object], label: str, rng: random.Random, profile: str) -> None:
    if label == "banana":
        candidate["enemigos_atras"] = max(int(candidate["enemigos_atras"]), rng.randint(1, 5))
        candidate["enemigos_delante"] = min(int(candidate["enemigos_delante"]), max(0, int(candidate["enemigos_atras"]) - rng.randint(1, 2)))
        candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]) + rng.randint(-1, 1), 0, 2)
        candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]) + rng.randint(-1, 1), 0, 2)
    elif label == "shell":
        candidate["enemigos_delante"] = max(int(candidate["enemigos_delante"]), rng.randint(1, 5))
        candidate["enemigos_atras"] = min(int(candidate["enemigos_atras"]), max(0, int(candidate["enemigos_delante"]) - rng.randint(1, 2)))
        candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]) + rng.randint(-1, 1), 0, 2)
        candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]) + rng.randint(-1, 1), 0, 2)
    elif label == "mushroom":
        candidate["recta"] = True
        candidate["platanos_delante"] = rng.randint(0, 1)
        candidate["conchas_atras"] = rng.randint(0, 1)
        candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]), 0, 2)
        candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]), 0, 2)
    elif label == "star":
        candidate["platanos_delante"] = max(int(candidate["platanos_delante"]), rng.randint(1, 4))
        candidate["conchas_atras"] = max(int(candidate["conchas_atras"]), rng.randint(1, 4))
        candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]), 0, 3)
        candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]), 0, 3)
        candidate["recta"] = candidate["recta"] if rng.random() < 0.4 else False

    if profile == "agresivo":
        if label in {"banana", "shell"}:
            candidate["recta"] = candidate["recta"] if rng.random() < 0.5 else False
            candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]), 0, 1)
            candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]), 0, 1)
            if label == "banana":
                candidate["enemigos_atras"] = max(int(candidate["enemigos_atras"]), rng.randint(2, 5))
            else:
                candidate["enemigos_delante"] = max(int(candidate["enemigos_delante"]), rng.randint(2, 5))
        elif label == "star":
            candidate["platanos_delante"] = max(int(candidate["platanos_delante"]), rng.randint(3, 4))
            candidate["conchas_atras"] = max(int(candidate["conchas_atras"]), rng.randint(3, 4))
            candidate["recta"] = False
    elif profile == "pacifico":
        if label == "mushroom":
            candidate["recta"] = True
            candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]), 0, 1)
            candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]), 0, 1)
            candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]), 0, 1)
            candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]), 0, 1)
        elif label == "star":
            candidate["platanos_delante"] = max(int(candidate["platanos_delante"]), rng.randint(2, 4))
            candidate["conchas_atras"] = max(int(candidate["conchas_atras"]), rng.randint(2, 4))
        elif label in {"banana", "shell"}:
            if label == "banana":
                candidate["enemigos_atras"] = max(int(candidate["enemigos_atras"]), rng.randint(1, 3))
                candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]), 0, 1)
            else:
                candidate["enemigos_delante"] = max(int(candidate["enemigos_delante"]), rng.randint(1, 3))
                candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]), 0, 1)
            candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]) + rng.randint(0, 1), 0, 2)
            candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]) + rng.randint(0, 1), 0, 2)


def bias_preserved_label_candidate(candidate: dict[str, object], label: str, rng: random.Random) -> None:
    if label == "banana":
        candidate["enemigos_atras"] = max(int(candidate["enemigos_atras"]), rng.randint(1, 4))
        candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]) + rng.randint(-1, 0), 0, 4)
        candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]) + rng.randint(-1, 1), 0, 2)
        candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]) + rng.randint(-1, 1), 0, 2)
        if rng.random() < 0.35:
            candidate["recta"] = False
    elif label == "shell":
        candidate["enemigos_delante"] = max(int(candidate["enemigos_delante"]), rng.randint(1, 4))
        candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]) + rng.randint(-1, 0), 0, 4)
        candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]) + rng.randint(-1, 1), 0, 2)
        candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]) + rng.randint(-1, 1), 0, 2)
        if rng.random() < 0.35:
            candidate["recta"] = False
    elif label == "mushroom":
        candidate["recta"] = True if rng.random() < 0.85 else bool(candidate["recta"])
        candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]), 0, 2)
        candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]), 0, 2)
        candidate["platanos_delante"] = clamp_int(int(candidate["platanos_delante"]), 0, 2)
        candidate["conchas_atras"] = clamp_int(int(candidate["conchas_atras"]), 0, 2)
    elif label == "star":
        candidate["platanos_delante"] = max(int(candidate["platanos_delante"]), rng.randint(1, 4))
        candidate["conchas_atras"] = max(int(candidate["conchas_atras"]), rng.randint(1, 4))
        candidate["enemigos_delante"] = clamp_int(int(candidate["enemigos_delante"]), 0, 3)
        candidate["enemigos_atras"] = clamp_int(int(candidate["enemigos_atras"]), 0, 3)
        if rng.random() < 0.5:
            candidate["recta"] = False


def generate_preserved_row_for_label(
    label: str,
    base_rows_by_label: dict[str, list[dict[str, object]]],
    rng: random.Random,
    max_attempts: int = 200,
) -> dict[str, object]:
    base_candidates = base_rows_by_label.get(label) or []

    for _ in range(max_attempts):
        if base_candidates:
            candidate = jitter_base_row(rng.choice(base_candidates), rng)
        else:
            candidate = {
                "recta": rng.choice([True, False]),
                "enemigos_delante": rng.randint(0, 5),
                "enemigos_atras": rng.randint(0, 5),
                "platanos_delante": rng.randint(0, 4),
                "conchas_atras": rng.randint(0, 4),
            }

        bias_preserved_label_candidate(candidate, label, rng)
        candidate["power_up_tirado"] = label
        return candidate

    raise RuntimeError(f"No se pudo generar una fila sintetica preservando la clase '{label}'.")


def generate_row_for_label(
    label: str,
    base_rows_by_label: dict[str, list[dict[str, object]]],
    rng: random.Random,
    profile: str,
    max_attempts: int = 200,
) -> dict[str, object]:
    base_candidates = base_rows_by_label.get(label) or []

    for _ in range(max_attempts):
        if base_candidates:
            candidate = jitter_base_row(rng.choice(base_candidates), rng)
        else:
            candidate = {
                "recta": rng.choice([True, False]),
                "enemigos_delante": rng.randint(0, 5),
                "enemigos_atras": rng.randint(0, 5),
                "platanos_delante": rng.randint(0, 4),
                "conchas_atras": rng.randint(0, 4),
            }

        bias_candidate_for_label(candidate, label, rng, profile)
        chosen_label = choose_power_up(
            bool(candidate["recta"]),
            int(candidate["enemigos_delante"]),
            int(candidate["enemigos_atras"]),
            int(candidate["platanos_delante"]),
            int(candidate["conchas_atras"]),
        )

        if chosen_label == label:
            candidate["power_up_tirado"] = label
            return candidate

    raise RuntimeError(f"No se pudo generar una fila sintetica coherente para la clase '{label}'.")


def build_target_counts(profile: str, target_size: int) -> dict[str, int]:
    ratios = PROFILE_TARGET_RATIOS.get(profile, PROFILE_TARGET_RATIOS["balanced"])
    target_counts = {label: int(target_size * ratios[label]) for label in POWER_UPS}
    assigned_total = sum(target_counts.values())
    remainder = target_size - assigned_total

    if remainder > 0:
        ordered_labels = sorted(
            POWER_UPS,
            key=lambda label: ratios[label] - target_counts[label] / max(1, target_size),
            reverse=True,
        )
        for remainder_index in range(remainder):
            target_counts[ordered_labels[remainder_index % len(ordered_labels)]] += 1

    return target_counts


def build_preserved_label_target_counts(rows: list[dict[str, object]], target_size: int) -> dict[str, int]:
    counts = Counter(str(row["power_up_tirado"]) for row in rows)
    total_rows = max(1, len(rows))
    if target_size <= len(rows):
        return {label: counts[label] for label in POWER_UPS}

    raw_targets = {
        label: counts[label] + (target_size - len(rows)) * (counts[label] / total_rows)
        for label in POWER_UPS
    }
    target_counts = {label: int(raw_targets[label]) for label in POWER_UPS}
    assigned_total = sum(target_counts.values())
    remainder = target_size - assigned_total

    if remainder > 0:
        ordered_labels = sorted(
            POWER_UPS,
            key=lambda label: raw_targets[label] - target_counts[label],
            reverse=True,
        )
        for remainder_index in range(remainder):
            target_counts[ordered_labels[remainder_index % len(ordered_labels)]] += 1

    for label in POWER_UPS:
        target_counts[label] = max(target_counts[label], counts[label])

    return target_counts


def choose_label_with_deficit(counts: Counter, target_counts: dict[str, int]) -> str | None:
    labels_with_deficit = [label for label in POWER_UPS if counts[label] < target_counts[label]]
    if not labels_with_deficit:
        return None

    return min(
        labels_with_deficit,
        key=lambda label: (counts[label] - target_counts[label], counts[label]),
    )


def augment_preserving_labels(
    rows: list[dict[str, object]],
    target_size: int,
    rng: random.Random,
) -> tuple[list[dict[str, object]], Counter]:
    counts = Counter(str(row["power_up_tirado"]) for row in rows)
    augmented_rows = list(rows)
    base_rows_by_label: dict[str, list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        base_rows_by_label[str(row["power_up_tirado"])].append(row)

    target_counts = build_preserved_label_target_counts(rows, target_size)
    while len(augmented_rows) < target_size:
        label_to_generate = choose_label_with_deficit(counts, target_counts)
        if label_to_generate is None:
            break

        synthetic_row = generate_preserved_row_for_label(label_to_generate, base_rows_by_label, rng)
        augmented_rows.append(synthetic_row)
        counts[label_to_generate] += 1

    return augmented_rows, counts


def write_rows(output_path: Path, rows: list[dict[str, object]]) -> None:
    with output_path.open("w", encoding="utf-8", newline="") as csv_file:
        writer = csv.DictWriter(csv_file, fieldnames=FIELDNAMES)
        writer.writeheader()
        for row in rows:
            writer.writerow(
                {
                    "recta": "true" if bool(row["recta"]) else "false",
                    "enemigos_delante": int(row["enemigos_delante"]),
                    "enemigos_atras": int(row["enemigos_atras"]),
                    "platanos_delante": int(row["platanos_delante"]),
                    "conchas_atras": int(row["conchas_atras"]),
                    "power_up_tirado": str(row["power_up_tirado"]),
                }
            )


def main() -> None:
    input_path, output_path = project_paths()

    parser = argparse.ArgumentParser(description="Aumenta el dataset sintetico de power-ups manteniendo contextos coherentes.")
    parser.add_argument("--input", type=Path, default=input_path, help="CSV de entrada.")
    parser.add_argument("--output", type=Path, default=output_path, help="CSV de salida aumentado.")
    parser.add_argument("--target-size", type=int, default=2000, help="Numero total de filas deseado.")
    parser.add_argument("--seed", type=int, default=42, help="Semilla aleatoria.")
    parser.add_argument(
        "--profile",
        choices=tuple(PROFILE_TARGET_RATIOS.keys()),
        default="balanced",
        help="Perfil de comportamiento a sesgar.",
    )
    parser.add_argument(
        "--preserve-labels",
        action="store_true",
        help="Aumenta el dataset sin cambiar la etiqueta original de cada fila.",
    )
    args = parser.parse_args()

    rng = random.Random(args.seed)
    rows = load_rows(args.input)
    if len(rows) == 0:
        raise SystemExit(f"El CSV de entrada no contiene filas: {args.input}")

    if args.preserve_labels:
        if len(rows) >= args.target_size:
            write_rows(args.output, rows)
            print(f"El dataset ya tiene {len(rows)} filas, no hace falta ampliarlo.")
            print(f"Salida escrita en: {args.output}")
            return

        augmented_rows, counts = augment_preserving_labels(rows, args.target_size, rng)
        write_rows(args.output, augmented_rows)
        print(f"Filas originales: {len(rows)}")
        print(f"Filas finales: {len(augmented_rows)}")
        print("Modo: preserve-labels")
        print("Balance final por clase:")
        for label in POWER_UPS:
            print(f"  {label}: {counts[label]}")
        print(f"CSV aumentado guardado en: {args.output}")
        return

    if len(rows) >= args.target_size:
        write_rows(args.output, rows)
        print(f"El dataset ya tiene {len(rows)} filas, no hace falta ampliarlo.")
        print(f"Salida escrita en: {args.output}")
        return

    counts = Counter(str(row["power_up_tirado"]) for row in rows)
    base_rows_by_label: dict[str, list[dict[str, object]]] = defaultdict(list)
    for row in rows:
        base_rows_by_label[str(row["power_up_tirado"])].append(row)

    augmented_rows = list(rows)
    target_counts = build_target_counts(args.profile, args.target_size)
    while len(augmented_rows) < args.target_size:
        label_to_generate = min(
            POWER_UPS,
            key=lambda label: (counts[label] - target_counts[label], counts[label]),
        )
        synthetic_row = generate_row_for_label(label_to_generate, base_rows_by_label, rng, args.profile)
        augmented_rows.append(synthetic_row)
        counts[label_to_generate] += 1

    args.output.parent.mkdir(parents=True, exist_ok=True)
    write_rows(args.output, augmented_rows)

    print(f"Filas originales: {len(rows)}")
    print(f"Filas finales: {len(augmented_rows)}")
    print(f"Perfil: {args.profile}")
    print("Balance final por clase:")
    for label in POWER_UPS:
        print(f"  {label}: {counts[label]}")
    print(f"CSV aumentado guardado en: {args.output}")


if __name__ == "__main__":
    main()
