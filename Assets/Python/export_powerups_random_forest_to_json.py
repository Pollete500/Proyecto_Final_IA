from __future__ import annotations

import argparse
import json
from pathlib import Path


def resolve_default_paths() -> tuple[Path, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    data_dir = project_root / "Assets" / "Data" / "classifier_rules"
    data_dir.mkdir(parents=True, exist_ok=True)
    return data_dir / "powerups_random_forest.joblib", data_dir / "powerups_random_forest.json"


def import_dependencies():
    try:
        import joblib  # type: ignore
        import numpy as np  # type: ignore
    except ImportError as exc:
        missing_module = str(exc).split("'")[1] if "'" in str(exc) else str(exc)
        raise SystemExit(
            "Faltan dependencias para exportar el Random Forest. "
            f"Modulo no encontrado: {missing_module}. "
            "Instala joblib y numpy en la venv."
        ) from exc

    return joblib, np


def main() -> None:
    default_input, default_output = resolve_default_paths()

    parser = argparse.ArgumentParser(
        description="Exporta un RandomForest entrenado en scikit-learn a JSON para usarlo desde Unity."
    )
    parser.add_argument("--input-model", type=Path, default=default_input, help="Archivo .joblib de entrada.")
    parser.add_argument("--output-json", type=Path, default=default_output, help="Archivo .json de salida.")
    args = parser.parse_args()

    joblib, np = import_dependencies()

    if not args.input_model.exists():
        raise SystemExit(f"No existe el modelo de entrada: {args.input_model}")

    bundle = joblib.load(args.input_model)
    model = bundle["model"] if isinstance(bundle, dict) and "model" in bundle else bundle
    class_labels = bundle.get("class_names") if isinstance(bundle, dict) else None
    feature_columns = bundle.get("feature_columns") if isinstance(bundle, dict) else None

    if model is None or not hasattr(model, "estimators_"):
        raise SystemExit("El archivo de entrada no contiene un RandomForestClassifier valido.")

    if not class_labels:
        class_labels = list(getattr(model, "classes_", []))
    if not class_labels:
        raise SystemExit("No se han encontrado etiquetas de clase en el modelo.")

    export_payload = {
        "classLabels": [str(label) for label in class_labels],
        "featureColumns": list(feature_columns) if feature_columns is not None else [],
        "trees": [],
    }

    for estimator in model.estimators_:
        tree = estimator.tree_
        predicted_class_indices = np.argmax(tree.value[:, 0, :], axis=1).astype(int).tolist()

        export_payload["trees"].append(
            {
                "featureIndices": tree.feature.astype(int).tolist(),
                "thresholds": [float(value) for value in tree.threshold.tolist()],
                "leftChildren": tree.children_left.astype(int).tolist(),
                "rightChildren": tree.children_right.astype(int).tolist(),
                "predictedClassIndices": predicted_class_indices,
            }
        )

    args.output_json.parent.mkdir(parents=True, exist_ok=True)
    with args.output_json.open("w", encoding="utf-8") as json_file:
        json.dump(export_payload, json_file, ensure_ascii=False, indent=2)

    print(f"Modelo exportado a JSON en: {args.output_json}")


if __name__ == "__main__":
    main()
