from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


def resolve_project_paths() -> dict[str, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    data_dir = project_root / "Assets" / "Data"
    player_data_dir = data_dir / "PlayerPowerUpInteractions"
    classifier_dir = data_dir / "classifier_rules" / "ImitarPlayer"
    player_data_dir.mkdir(parents=True, exist_ok=True)
    classifier_dir.mkdir(parents=True, exist_ok=True)

    return {
        "project_root": project_root,
        "data_dir": data_dir,
        "player_data_dir": player_data_dir,
        "classifier_dir": classifier_dir,
        "python_executable": Path(sys.executable).resolve(),
        "augment_script": script_dir / "powerups_augment_dataset.py",
        "train_script": script_dir / "train_powerups_random_forest.py",
        "export_script": script_dir / "export_powerups_random_forest_to_json.py",
    }


def resolve_default_input_path(paths: dict[str, Path]) -> Path:
    laps_root = paths["player_data_dir"] / "Laps"
    lap_candidates = [path for path in laps_root.rglob("*.csv") if path.is_file()]
    if lap_candidates:
        return max(lap_candidates, key=lambda path: path.stat().st_mtime)

    return paths["player_data_dir"] / "player_powerup_interactions.csv"


def run_command(command: list[str], cwd: Path) -> None:
    print("Ejecutando:", " ".join(str(part) for part in command))
    result = subprocess.run(command, cwd=cwd, check=False)
    if result.returncode != 0:
        raise SystemExit(f"El comando fallo con codigo {result.returncode}: {' '.join(str(part) for part in command)}")


def main() -> None:
    paths = resolve_project_paths()
    parser = argparse.ArgumentParser(
        description="Construye el perfil de comportamiento del jugador para power-ups."
    )
    parser.add_argument(
        "--input",
        type=Path,
        default=resolve_default_input_path(paths),
        help="CSV de una vuelta del jugador. Por defecto usa la ultima vuelta disponible.",
    )
    parser.add_argument(
        "--augmented-output",
        type=Path,
        default=paths["player_data_dir"] / "player_powerup_interactions_augmented.csv",
        help="CSV aumentado preservando etiquetas.",
    )
    parser.add_argument(
        "--output-model",
        type=Path,
        default=paths["classifier_dir"] / "powerups_random_forest_player.joblib",
        help="Archivo del modelo entrenado.",
    )
    parser.add_argument(
        "--output-json",
        type=Path,
        default=paths["classifier_dir"] / "powerups_random_forest_player.json",
        help="Export JSON para Unity.",
    )
    parser.add_argument("--target-size", type=int, default=2000, help="Numero total de filas tras el augment.")
    parser.add_argument("--seed", type=int, default=42, help="Semilla aleatoria.")
    args = parser.parse_args()

    python_executable = paths["python_executable"]
    if not python_executable.exists():
        raise SystemExit(f"No existe el interprete de Python: {python_executable}")

    if not args.input.exists():
        raise SystemExit(f"No existe el CSV del jugador: {args.input}")

    run_command(
        [
            str(python_executable),
            str(paths["augment_script"]),
            "--input",
            str(args.input),
            "--output",
            str(args.augmented_output),
            "--target-size",
            str(args.target_size),
            "--seed",
            str(args.seed),
            "--preserve-labels",
        ],
        paths["project_root"],
    )

    run_command(
        [
            str(python_executable),
            str(paths["train_script"]),
            "--input",
            str(args.augmented_output),
            "--output-model",
            str(args.output_model),
            "--class-weight",
            "none",
        ],
        paths["project_root"],
    )

    run_command(
        [
            str(python_executable),
            str(paths["export_script"]),
            "--input-model",
            str(args.output_model),
            "--output-json",
            str(args.output_json),
        ],
        paths["project_root"],
    )

    print("\nPerfil del jugador generado correctamente:")
    print(f"  - {args.augmented_output.name}")
    print(f"  - {args.output_model.name}")
    print(f"  - {args.output_json.name}")


if __name__ == "__main__":
    main()
