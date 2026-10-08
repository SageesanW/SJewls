#!/bin/bash
# ==============================================================================
# SJewls API Local Development Server Launcher
# Ensures port 5230 is free before starting dotnet run
# ==============================================================================

PORT=5230

# Check if port 5230 is occupied
OCCUPYING_PID=$(lsof -ti :$PORT 2>/dev/null || true)
if [ -n "$OCCUPYING_PID" ]; then
  echo "⚠️  Port $PORT is currently in use by PID $OCCUPYING_PID."
  echo "🧹 Terminating stale process to free port $PORT..."
  kill -9 $OCCUPYING_PID 2>/dev/null || true
  sleep 1
  echo "✅ Port $PORT is now available."
fi

# Set .NET path if available in user home
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
if [ -d "$DOTNET_ROOT" ]; then
  export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"
fi

echo "🚀 Starting SJewls API on http://localhost:5230..."
cd "$(dirname "$0")"
exec dotnet run --project src/SJewls.Api --launch-profile http "$@"
