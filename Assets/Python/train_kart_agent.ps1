param(
    [string]$RunId = "kart_ppo",
    [int]$BasePort = 5005,
    [switch]$Resume,
    [switch]$Force
)

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$configPath = Join-Path $projectRoot "Assets\\MLAgents\\Config\\kart_agent_config.yaml"
$resultsDir = Join-Path $projectRoot "Assets\\MLAgents\\TrainingLogs"

$arguments = @(
    $configPath,
    "--run-id", $RunId,
    "--base-port", $BasePort,
    "--results-dir", $resultsDir
)

if ($Resume) { $arguments += "--resume" }
if ($Force)  { $arguments += "--force" }

Write-Host ""
Write-Host "Run ID   : $RunId"
Write-Host "Config   : $configPath"
Write-Host "Logs dir : $resultsDir"
Write-Host ""
Write-Host "WAIT for 'Listening on port...' before pressing Play in Unity."
Write-Host ""
mlagents-learn @arguments
