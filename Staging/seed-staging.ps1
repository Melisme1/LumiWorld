<#
.SYNOPSIS
    Idempotent Staging Seed & Reset script for LumiWorld Defense 1.1 Demo Scenarios (WF-01 .. WF-07).
.DESCRIPTION
    1. Optionally resets gameplay data in PostgreSQL (`-Reset`) via `docker compose exec` or local `psql`.
    2. Registers/logs in deterministic staging accounts via `/api/v1/auth/register` + `/login`
       so password hashes match ASP.NET Core Identity PasswordHasher.
    3. Promotes `staging_admin` to `role='admin'` in PostgreSQL (`F-14`).
    4. Uploads valid WorldSaveData + EconomySaveData (`PUT /api/v1/saves/me`) matching exact Unity JSON fields.
    5. Publishes WorldSnapshots (`PUT /api/v1/worlds/me`) and sets `created_at` > 72h ago for obstacle testing (`WF-05`).
    6. Seeds Active Market listings (`POST /api/v1/market/listings`) for `WF-04`.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File Staging\seed-staging.ps1 -BaseUrl "http://localhost:8080" -OwnerPassword "..." -Reset
#>
param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$SeedPassword = "LumiDemo2026!",
    [string]$ComposeFile = "Staging/docker-compose.yml",
    [string]$EnvFile = "Staging/.env.staging",
    [switch]$Reset,
    [switch]$SkipDbSql
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")

function Invoke-Sql($sql) {
    if ($SkipDbSql) { return }
    if (Get-Command docker -ErrorAction SilentlyContinue) {
        if ((Test-Path $ComposeFile) -and (Test-Path $EnvFile)) {
            $sql | & docker compose -f $ComposeFile --env-file $EnvFile exec -T db psql -U postgres -d lumi -v ON_ERROR_STOP=1 -X -q
            if ($LASTEXITCODE -eq 0) { return }
        }
    }
    if ($env:LUMI_OWNER_PASSWORD -and (Get-Command psql -ErrorAction SilentlyContinue)) {
        $prev = $env:PGPASSWORD
        try {
            $env:PGPASSWORD = $env:LUMI_OWNER_PASSWORD
            $sql | & psql -h "${env:PGHOST}" -p "${env:PGPORT}" -U lumi_owner -d lumi -v ON_ERROR_STOP=1 -X -q
            if ($LASTEXITCODE -ne 0) { throw "psql command failed" }
        } finally {
            $env:PGPASSWORD = $prev
        }
        return
    }
    Write-Warning "Skipping direct DB SQL (neither running Docker Compose db nor local psql+LUMI_OWNER_PASSWORD detected)."
}

function Invoke-ApiJson($method, $path, $bodyObj = $null, $token = $null, $opKey = $null) {
    $headers = @{ "Accept" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    if ($opKey) { $headers["Idempotency-Key"] = $opKey }
    $params = @{
        Method      = $method
        Uri         = "$BaseUrl$path"
        Headers     = $headers
        ContentType = "application/json; charset=utf-8"
    }
    if ($null -ne $bodyObj) {
        $params["Body"] = ($bodyObj | ConvertTo-Json -Depth 12 -Compress)
    }
    Invoke-RestMethod @params
}

function Ensure-Account($username) {
    try {
        return Invoke-ApiJson "POST" "/api/v1/auth/register" @{
            username        = $username
            password        = $SeedPassword
            confirmPassword = $SeedPassword
        }
    } catch {
        # Fallback to login if already seeded
        return Invoke-ApiJson "POST" "/api/v1/auth/login" @{
            username = $username
            password = $SeedPassword
        }
    }
}

Write-Host "[seed] Checking staging API health at $BaseUrl/health ..."
$health = Invoke-RestMethod -Uri "$BaseUrl/health" -Method GET -TimeoutSec 10
if ($health.status -ne "ok") { throw "Unexpected /health response" }

if ($Reset) {
    Write-Host "[seed] Resetting transactional tables on lumi (preserving schema_migrations & game_config)..."
    Invoke-Sql @"
truncate table
    inbox_items,
    op_keys,
    card_trades,
    listings,
    world_snapshots,
    world_positions,
    saves,
    action_log,
    players
restart identity cascade;
alter sequence world_slot_seq restart with 0;
"@
}

$accounts = @{}
foreach ($u in @("staging_admin", "demo_vinh", "demo_toan", "demo_tan", "demo_tuananh", "demo_buyer", "demo_khang")) {
    Write-Host "[seed] Ensuring account: $u"
    $accounts[$u] = Ensure-Account $u
}

# Promote staging_admin and age demo_tan past 72h obstacle protection for WF-05 demo
Write-Host "[seed] Promoting staging_admin & aging demo_tan (>72h protection window)..."
Invoke-Sql @"
update players set role = 'admin', is_suspended = false where username = 'staging_admin';
update players set created_at = now() - interval '96 hours' where username = 'demo_tan';
"@

function New-HexTiles {
    # 7-tile starter hex patch at q,r around (0,0) matching WorldSaveData.TileRecord
    $coords = @(@(0,0), @(1,0), @(0,1), @(-1,1), @(-1,0), @(0,-1), @(1,-1))
    $tiles = @()
    foreach ($c in $coords) {
        $tiles += @{
            hasElevationLevel  = $true
            elevationLevel     = 1
            q                  = $c[0]
            r                  = $c[1]
            model              = "hex_grass_starter"
            objectName         = "Hex_$($c[0])_$($c[1])"
            terrainTypeIndex   = -1
            terrainDataVersion = 1
            hasLushEnvironment = $true
            rotationStep       = 0
            isOccupied         = $true
        }
    }
    return $tiles
}

function New-StarterEconomy([int]$coins, $extraResources = @(), $extraHand = @()) {
    $res = @(
        @{ id = "resource.leafwood.raspberries"; amount = 80 },
        @{ id = "resource.leafwood.acorns";      amount = 40 },
        @{ id = "resource.plain.wheat";          amount = 40 }
    ) + $extraResources

    $hand = @(
        @{ cardID = "leafwood_001";         count = 4 },
        @{ cardID = "bloomfield_001";       count = 4 },
        @{ cardID = "Rain";                 count = 6 },
        @{ cardID = "Medieval_Village";     count = 4 },
        @{ cardID = "River";                count = 4 },
        @{ cardID = "creature_deer";        count = 2 },
        @{ cardID = "creature_caterpillar"; count = 2 }
    ) + $extraHand

    return @{
        version              = 4
        coins                = $coins
        starterKitGranted    = $true
        resources            = $res
        openOrders           = @()
        orderRefills         = @()
        appliedInboxIds      = @()
        pendingMarketOps     = @()
        pendingTradeOps      = @()
        harmonyInitialized   = $true
        totalHarmony         = 120
        hasRankedUpAnyCreature = $false
        claimedMilestoneIds  = @()
        claimedAchievementIds = @()
        achievementProgress  = @()
        starterHandGranted   = $true
        hand                 = $hand
        craftingJobs         = @()
        savedAtUtc           = (Get-Date).ToUniversalTime().ToString("o")
        islandAccess         = @(@{ islandId = "starter"; unlocked = $true; stage = "Unlocked" })
    }
}

function Save-PlayerWorld($username, [int]$coins, $placements, $extraRes = @(), $extraHand = @(), [int]$score = 65) {
    $tok = $accounts[$username].token
    $baseRev = 0
    try {
        $existing = Invoke-ApiJson "GET" "/api/v1/saves/me" $null $tok
        $baseRev = [int]$existing.revision
    } catch { $baseRev = 0 }

    $tiles = New-HexTiles
    $world = @{
        version            = 1
        playerId           = $accounts[$username].player.id
        savedAtUtc         = (Get-Date).ToUniversalTime().ToString("o")
        initializeNewWorld = $false
        tiles              = $tiles
        placements         = $placements
        nextClusterId      = 2
        progress           = @{ saved = $true; bonusScore = 10; expansionCharges = 2; grantedMilestoneThresholds = @(); clusterCenters = @() }
        habitats           = @()
        production         = @()
        creatures          = @()
        disasters          = @{ saved = $true; instanceCounter = 0; nextSpawnInSeconds = -1; appliedInboxObstacleIds = @(); tiles = @(); active = @() }
        wild               = @{ saved = $true; intervalSeconds = 120; remainingSeconds = 60; incidents = @() }
    }
    $econ = New-StarterEconomy $coins $extraRes $extraHand
    $res = Invoke-ApiJson "PUT" "/api/v1/saves/me" @{
        baseRevision = $baseRev
        world        = $world
        economy      = $econ
    } $tok

    # Publish read-only WorldSnapshot (placed on 10-col World Map)
    $snapPlacements = @()
    foreach ($pl in $placements) {
        $snapPlacements += @{
            cardID         = $pl.cardID
            q              = $pl.q
            r              = $pl.r
            clusterId      = $pl.clusterId
            displayedScore = $pl.displayedScore
            qualityRank    = 1
            animalName     = ""
            animalStar     = 1
            animalTrait    = 0
            riverFlowStep  = -1
            riverIsOrigin  = $false
        }
    }
    Invoke-ApiJson "PUT" "/api/v1/worlds/me" @{
        version    = 1
        score      = $score
        tiles      = $tiles
        placements = $snapPlacements
        burned     = @()
    } $tok | Out-Null

    return $res
}

$vinhPlacements = @(
    @{ cardID = "leafwood_001";     q = 0; r = 0; clusterId = 1; displayedScore = 10; placementId = "pl_vinh_1"; riverFlowStep = -1; riverIsOrigin = $false },
    @{ cardID = "leafwood_001";     q = 1; r = 0; clusterId = 1; displayedScore = 10; placementId = "pl_vinh_2"; riverFlowStep = -1; riverIsOrigin = $false },
    @{ cardID = "leafwood_001";     q = 0; r = 1; clusterId = 1; displayedScore = 10; placementId = "pl_vinh_3"; riverFlowStep = -1; riverIsOrigin = $false },
    @{ cardID = "Medieval_Village"; q = -1; r = 1; clusterId = 2; displayedScore = 15; placementId = "pl_vinh_4"; riverFlowStep = -1; riverIsOrigin = $false }
)
$tanPlacements = @(
    @{ cardID = "bloomfield_001"; q = 0; r = 0; clusterId = 1; displayedScore = 10; placementId = "pl_tan_1"; riverFlowStep = -1; riverIsOrigin = $false },
    @{ cardID = "bloomfield_001"; q = 1; r = 0; clusterId = 1; displayedScore = 10; placementId = "pl_tan_2"; riverFlowStep = -1; riverIsOrigin = $false },
    @{ cardID = "bloomfield_001"; q = 0; r = 1; clusterId = 1; displayedScore = 10; placementId = "pl_tan_3"; riverFlowStep = -1; riverIsOrigin = $false }
)

Write-Host "[seed] Uploading baseline cloud saves & publishing WorldSnapshots..."
Save-PlayerWorld "demo_vinh"    500 $vinhPlacements -score 95  | Out-Null
Save-PlayerWorld "demo_toan"    500 $tanPlacements  -score 80  | Out-Null
Save-PlayerWorld "demo_tan"     600 $tanPlacements  -score 110 | Out-Null
Save-PlayerWorld "demo_tuananh" 500 $tanPlacements  -score 85  | Out-Null
Save-PlayerWorld "demo_buyer"   800 $tanPlacements  -score 70  | Out-Null
Save-PlayerWorld "demo_khang"   600 $tanPlacements  -score 75  | Out-Null

# Seed active Market lots idempotently for WF-04 demo
$sellerTok = $accounts["demo_tuananh"].token
$opBase = if ($Reset) { "seed-reset-$(Get-Date -Format 'yyyyMMddHHmmss')" } else { "seed-v1" }
try {
    Invoke-ApiJson "POST" "/api/v1/market/listings" @{
        resourceId = "resource.leafwood.acorns"
        amount     = 10
        priceCoins = 25
    } $sellerTok "$opBase-acorns" | Out-Null
    Invoke-ApiJson "POST" "/api/v1/market/listings" @{
        resourceId = "card.creature_deer"
        amount     = 1
        priceCoins = 50
    } $sellerTok "$opBase-deer" | Out-Null
} catch {
    Write-Warning "Market seed skipped or already active: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "================ STAGING SEED COMPLETE ================" -ForegroundColor Green
Write-Host "Base URL      : $BaseUrl"
Write-Host "Admin Portal  : $BaseUrl/admin/"
Write-Host "Password (all): $SeedPassword"
Write-Host "Accounts      : staging_admin (ADMIN), demo_vinh, demo_toan, demo_tan (>72h), demo_tuananh (Seller), demo_buyer (800 Coins), demo_khang"
Write-Host "=======================================================" -ForegroundColor Green
