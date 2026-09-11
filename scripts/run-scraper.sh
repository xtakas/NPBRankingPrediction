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
git pull --ff-only

echo "[$(date '+%F %T')] running scraper for season $SEASON"
dotnet run --configuration Release --project src/NpbRankingPrediction.Scraper -- --season "$SEASON" --data-dir data

if git status --porcelain data | grep -q .; then
    echo "[$(date '+%F %T')] data changed, committing"
    git add data
    git commit -m "chore: update ${SEASON} standings data ($(date '+%F'))"
    git push
else
    echo "[$(date '+%F %T')] no data changes"
fi
