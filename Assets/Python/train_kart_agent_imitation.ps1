param(
    [string]$RunId = "kart_imitation",
    [int]$BasePort = 5005,
    [switch]$Resume
)

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$configPath = Join-Path $projectRoot "Assets\\MLAgents\\Config\\kart_agent_imitation_config.yaml"
$resultsDir = Join-Path $projectRoot "Assets\\MLAgents\\TrainingLogs"

$arguments = @(
    $configPath,
    "--run-id", $RunId,
    "--base-port", $BasePort,
    "--results-dir", $resultsDir
)

if ($Resume) {
    $arguments += "--resume"
}

Write-Host "Starting imitation-assisted training with run-id '$RunId'."
Write-Host "Make sure Assets/Data/demos/kart_demo.demo exists before running this script."
Write-Host "After the trainer starts listening, return to Unity and press Play in the training scene."
mlagents-learn @arguments
