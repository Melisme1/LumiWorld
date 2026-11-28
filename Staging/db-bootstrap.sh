#!/usr/bin/env bash
set -euo pipefail

# Replicates database/scripts/setup.ps1 and migrate.ps1 on Linux/Docker against PostgreSQL 18.
PSQL="psql -h ${PGHOST:-db} -p ${PGPORT:-5432} -X -q -v ON_ERROR_STOP=1"

echo "[db-bootstrap] Ensuring Lumi roles and databases exist..."
$PSQL -U postgres -d postgres \
    -v "owner_pw=${LUMI_OWNER_PASSWORD}" \
    -v "app_pw=${LUMI_APP_PASSWORD}" \
    -v "report_pw=${LUMI_REPORT_PASSWORD}" \
    -f /database/setup/01_roles.sql \
    -f /database/setup/02_databases.sql

for target_db in lumi lumi_test; do
    echo "[db-bootstrap] Applying schema grants on ${target_db}..."
    $PSQL -U postgres -d "${target_db}" -f /database/setup/03_grants.sql
done

run_migrations() {
    local target_db="$1"
    echo "[db-bootstrap] Running pending migrations on ${target_db} as lumi_owner..."
    PGPASSWORD="${LUMI_OWNER_PASSWORD}" psql -h "${PGHOST:-db}" -p "${PGPORT:-5432}" \
        -U lumi_owner -d "${target_db}" -X -q -v ON_ERROR_STOP=1 \
        -c "create table if not exists schema_migrations (version text primary key, applied_at timestamptz not null default now());"

    local applied
    applied="$(PGPASSWORD="${LUMI_OWNER_PASSWORD}" psql -h "${PGHOST:-db}" -p "${PGPORT:-5432}" \
        -U lumi_owner -d "${target_db}" -X -q -tA -c "select version from schema_migrations;")"

    local count=0
    for f in /database/migrations/*.sql; do
        [ -e "$f" ] || continue
        local ver
        ver="$(basename "$f" .sql)"
        if grep -Fxq "$ver" <<< "$applied"; then
            continue
        fi
        echo "[db-bootstrap] Applying ${ver} -> ${target_db}"
        PGPASSWORD="${LUMI_OWNER_PASSWORD}" psql -h "${PGHOST:-db}" -p "${PGPORT:-5432}" \
            -U lumi_owner -d "${target_db}" -X -q -v ON_ERROR_STOP=1 \
            -1 -f "$f" -c "insert into schema_migrations (version) values ('${ver}');"
        count=$((count + 1))
    done
    echo "[db-bootstrap] ${target_db} up to date (${count} newly applied)."
}

run_migrations lumi
run_migrations lumi_test
