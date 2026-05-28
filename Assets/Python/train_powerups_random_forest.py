from __future__ import annotations

import argparse
from pathlib import Path


def resolve_default_paths() -> tuple[Path, Path]:
    script_dir = Path(__file__).resolve().parent
    project_root = script_dir.parent.parent
    data_dir = project_root / "Assets" / "Data"
    model_dir = data_dir / "classifier_rules"
    model_dir.mkdir(parents=True, exist_ok=True)
    return data_dir / "powerups_sintetico_augmented.csv", model_dir / "powerups_random_forest.joblib"


def import_dependencies():
    try:
        import joblib  # type: ignore
        import pandas as pd  # type: ignore
        from sklearn.ensemble import RandomForestClassifier  # type: ignore
        from sklearn.metrics import accuracy_score, classification_report, confusion_matrix  # type: ignore
        from sklearn.model_selection import train_test_split  # type: ignore
    except ImportError as exc:
        missing_module = str(exc).split("'")[1] if "'" in str(exc) else str(exc)
        raise SystemExit(
            "Faltan dependencias para entrenar el Random Forest. "
            f"Modulo no encontrado: {missing_module}. "
            "Instala pandas, scikit-learn y joblib en la venv."
        ) from exc

    return joblib, pd, RandomForestClassifier, accuracy_score, classification_report, confusion_matrix, train_test_split


def main() -> None:
    default_input, default_model = resolve_default_paths()

    parser = argparse.ArgumentParser(description="Entrena un RandomForest para decidir power-ups y guarda el modelo.")
    parser.add_argument("--input", type=Path, default=default_input, help="CSV de entrenamiento.")
    parser.add_argument("--output-model", type=Path, default=default_model, help="Archivo de salida para el modelo.")
    parser.add_argument("--test-size", type=float, default=0.2, help="Porcentaje de test.")
    parser.add_argument("--random-state", type=int, default=42, help="Semilla.")
    parser.add_argument("--n-estimators", type=int, default=300, help="Numero de arboles.")
    parser.add_argument("--max-depth", type=int, default=10, help="Profundidad maxima.")
    parser.add_argument("--min-samples-leaf", type=int, default=2, help="Minimo de muestras por hoja.")
    parser.add_argument(
        "--class-weight",
        choices=("balanced", "none"),
        default="balanced",
        help="Ponderacion de clases del Random Forest.",
    )
    args = parser.parse_args()

    (
        joblib,
        pd,
        RandomForestClassifier,
        accuracy_score,
        classification_report,
        confusion_matrix,
        train_test_split,
    ) = import_dependencies()

    if not args.input.exists():
        raise SystemExit(f"No existe el CSV de entrada: {args.input}")

    df = pd.read_csv(args.input)
    if len(df) < 2:
        raise SystemExit("El dataset necesita al menos 2 filas para entrenar el modelo.")

    expected_columns = [
        "recta",
        "enemigos_delante",
        "enemigos_atras",
        "platanos_delante",
        "conchas_atras",
        "power_up_tirado",
    ]

    missing_columns = [column for column in expected_columns if column not in df.columns]
    if missing_columns:
        raise SystemExit(f"Faltan columnas en el CSV: {missing_columns}")

    df["recta"] = df["recta"].astype(str).str.lower().map({"true": 1, "false": 0})
    if df["recta"].isnull().any():
        raise SystemExit("La columna 'recta' contiene valores no validos. Usa true/false.")

    feature_columns = [
        "recta",
        "enemigos_delante",
        "enemigos_atras",
        "platanos_delante",
        "conchas_atras",
    ]
    target_column = "power_up_tirado"

    X = df[feature_columns]
    y = df[target_column]

    class_counts = y.value_counts()
    estimated_test_samples = max(1, int(round(len(df) * args.test_size)))
    can_stratify = (
        len(class_counts) > 1
        and int(class_counts.min()) >= 2
        and estimated_test_samples >= len(class_counts)
    )

    X_train, X_test, y_train, y_test = train_test_split(
        X,
        y,
        test_size=args.test_size,
        random_state=args.random_state,
        stratify=y if can_stratify else None,
    )

    class_weight = None if args.class_weight == "none" else "balanced"
    model = RandomForestClassifier(
        n_estimators=args.n_estimators,
        max_depth=args.max_depth,
        min_samples_leaf=args.min_samples_leaf,
        class_weight=class_weight,
        random_state=args.random_state,
        n_jobs=-1,
    )

    model.fit(X_train, y_train)

    y_pred = model.predict(X_test)
    accuracy = accuracy_score(y_test, y_pred)

    print("=== Random Forest entrenado ===")
    print(f"CSV de entrada: {args.input}")
    print(f"Filas totales: {len(df)}")
    print(f"Train: {len(X_train)} | Test: {len(X_test)}")
    print(f"Stratified split: {can_stratify}")
    print(f"Accuracy test: {accuracy:.4f}")
    print("\nFeature importances:")
    for feature_name, importance in zip(feature_columns, model.feature_importances_):
        print(f"  {feature_name}: {importance:.4f}")

    print("\nClassification report:")
    print(classification_report(y_test, y_pred))

    print("Confusion matrix:")
    print(confusion_matrix(y_test, y_pred))

    args.output_model.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(
        {
            "model": model,
            "feature_columns": feature_columns,
            "target_column": target_column,
            "class_names": sorted(y.unique().tolist()),
        },
        args.output_model,
    )

    print(f"\nModelo guardado en: {args.output_model}")


if __name__ == "__main__":
    main()
