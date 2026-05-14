param(
    [string]$RunId = "kart_ppo",
    [int]$BasePort = 5005,
    [switch]$Resume,
    [switch]$Force
)

$projectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$configPath  = Join-Path $projectRoot "Assets\MLAgents\Config\kart_agent_config.yaml"
$resultsDir  = Join-Path $projectRoot "Assets\MLAgents\TrainingLogs"

# Use the project-local venv so we always get the right Python/mlagents version
# regardless of what is on the system PATH.
$venvPython  = Join-Path $projectRoot ".venv_mlagents\Scripts\python.exe"
if (-not (Test-Path $venvPython))
{
    Write-Error "venv not found at $venvPython`nCreate it with: py -3.10 -m venv .venv_mlagents && .venv_mlagents\Scripts\pip install mlagents==1.1.0"
    exit 1
}

$arguments = @(
    "-m", "mlagents.trainers.learn",
    $configPath,
    "--run-id", $RunId,
    "--base-port", $BasePort,
    "--results-dir", $resultsDir
)

if ($Resume) { $arguments += "--resume" }
if ($Force)  { $arguments += "--force" }

Write-Host ""
Write-Host "Python   : $venvPython"
Write-Host "Run ID   : $RunId"
Write-Host "Config   : $configPath"
Write-Host "Logs dir : $resultsDir"
Write-Host ""
Write-Host "WAIT for 'Listening on port...' before pressing Play in Unity."
Write-Host ""
& $venvPython @arguments
