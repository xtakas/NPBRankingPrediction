#!/usr/bin/env bash
# 自宅Linuxマシンのcronから日次実行するスクレイパーラッパー。
# npb.jpから当月の試合結果を取得し、games/standings/scores/seasons.jsonを更新して、
# 変更があればそのままリポジトリにコミット・pushする。
#
# crontab例(毎日深夜1時に実行):
#   0 1 * * * /path/to/NPBRankingPrediction/scripts/run-scraper.sh >> /var/log/npb-scraper.log 2>&1

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SEASON="${1:-$(date +%Y)}"

cd "$REPO_ROOT"

echo "[$(date '+%F %T')] pulling latest repo state"
if ! git pull --ff-only; then
    echo "[$(date '+%F %T')] ERROR: git pull --ff-only failed. This usually means a previous run's" >&2
    echo "  git push failed, leaving an un-pushed local commit that now conflicts with the remote." >&2
    echo "  Manual intervention needed: cd $REPO_ROOT && git status (then resolve, e.g. git pull --rebase)." >&2
    exit 1
fi

echo "[$(date '+%F %T')] running scraper for season $SEASON"
dotnet run --configuration Release --project src/NpbRankingPrediction.Scraper -- --season "$SEASON" --data-dir data

if git status --porcelain data | grep -q .; then
    echo "[$(date '+%F %T')] data changed, committing"
    git add data
    git commit -m "chore: update ${SEASON} standings data ($(date '+%F'))"

    push_ok=false
    for attempt in 1 2 3; do
        if git push; then
            push_ok=true
            break
        fi
        echo "[$(date '+%F %T')] git push attempt $attempt failed, retrying in 10s..."
        sleep 10
    done

    if [ "$push_ok" != true ]; then
        echo "[$(date '+%F %T')] ERROR: git push failed after 3 attempts. A local commit now exists that" >&2
        echo "  is not pushed; the next run's 'git pull --ff-only' will likely fail until this is" >&2
        echo "  resolved manually (cd $REPO_ROOT && git push, or investigate why it's failing)." >&2
        exit 1
    fi
else
    echo "[$(date '+%F %T')] no data changes"
fi
