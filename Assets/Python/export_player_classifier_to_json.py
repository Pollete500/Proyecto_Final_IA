from __future__ import annotations

import argparse
import json
from pathlib import Path


def resolve_default_paths() -> tuple[Path, Path, Path, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    models_dir = project_root / "Assets" / "Python" / "player_classifier" / "models"
    output_dir = project_root / "Assets" / "Data" / "classifier_rules"
    output_dir.mkdir(parents=True, exist_ok=True)
    return (
        models_dir / "best_model.joblib",
        models_dir / "scaler.joblib",
        models_dir / "label_encoder.joblib",
        output_dir / "player_classifier.json",
    )


def import_dependencies():
    try:
        import joblib  # type: ignore
        import numpy as np  # type: ignore
    except ImportError as exc:
        missing_module = str(exc).split("'")[1] if "'" in str(exc) else str(exc)
        raise SystemExit(
            "Faltan dependencias para exportar el clasificador. "
            f"Modulo no encontrado: {missing_module}. "
            "Instala joblib y numpy en la venv."
        ) from exc
    return joblib, np


# These are the 9 raw columns recorded by PlayerLapDataRecorder (aggregated per race).
# Unity only needs to provide these — the engineered features are computed here during export
# and their scaler parameters are included so C# can replicate the full pipeline.
RAW_FEATURE_COLS = [
    "lapTime_delta",
    "position_delta",
    "mapCollisions_total",
    "shellsUsed_total",
    "bananasUsed_total",
    "mushroomsUsed_total",
    "starsUsed_total",
    "bananaHitsReceived_total",
    "shellHitsReceived_total",
]

# Full feature list fed to the model (raw + engineered).
ALL_FEATURE_COLS = RAW_FEATURE_COLS + [
    "aggressiveness",
    "hit_ratio",
    "powerup_efficiency",
]


def main() -> None:
    default_model, default_scaler, default_encoder, default_output = resolve_default_paths()

    parser = argparse.ArgumentParser(
        description="Exporta el clasificador de jugador a JSON para usarlo desde Unity."
    )
    parser.add_argument("--model",   type=Path, default=default_model,   help="best_model.joblib")
    parser.add_argument("--scaler",  type=Path, default=default_scaler,  help="scaler.joblib")
    parser.add_argument("--encoder", type=Path, default=default_encoder, help="label_encoder.joblib")
    parser.add_argument("--output",  type=Path, default=default_output,  help="JSON de salida.")
    args = parser.parse_args()

    joblib, np = import_dependencies()

    for path in (args.model, args.scaler, args.encoder):
        if not path.exists():
            raise SystemExit(f"Archivo no encontrado: {path}")

    model   = joblib.load(args.model)
    scaler  = joblib.load(args.scaler)
    encoder = joblib.load(args.encoder)

    if not hasattr(model, "estimators_"):
        raise SystemExit("El modelo no es un RandomForestClassifier valido.")

    if len(scaler.mean_) != len(ALL_FEATURE_COLS):
        raise SystemExit(
            f"El scaler tiene {len(scaler.mean_)} features pero se esperaban {len(ALL_FEATURE_COLS)}."
        )

    # --- Build export payload ---
    # Unity will:
    #   1. Receive the 9 raw values from PlayerLapDataRecorder
    #   2. Compute the 3 engineered features using the formulas below
    #   3. Normalize all 12 values using scalerMean / scalerScale
    #   4. Run the normalized vector through the trees
    #   5. Take majority vote → class label

    export_payload = {
        "classLabels": [str(label) for label in encoder.classes_],
        "rawFeatureColumns": RAW_FEATURE_COLS,
        "allFeatureColumns": ALL_FEATURE_COLS,
        "engineeredFeatures": {
            "aggressiveness": "shellsUsed_total + bananasUsed_total",
            "hit_ratio": "(bananaHitsReceived_total + shellHitsReceived_total) / (aggressiveness + 1)",
            "powerup_efficiency": "aggressiveness / (mapCollisions_total + 1)",
        },
        "scalerMean":  [float(v) for v in scaler.mean_],
        "scalerScale": [float(v) for v in scaler.scale_],
        "trees": [],
    }

    for estimator in model.estimators_:
        tree = estimator.tree_
        predicted_class_indices = np.argmax(tree.value[:, 0, :], axis=1).astype(int).tolist()
        export_payload["trees"].append(
            {
                "featureIndices":      tree.feature.astype(int).tolist(),
                "thresholds":          [float(v) for v in tree.threshold.tolist()],
                "leftChildren":        tree.children_left.astype(int).tolist(),
                "rightChildren":       tree.children_right.astype(int).tolist(),
                "predictedClassIndices": predicted_class_indices,
            }
        )

    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open("w", encoding="utf-8") as f:
        json.dump(export_payload, f, ensure_ascii=False, indent=2)

    n_trees = len(export_payload["trees"])
    n_classes = len(export_payload["classLabels"])
    print(f"Exportado: {args.output}")
    print(f"  Clases ({n_classes}): {export_payload['classLabels']}")
    print(f"  Arboles: {n_trees}")
    print(f"  Features: {len(ALL_FEATURE_COLS)} ({len(RAW_FEATURE_COLS)} raw + 3 engineered)")
    print(f"  Scaler incluido: media y escala por feature")


if __name__ == "__main__":
    main()
