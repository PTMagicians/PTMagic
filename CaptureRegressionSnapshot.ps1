param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('OLD','NEW')]
    [string]$Label
)

$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$dataDir = Join-Path $root '_data'
$summaryPath = Join-Path $dataDir 'LastRuntimeSummary.json'
$exchangeDir = Join-Path $dataDir 'Exchange'

if (-not (Test-Path $summaryPath)) {
    throw "LastRuntimeSummary.json not found: $summaryPath"
}

$summary = Get-Content $summaryPath -Raw | ConvertFrom-Json

$marketDataFile = Get-ChildItem $exchangeDir -Filter 'MarketData_*.json' -File |
    Sort-Object LastWriteTimeUtc -Descending |
    Select-Object -First 1

if (-not $marketDataFile) {
    throw "No MarketData_*.json file found in $exchangeDir"
}

$marketData = Get-Content $marketDataFile.FullName -Raw | ConvertFrom-Json
$exchangeMarkets = @($marketData.PSObject.Properties.Name | Sort-Object)

$marketTrends = [ordered]@{}
if ($summary.MarketTrendChanges) {
    foreach ($trendProperty in $summary.MarketTrendChanges.PSObject.Properties | Sort-Object Name) {
        $markets = @($trendProperty.Value | ForEach-Object { $_.Market } | Where-Object { $_ } | Sort-Object)
        $marketTrends[$trendProperty.Name] = [ordered]@{
            Count = $markets.Count
            Markets = $markets
        }
    }
}

$marketSummaryMarkets = @()
$activeSms = [ordered]@{}
if ($summary.MarketSummary) {
    $marketSummaryMarkets = @($summary.MarketSummary.PSObject.Properties.Name | Sort-Object)
    foreach ($marketProperty in $summary.MarketSummary.PSObject.Properties | Sort-Object Name) {
        $settings = @($marketProperty.Value.ActiveSingleSettings | ForEach-Object { $_.SettingName } | Where-Object { $_ } | Sort-Object)
        if ($settings.Count -gt 0) {
            $activeSms[$marketProperty.Name] = $settings
        }
    }
}

$effectiveTypesPath = Join-Path $exchangeDir 'BinanceFuturesAnalyzerUnderlyingTypes.json'
$effectiveTypes = @()
if (Test-Path $effectiveTypesPath) {
    $effectiveTypes = @(Get-Content $effectiveTypesPath -Raw | ConvertFrom-Json | Sort-Object)
}

$snapshot = [ordered]@{
    Label = $Label
    CapturedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    SourceSummaryLastWriteUtc = (Get-Item $summaryPath).LastWriteTimeUtc.ToString('o')
    SourceMarketDataFile = $marketDataFile.Name
    SourceMarketDataLastWriteUtc = $marketDataFile.LastWriteTimeUtc.ToString('o')
    ActiveGlobalSetting = $summary.CurrentGlobalSetting.SettingName
    ExchangeMarketCount = $exchangeMarkets.Count
    ExchangeMarkets = $exchangeMarkets
    MarketSummaryCount = $marketSummaryMarkets.Count
    MarketSummaryMarkets = $marketSummaryMarkets
    MarketTrends = $marketTrends
    ActiveSingleMarketSettingMarketCount = $activeSms.Count
    ActiveSingleMarketSettings = $activeSms
    EffectiveUnderlyingTypes = $effectiveTypes
}

$outputPath = Join-Path $dataDir ("RegressionSnapshot-{0}.json" -f $Label)
$snapshot | ConvertTo-Json -Depth 20 | Set-Content $outputPath -Encoding UTF8

Write-Host "Regression snapshot written: $outputPath"
Write-Host "Exchange markets: $($exchangeMarkets.Count)"
Write-Host "Market summary markets: $($marketSummaryMarkets.Count)"
Write-Host "Markets with active SMS: $($activeSms.Count)"
Write-Host "Active global setting: $($summary.CurrentGlobalSetting.SettingName)"
