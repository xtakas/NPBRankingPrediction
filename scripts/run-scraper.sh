#!/usr/bin/env bash
# 自宅Linuxマシンのcronから日次実行するスクレイパーラッパー。
# npb.jpから当月の試合結果を取得し、games/standings/scores/seasons.jsonを更新して、
# 変更があればそのままリポジトリにコミット・pushする。
#
# このマシンには.NET SDKをインストールしない前提。代わりにWindows側で
#   dotnet publish src/NpbRankingPrediction.Scraper -c Release -r linux-x64 --self-contained true -o publish_linux_scraper
# として単体実行ファイルを作り、このマシンの $SCRAPER_BIN (既定: ~/npb-scraper-bin/NpbRankingPrediction.Scraper)
# にコピー・chmod +x したものを実行する。Scraperのコードを変更したら、その都度この再publish・再配置が必要。
#
# 当日の試合速報(npb.jpの本日の試合速報ウィジェット)は21〜24時台に複数回実行して取りに行く
# 想定。その日の全試合が既に「試合終了」と確認済みの場合、このスクリプトはnpb.jpへ一切
# アクセスせずに即終了する(Scraper側のLastFullyFinishedDateによる判定)ので、頻繁に実行しても
# 無駄なアクセスにはならない。
#
# crontab例(21〜23時台は30分おき、24時・深夜1時にも念のため実行)。
# root以外のユーザーで実行する場合 /var/log/ は書き込めないため、$HOME 配下などに出力する:
#   0,30 21-23 * * * /path/to/NPBRankingPrediction/scripts/run-scraper.sh >> $HOME/npb-scraper.log 2>&1
#   0 0,1 * * *       /path/to/NPBRankingPrediction/scripts/run-scraper.sh >> $HOME/npb-scraper.log 2>&1
#
# 手動実行時の引数:
#   ./run-scraper.sh [SEASON] [--backfill] [--finalize] [--from-month N] [--to-month N]
# SEASON(年)を省略すると当年になる。--backfill/--finalize/--from-month/--to-month は
# そのままScraper本体に転送される(例: ./run-scraper.sh 2026 --backfill)。
# これら以外の未知のオプションはエラーで停止する(誤ってSEASONとして解釈され、
# 分かりにくいクラッシュになるのを防ぐため)。

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
SCRAPER_BIN="${SCRAPER_BIN:-$HOME/npb-scraper-bin/NpbRankingPrediction.Scraper}"

SEASON="$(date +%Y)"
EXTRA_ARGS=()

while [ $# -gt 0 ]; do
    case "$1" in
        --backfill|--finalize)
            EXTRA_ARGS+=("$1")
            shift
            ;;
        --from-month|--to-month)
            if [ $# -lt 2 ]; then
                echo "ERROR: $1 requires a value" >&2
                exit 1
            fi
            EXTRA_ARGS+=("$1" "$2")
            shift 2
            ;;
        --*)
            echo "ERROR: unknown option: $1" >&2
            echo "  Supported: [SEASON] [--backfill] [--finalize] [--from-month N] [--to-month N]" >&2
            exit 1
            ;;
        *)
            SEASON="$1"
            shift
            ;;
    esac
done

if ! [[ "$SEASON" =~ ^[0-9]+$ ]]; then
    echo "ERROR: invalid season '$SEASON' (expected a 4-digit year)" >&2
    echo "  Supported: [SEASON] [--backfill] [--finalize] [--from-month N] [--to-month N]" >&2
    exit 1
fi

if [ ! -x "$SCRAPER_BIN" ]; then
    echo "[$(date '+%F %T')] ERROR: scraper executable not found or not executable: $SCRAPER_BIN" >&2
    echo "  Publish it on Windows with:" >&2
    echo "    dotnet publish src/NpbRankingPrediction.Scraper -c Release -r linux-x64 --self-contained true -o publish_linux_scraper" >&2
    echo "  then copy the contents of publish_linux_scraper/ to $(dirname "$SCRAPER_BIN")/ on this machine and chmod +x the binary." >&2
    exit 1
fi

cd "$REPO_ROOT"

echo "[$(date '+%F %T')] pulling latest repo state"
if ! git pull --ff-only; then
    echo "[$(date '+%F %T')] ERROR: git pull --ff-only failed. This usually means a previous run's" >&2
    echo "  git push failed, leaving an un-pushed local commit that now conflicts with the remote." >&2
    echo "  Manual intervention needed: cd $REPO_ROOT && git status (then resolve, e.g. git pull --rebase)." >&2
    exit 1
fi

if [ ${#EXTRA_ARGS[@]} -gt 0 ]; then
    echo "[$(date '+%F %T')] running scraper for season $SEASON (${EXTRA_ARGS[*]})"
else
    echo "[$(date '+%F %T')] running scraper for season $SEASON"
fi
# 空配列を "${EXTRA_ARGS[@]}" として展開すると、古いbash(4.4未満)ではset -uで
# "unbound variable" になることがあるため、+ を使った安全な展開にしている。
"$SCRAPER_BIN" --season "$SEASON" --data-dir data "${EXTRA_ARGS[@]+"${EXTRA_ARGS[@]}"}"

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
