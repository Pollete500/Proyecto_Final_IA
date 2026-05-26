from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


def resolve_project_paths() -> dict[str, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    data_dir = project_root / "Assets" / "Data"
    classifier_dir = data_dir / "classifier_rules"
    classifier_dir.mkdir(parents=True, exist_ok=True)

    return {
        "project_root": project_root,
        "data_dir": data_dir,
        "classifier_dir": classifier_dir,
        "python_executable": Path(sys.executable).resolve(),
        "augment_script": script_dir / "powerups_augment_dataset.py",
        "train_script": script_dir / "train_powerups_random_forest.py",
        "export_script": script_dir / "export_powerups_random_forest_to_json.py",
    }


def run_command(command: list[str], cwd: Path) -> None:
    print("Ejecutando:", " ".join(str(part) for part in command))
    result = subprocess.run(command, cwd=cwd, check=False)
    if result.returncode != 0:
        raise SystemExit(f"El comando fallo con codigo {result.returncode}: {' '.join(str(part) for part in command)}")


def main() -> None:
    paths = resolve_project_paths()
    parser = argparse.ArgumentParser(
        description="Genera datasets agresivo/pacifico y entrena/exporta sus Random Forest."
    )
    parser.add_argument("--target-size", type=int, default=2000, help="Numero total de filas por perfil.")
    parser.add_argument("--seed", type=int, default=42, help="Semilla base.")
    args = parser.parse_args()

    python_executable = paths["python_executable"]
    if not python_executable.exists():
        raise SystemExit(f"No existe el interprete de Python: {python_executable}")

    profiles = ("agresivo", "pacifico")
    for profile_index, profile in enumerate(profiles):
        dataset_path = paths["data_dir"] / f"powerups_{profile}_augmented.csv"
        model_path = paths["classifier_dir"] / f"powerups_random_forest_{profile}.joblib"
        json_path = paths["classifier_dir"] / f"powerups_random_forest_{profile}.json"

        run_command(
            [
                str(python_executable),
                str(paths["augment_script"]),
                "--profile",
                profile,
                "--target-size",
                str(args.target_size),
                "--seed",
                str(args.seed + profile_index),
                "--output",
                str(dataset_path),
            ],
            paths["project_root"],
        )

        run_command(
            [
                str(python_executable),
                str(paths["train_script"]),
                "--input",
                str(dataset_path),
                "--output-model",
                str(model_path),
            ],
            paths["project_root"],
        )

        run_command(
            [
                str(python_executable),
                str(paths["export_script"]),
                "--input-model",
                str(model_path),
                "--output-json",
                str(json_path),
            ],
            paths["project_root"],
        )

    print("\nPerfiles generados correctamente:")
    for profile in profiles:
        print(f"  - powerups_{profile}_augmented.csv")
        print(f"  - powerups_random_forest_{profile}.joblib")
        print(f"  - powerups_random_forest_{profile}.json")


if __name__ == "__main__":
    main()
