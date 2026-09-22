$ErrorActionPreference = 'Stop'
Set-Location C:\Users\User\repos\AIConnect4
Get-Process -Name AIConnect4.App -ErrorAction SilentlyContinue | Stop-Process -Force
$ctrl = Join-Path (Get-Location) '.cursor\skills\verify-ai-connect4\helpers\control-c4.ps1'
$runId = 'u5ready' + (Get-Date -Format 'HHmmss')
$art = Join-Path (Resolve-Path '.\.cursor\skills\verify-ai-connect4\artifacts').Path $runId
Write-Host "RUNID=$runId"
& $ctrl launch -RunId $runId -WithoutApiKey | Out-Host
& $ctrl doctor | Out-Host
& $ctrl wait-name -Name 'Ready' | Out-Host
& $ctrl assert-text -Contains 'Wins: 0' | Out-Host
$play = & $ctrl get-enabled -Name 'Play' | ConvertFrom-Json
if ($play.enabled -ne $false) { throw 'Play should be disabled' }
& $ctrl snapshot -Path (Join-Path $art 'tree.uia.txt') | Out-Host
& $ctrl screenshot -Path (Join-Path $art 'ready.png') | Out-Host
@(
  "runId=$runId"
  'feature=ready-state'
  'entry=launch -WithoutApiKey'
  "playEnabled=$($play.enabled)"
  "host=$env:COMPUTERNAME"
  "provenUtc=$((Get-Date).ToUniversalTime().ToString('o'))"
  'capture=PrintWindow PW_RENDERFULLCONTENT'
) | Set-Content (Join-Path $art 'proof.txt') -Encoding utf8
& $ctrl cleanup | Out-Host
$store = 'C:\Users\User\AppData\Local\Cursor\AgentStores\cursor_agent_stores\bc-6b3e571a-60a2-441d-988c-ffbccda83509\files\media\u5'
New-Item -ItemType Directory -Force -Path $store | Out-Null
Copy-Item (Join-Path $art 'ready.png') (Join-Path $store 'ready.png') -Force
Copy-Item (Join-Path $art 'tree.uia.txt') (Join-Path $store 'tree.uia.txt') -Force
Copy-Item (Join-Path $art 'proof.txt') (Join-Path $store 'proof.txt') -Force
$sz = (Get-Item (Join-Path $store 'ready.png')).Length
$survive = Test-Path (Join-Path $art 'ready.png')
Write-Host "EVIDENCE_OK size=$sz survive=$survive runId=$runId"
