#!/bin/bash
# ==============================================================================
# Helper to kill any stale process holding a port (default: 5230)
# ==============================================================================

PORT=${1:-5230}
PID=$(lsof -ti :$PORT 2>/dev/null || true)

if [ -n "$PID" ]; then
  echo "Found process (PID $PID) holding port $PORT."
  kill -9 $PID 2>/dev/null || true
  sleep 0.5
  echo "✅ Terminated process $PID. Port $PORT is now free."
else
  echo "✅ Port $PORT is already free."
fi
