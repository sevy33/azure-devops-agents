#!/usr/bin/env bash
# Start both the .NET API and the Angular dev server concurrently.
# Usage: ./dev.sh

set -e

BACKEND_DIR="src/backend/AzureDevOpsAgents.Api"
FRONTEND_DIR="src/frontend"

# Kill child processes on exit
trap 'kill $(jobs -p) 2>/dev/null; exit' INT TERM EXIT

echo "Starting .NET API on http://localhost:5000 …"
dotnet run --project "$BACKEND_DIR" --urls "http://localhost:5000" &
BACKEND_PID=$!

echo "Starting Angular dev server on http://localhost:4200 …"
(cd "$FRONTEND_DIR" && npx @angular/cli@latest serve) &
FRONTEND_PID=$!

echo ""
echo "  Backend : http://localhost:5000"
echo "  Frontend: http://localhost:4200"
echo ""
echo "Press Ctrl+C to stop both servers."
wait
