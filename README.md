# NPB順位予想 的中トラッカー

仲間内で行うNPB(日本プロ野球)のシーズン最終順位予想について、シーズン中に変動する実際の順位に対して各予想者の予想がどれだけ的中しているかを可視化するツールです。

- セ・パ合計12球団中、完全一致数が **6つ以上** になると「的中」ラインを超えたと判定し、グラフ・バッジで表示します。
- 実際の順位データは npb.jp の試合結果ページから日々スクレイピングして構築します。

## 仕組みの全体像(日々の順位変動がWebに反映されるまで)

```
① cron(自宅Linux, 毎日深夜)
    ↓ scripts/run-scraper.sh を実行
② NpbRankingPrediction.Scraper
    ↓ npb.jpから当月の試合結果を取得
    ↓ games.json / standings.json / scores.json / seasons.json(lastScrapedAtUtc) を更新
③ run-scraper.sh が data/ の差分を検知
    ↓ 差分があれば git commit & push
④ GitHub Actions (.github/workflows/deploy.yml)
    ↓ pushをトリガーにBlazor WASMをビルド
⑤ GitHub Pages
    ↓ 静的ファイルとして配信
⑥ ブラウザ(Web画面)
    最新の data/*.json を読み込んで表示
```

- ②は「その日の試合結果が確定した分だけ」`games.json` に追記するので、1日1回の実行で日々の順位が自動的に積み上がっていきます。同じ試合を再取得しても `gameId` で上書きされるだけなので、日に何度実行しても壊れません(冪等)。
- Web画面には `最終更新日`(=順位データが反映されている試合日)と `データ取得`(=②が最後に成功した日時)を分けて表示します。試合のない日を除いて**36時間以上「データ取得」が更新されない場合は画面上に警告バナーが出ます**(①〜③のどこかが止まっている合図です)。
- ①〜③は自宅Linuxマシン、④〜⑤はGitHub側で完結するので、自宅マシンの電源が入っている間だけ気にすればよい構成です。

## 構成

```
src/
  NpbRankingPrediction.Core/     共有ドメインロジック(順位計算・的中判定・データ永続化の抽象)
  NpbRankingPrediction.Scraper/  npb.jpをスクレイピングしてdata/配下のJSONを更新するコンソールアプリ
  NpbRankingPrediction.Web/      的中状況を表示するBlazor WebAssemblyアプリ(GitHub Pagesで静的公開)
data/
  seasons.json                  利用可能シーズン一覧
  {season}/
    games.json                  試合結果ログ
    standings.json              日次の順位スナップショット
    predictions.json            予想者ごとの予想順位(手動作成)
    scores.json                 日次の的中スコア・キャリーオーバー判定
scripts/
  run-scraper.sh                 cron用ラッパー(スクレイパー実行→git commit・push)
.github/workflows/deploy.yml     push時にBlazor WASMをビルドしGitHub Pagesへデプロイ
```

## セットアップ

### 1. 予想データを作成する

シーズン開始前に、仲間内で集めた予想を `data/{season}/predictions.json` に手動で記入します。キーは各予想者の**背番号**(文字列)です。

```json
{
  "season": 2026,
  "predictors": {
    "7": {
      "name": "",
      "central": ["巨人", "阪神", "DeNA", "広島", "ヤクルト", "中日"],
      "pacific": [
        "ソフトバンク",
        "日本ハム",
        "西武",
        "楽天",
        "オリックス",
        "ロッテ"
      ]
    },
    "23": {
      "name": "",
      "central": ["阪神", "巨人", "広島", "DeNA", "中日", "ヤクルト"],
      "pacific": [
        "ソフトバンク",
        "西武",
        "ロッテ",
        "日本ハム",
        "オリックス",
        "楽天"
      ]
    }
  }
}
```

- `central`/`pacific` は予想順位1位から6位の順にチーム名(略称)を並べます。
- チーム名は以下の12種類のみ有効です:
  - セ・リーグ: 巨人 / 阪神 / DeNA / 広島 / ヤクルト / 中日
  - パ・リーグ: ソフトバンク / 日本ハム / オリックス / ロッテ / 楽天 / 西武
- 参加人数・背番号はシーズンごとに自由に変えられます。コード側に人数や背番号を固定した箇所はなく、`predictions.json` の中身だけで完結します。
- `predictions.json` の `name` は空文字のままコミットします(スコア計算には使わないので空でも問題ありません)。これは**GitHubに必ずpushされるファイル**なので、実名はここには書かないでください。

#### 実名はローカル専用ファイルで管理する

的中判定に使う `predictions.json` とは別に、`data/{season}/predictor-names.json` を作ると、その中の実名だけが表示に使われます。このファイルは `.gitignore` 済みで**GitHubには一切pushされません**。

```json
{
  "season": 2026,
  "names": {
    "7": "たかやす",
    "23": "他の人"
  }
}
```

- ローカルで `dotnet run --project src/NpbRankingPrediction.Web` すると、このファイルの実名がリーダーボード・答え合わせ表・グラフ凡例に反映されます。
- このファイルを置かない(またはGitHub Pages上、つまりこのファイルが存在しない環境)の場合は、自動的に背番号のみの表示にフォールバックします。特別な設定切り替えは不要です。
- 「仲間内では実名で見たいが、公開URLは背番号だけにしたい」という運用は、この2ファイルの使い分け(`predictions.json`=常にコミット・実名なし/`predictor-names.json`=ローカル専用・実名あり)で実現しています。

### 2. スクレイパーを実行する(過去分の初回バックフィル)

```bash
dotnet run --project src/NpbRankingPrediction.Scraper -- --season 2026 --backfill --data-dir data
```

`--backfill` を付けると開幕月(3月)から現在の月まで遡って試合結果を取得し、`games.json`/`standings.json` を構築します。`predictions.json` が既に配置されていれば `scores.json` も同時に生成されます。

### 3. フロントエンドをローカルで確認する

```bash
dotnet run --project src/NpbRankingPrediction.Web
```

`http://localhost:5196` で表示を確認できます。`data/` 配下のJSONはビルド時に `wwwroot/data/` へ自動コピーされます(`NpbRankingPrediction.Web.csproj` のMSBuildターゲット)。

### 4. GitHubにpushしてGitHub Pagesを公開する

1. GitHubにリポジトリを作成し、このリポジトリをpush(`git remote add origin ...` → `git push -u origin main`)
2. リポジトリの Settings → Pages → Source を **GitHub Actions** に設定
3. 以降は `main`(または`master`)へのpushのたびに `.github/workflows/deploy.yml` が自動的にビルド・デプロイします

ここまでで手動更新のWebサイトとして公開が完了します。日々の順位を自動で反映したい場合は、続けて手順5を行ってください。

- **リポジトリはPrivateで問題ありません**(推奨)。予想者の実名は `predictor-names.json`(ローカル専用・`.gitignore`対象)にしか書かないため、そもそもGitHubにpushされる内容に実名は含まれません。それでもPrivateにしておけば、予想の中身や運用スクリプトなども含めて第三者から見えなくなります。ただし公開される GitHub Pages のサイトそのもの(`https://<user>.github.io/<repo>/`)はリポジトリの公開/非公開に関わらず、URLを知っていれば誰でも閲覧できる点は変わりません(そのサイトには背番号のみが表示されます)。

### 5. 日次の自動更新(自宅Linuxマシン)

手順4でリモートリポジトリが用意できたら、そのクローンを自宅Linuxマシンに置き、`scripts/run-scraper.sh` を cron に登録します(スクリプトが `git pull`/`git push` するため、事前に push できる状態のクローンであることが前提です)。

```
0 1 * * * /path/to/NPBRankingPrediction/scripts/run-scraper.sh >> /var/log/npb-scraper.log 2>&1
```

このスクリプトは当月分の試合結果を再取得し、`data/` に変更があれば自動で commit・push します。push をトリガーに GitHub Actions がフロントエンドを再ビルドし、GitHub Pages に反映します(全体の流れは前掲の図を参照)。

- 実行が成功するたびに、試合結果に変更がなくても `data/seasons.json` の `lastScrapedAtUtc` が更新されます。Web画面上部の「データ取得: ◯月◯日 ◯◯:◯◯」はこの値で、cronが実際に動いているかどうかの確認に使えます。
- 36時間以上 `lastScrapedAtUtc` が更新されないと、Web画面に自動更新が止まっている可能性がある旨の警告バナーが表示されます。cronの設定・自宅マシンの起動状態・ネットワークなどを確認してください。
- 初回セットアップ時の動作確認は `--season 2026 --data-dir data` を手動で1回実行し、`data/seasons.json` に `lastScrapedAtUtc` が書き込まれること・コンソールに `-> N completed games found` と出ることを確認すると安心です。

### 6. シーズン終了後に「確定」にする

レギュラーシーズンが終了したら、`--finalize` を付けて一度実行します。

```bash
dotnet run --project src/NpbRankingPrediction.Scraper -- --season 2026 --data-dir data --finalize
```

- 最終結果を取得したうえで `data/seasons.json` の該当シーズンに `"isFinal": true` を記録します。
- 確定後にWeb画面を開くと「✓ 2026年シーズン 最終順位確定」のようなバナーが表示され、「現在の的中状況」の見出しも「最終的中状況」に変わります。
- 確定済みのシーズンに対して `--finalize` なしで実行すると、更新をスキップします(誤って上書きしないための安全策)。再取得したい場合のみ `--finalize` を付けて再実行してください。

## 将来のAzure移行

`NpbRankingPrediction.Core` の `IDataStore` インターフェースを介してデータアクセスを抽象化しているため、将来的に以下のような移行が可能です。

- `JsonFileDataStore` → Azure Table Storage / Blob Storage を使う実装に差し替え(DI設定の変更のみ)
- Blazor WebAssemblyフロントエンドはそのまま Azure Static Web Apps(無料枠)でホスティング可能
- スクレイパーも将来的に Azure Functions のTimerTriggerへ移設可能(ロジックはCoreに集約済み)
