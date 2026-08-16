#!/bin/bash
# =============================================================================
# auto-deploy.sh
# Watches the public git repo for new commits and redeploys automatically.
# Run once — loops forever. Managed by auto-deploy.service (systemd).
#
# SETUP (one-time, on the server):
#   1. Edit the variables in the "Configuration" section below.
#   2. Allow passwordless sudo for the restart command:
#        sudo visudo
#        # Add this line (replace "youruser" with the OS user running this script):
#        youruser ALL=(ALL) NOPASSWD: /bin/systemctl restart backend
#   3. Install the systemd unit:
#        sudo cp auto-deploy.service /etc/systemd/system/
#        sudo systemctl daemon-reload
#        sudo systemctl enable --now auto-deploy
# =============================================================================

set -uo pipefail

# ── Configuration ─────────────────────────────────────────────────────────────
# VPS runs from FlowentraApp_new (see stack traces in journalctl). Use that path here.
REPO_DIR="/home/backend/FlowentraApp_new/Backend"   # absolute path to cloned repo
PUBLISH_DIR="/home/backend/publish"             # dotnet publish output
SERVICE_NAME="backend"                          # systemd service to restart
BRANCH="main"                                   # branch to track
POLL_INTERVAL=30                                # seconds between git checks
LOG_FILE="/var/log/auto-deploy.log"             # set to "" to log to stdout only
# ──────────────────────────────────────────────────────────────────────────────

log() {
    local msg="$(date '+%Y-%m-%d %H:%M:%S') [auto-deploy] $*"
    echo "$msg"
    if [ -n "$LOG_FILE" ]; then
        echo "$msg" >> "$LOG_FILE"
    fi
}

deploy() {
    log "Change detected — starting deployment"

    log "git pull origin $BRANCH"
    git -C "$REPO_DIR" pull origin "$BRANCH" || { log "ERROR: git pull failed"; return 1; }

    log "Removing build artifacts (obj/ bin/)"
    rm -rf "$REPO_DIR/obj" "$REPO_DIR/bin"

    log "dotnet restore"
    dotnet restore "$REPO_DIR" || { log "ERROR: dotnet restore failed"; return 1; }

    log "dotnet publish -c Release -o $PUBLISH_DIR"
    dotnet publish "$REPO_DIR" -c Release -o "$PUBLISH_DIR" || { log "ERROR: dotnet publish failed"; return 1; }

    log "sudo systemctl restart $SERVICE_NAME"
    sudo systemctl restart "$SERVICE_NAME" || { log "ERROR: systemctl restart failed"; return 1; }

    log "Deployment complete ✓"
}

# ── Startup checks ────────────────────────────────────────────────────────────
if [ ! -d "$REPO_DIR/.git" ]; then
    log "ERROR: $REPO_DIR is not a git repository. Clone the repo there first."
    exit 1
fi

log "Watcher started — monitoring '$BRANCH' in $REPO_DIR every ${POLL_INTERVAL}s"

# ── Main loop ─────────────────────────────────────────────────────────────────
while true; do
    # Fetch remote state without touching the working tree
    if ! git -C "$REPO_DIR" fetch --quiet origin "$BRANCH" 2>/dev/null; then
        log "WARNING: git fetch failed (network issue?), will retry in ${POLL_INTERVAL}s"
        sleep "$POLL_INTERVAL"
        continue
    fi

    LOCAL=$(git -C "$REPO_DIR" rev-parse HEAD)
    REMOTE=$(git -C "$REPO_DIR" rev-parse "origin/$BRANCH")

    if [ "$LOCAL" != "$REMOTE" ]; then
        log "New commit detected: $LOCAL -> $REMOTE"
        deploy || log "WARNING: deployment failed — will retry on next change"
    else
        log "Up to date ($LOCAL)"
    fi

    sleep "$POLL_INTERVAL"
done
