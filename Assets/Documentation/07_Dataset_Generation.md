# Dataset Generation

## Status

Planned. Not implemented yet in this pass.

## Planned Outputs

- `Assets/Data/behavior_dataset.csv`
- `Assets/Data/synthetic_behavior_dataset.csv`
- `Assets/Data/race_logs/`
- `Assets/Data/classifier_rules/`

## Planned Python Scripts

- `generate_synthetic_dataset.py`
- `train_behavior_classifier.py`
- `export_tree_rules.py`

## Planned Data Flow

1. Generate synthetic samples.
2. Record real race samples from Unity.
3. Merge or compare datasets.
4. Train a lightweight classifier.
5. Export rules back into C#.

## Next Update

Document final CSV columns and training commands when the dataset pipeline is created.
