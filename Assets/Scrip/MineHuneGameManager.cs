using System.Collections.Generic;
using UnityEngine;

public class MineHuntGameManager : MonoBehaviour
{
    private enum MapShape { Full, Cross }

    [System.Serializable]
    private class StageDef
    {
        public string name;
        public int width;
        public int height;
        public int maxAttempts;
        public MapShape shape;
        public int crossThickness;
        public int obstacleCount;
        public int mineCount;
    }

    [Header("Stage 1 (Inspector base)")]
    public int width = 10;
    public int height = 10;
    public int maxAttempts = 5;

    [Header("Prefabs & Parents")]
    public MineTile tilePrefab;
    public Transform boardParent;
    public float tileSpacing = 64f;

    [Header("UI")]
    public SimpleUI ui;

    public bool IsGameOver { get; private set; }
    public int AttemptsLeft { get; private set; }

    private int stageIndex = 0;
    private List<StageDef> stages;

    private MineTile[,] tiles;

    // 맵/장애물
    private HashSet<Vector2Int> playableTiles = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> blockedTiles = new HashSet<Vector2Int>();

    // 지뢰(여러 개)
    private HashSet<Vector2Int> mines = new HashSet<Vector2Int>();
    private HashSet<Vector2Int> foundMines = new HashSet<Vector2Int>();

    // 남은 지뢰(타겟)까지의 최단거리 맵
    private int[,] distMap;

    private static readonly Vector2Int[] DIRS =
    {
        new Vector2Int(1,0), new Vector2Int(-1,0),
        new Vector2Int(0,1), new Vector2Int(0,-1),
    };

    private StageDef CurrentStage => stages[stageIndex];

    private void Awake()
    {
        stages = new List<StageDef>
        {
            // Stage 1: 지뢰 1개
            new StageDef { name="Stage 1", width=5, height=5, maxAttempts=maxAttempts, shape=MapShape.Full,  crossThickness=0, obstacleCount=0,  mineCount=1 },

            // Stage 2: 지뢰 1개 + 장애물
            new StageDef { name="Stage 2", width=7, height=7, maxAttempts=7, shape=MapShape.Full,  crossThickness=0, obstacleCount=12, mineCount=1 },

            // Stage 3: 지뢰 2개 + 장애물
            new StageDef { name="Stage 3", width=12, height=12, maxAttempts=12, shape=MapShape.Full,  crossThickness=1, obstacleCount=22, mineCount=2 },

            // Stage 4: 십자가 + 장애물 + 지뢰 2개
            new StageDef { name="Stage 4", width=13, height=13, maxAttempts=14, shape=MapShape.Cross, crossThickness=3, obstacleCount=10, mineCount=2 },
        };
    }

    private void Start()
    {
        NewGame();
    }

    public void NextStage()
    {
        if (stageIndex < stages.Count - 1) stageIndex++;
        NewGame();
    }

    public void NewGame()
    {
        // Stage1 인스펙터 반영
        if (stageIndex == 0)
        {
            stages[0].width = width;
            stages[0].height = height;
            stages[0].maxAttempts = maxAttempts;
        }

        IsGameOver = false;
        AttemptsLeft = CurrentStage.maxAttempts;

        playableTiles.Clear();
        blockedTiles.Clear();
        mines.Clear();
        foundMines.Clear();

        // 기존 보드 제거
        if (tiles != null)
        {
            for (int y = 0; y < tiles.GetLength(1); y++)
                for (int x = 0; x < tiles.GetLength(0); x++)
                    if (tiles[x, y] != null) Destroy(tiles[x, y].gameObject);
        }

        int w = CurrentStage.width;
        int h = CurrentStage.height;
        tiles = new MineTile[w, h];
        distMap = new int[w, h];

        BuildPlayableTiles(w, h);
        PlaceObstacles();
        PlaceMines(CurrentStage.mineCount);

        // “남은 지뢰들” 기준 거리맵 계산(초기엔 남은 지뢰=전체 지뢰)
        RecomputeDistanceMap();

        // 중앙 배치
        float startX = -((w - 1) * tileSpacing) * 0.5f;
        float startY = -((h - 1) * tileSpacing) * 0.5f;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                MineTile tile = Instantiate(tilePrefab, boardParent != null ? boardParent : transform);
                tile.Init(this, new Vector2Int(x, y));

                var rt = tile.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(startX + x * tileSpacing, startY + y * tileSpacing);

                var p = new Vector2Int(x, y);

                if (!playableTiles.Contains(p))
                    tile.SetBlocked(true, "");
                else if (blockedTiles.Contains(p))
                    tile.SetBlocked(true, "X");

                tiles[x, y] = tile;
            }
        }

        ui?.BindGameManager(this);
        ui?.ShowRestart(false);
        ui?.ShowNextStage(false);
        ui?.SetStatus($"{CurrentStage.name} - 남은 지뢰: {foundMines.Count}/{mines.Count}");
        ui?.SetAttempts(AttemptsLeft);
    }

    private void BuildPlayableTiles(int w, int h)
    {
        playableTiles.Clear();

        if (CurrentStage.shape == MapShape.Full)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    playableTiles.Add(new Vector2Int(x, y));
            return;
        }

        // Cross
        int cx = w / 2;
        int cy = h / 2;
        int t = Mathf.Max(1, CurrentStage.crossThickness);
        int half = t / 2;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inV = Mathf.Abs(x - cx) <= half;
                bool inH = Mathf.Abs(y - cy) <= half;
                if (inV || inH) playableTiles.Add(new Vector2Int(x, y));
            }
        }
    }

    private void PlaceObstacles()
    {
        blockedTiles.Clear();

        int need = Mathf.Clamp(CurrentStage.obstacleCount, 0, playableTiles.Count - 1);
        var candidates = new List<Vector2Int>(playableTiles);

        // 중앙 1칸 보호(선택)
        int cx = CurrentStage.width / 2;
        int cy = CurrentStage.height / 2;
        candidates.Remove(new Vector2Int(cx, cy));

        while (blockedTiles.Count < need && candidates.Count > 0)
        {
            int idx = Random.Range(0, candidates.Count);
            blockedTiles.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
    }

    private void PlaceMines(int mineCount)
    {
        // 지뢰는 플레이 가능 + 장애물 제외
        var candidates = new List<Vector2Int>();
        foreach (var p in playableTiles)
            if (!blockedTiles.Contains(p))
                candidates.Add(p);

        mineCount = Mathf.Clamp(mineCount, 1, candidates.Count);

        for (int i = 0; i < mineCount; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            mines.Add(candidates[idx]);
            candidates.RemoveAt(idx);
        }
    }

    // ✅ “남은 지뢰(= 아직 못 찾은 지뢰들)”까지의 최단 경로 거리맵 생성 (멀티소스 BFS)
    private void RecomputeDistanceMap()
    {
        int w = tiles.GetLength(0);
        int h = tiles.GetLength(1);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                distMap[x, y] = -1;

        var q = new Queue<Vector2Int>();

        // 남은 지뢰들만 소스(0)
        foreach (var m in mines)
        {
            if (foundMines.Contains(m)) continue; // 이미 찾은 지뢰는 타겟에서 제외
            distMap[m.x, m.y] = 0;
            q.Enqueue(m);
        }

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            int curDist = distMap[cur.x, cur.y];

            foreach (var d in DIRS)
            {
                var nxt = cur + d;

                if (nxt.x < 0 || nxt.x >= w || nxt.y < 0 || nxt.y >= h) continue;
                if (!playableTiles.Contains(nxt)) continue;

                // ✅ 장애물은 통과 불가
                if (blockedTiles.Contains(nxt)) continue;

                // ✅ “찾은 지뢰 칸”은 통과 가능 (그냥 일반 바닥처럼 취급)
                // (별도 처리 필요 없음: blocked가 아니면 통과됨)

                if (distMap[nxt.x, nxt.y] != -1) continue;

                distMap[nxt.x, nxt.y] = curDist + 1;
                q.Enqueue(nxt);
            }
        }
    }

    // ✅ 이미 열려 있는 숫자들도 전부 “남은 지뢰 기준”으로 갱신
    private void RefreshAllRevealedNumbers()
    {
        int w = tiles.GetLength(0);
        int h = tiles.GetLength(1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var t = tiles[x, y];
                if (t == null) continue;

                var p = new Vector2Int(x, y);

                if (!t.IsRevealed) continue;
                if (!playableTiles.Contains(p)) continue;
                if (blockedTiles.Contains(p)) continue;

                // M은 유지
                if (mines.Contains(p))
                {
                    t.ShowMine();
                    continue;
                }

                // 숫자는 새 기준으로 갱신
                int d = distMap[x, y];
                t.SetNumberText(d);
            }
        }
    }

    // MineTile에서 호출
    // dist: -2 = 지뢰 찾음, -1 = 도달불가, >=0 = 거리
    public bool TryReveal(Vector2Int pos, out int dist)
    {
        dist = -1;

        if (IsGameOver) return false;
        if (AttemptsLeft <= 0) return false;

        if (!playableTiles.Contains(pos)) return false;
        if (blockedTiles.Contains(pos)) return false;

        AttemptsLeft--;
        ui?.SetAttempts(AttemptsLeft);

        // ✅ 지뢰 클릭
        if (mines.Contains(pos))
        {
            foundMines.Add(pos);
			tiles[pos.x, pos.y]?.ShowMine();
            // 지뢰 찾았으니 dist는 -2로 신호
            dist = -2;

            // 전부 찾으면 성공
            if (foundMines.Count >= mines.Count)
            {
                IsGameOver = true;
                ui?.SetStatus("성공!");
                RevealAll(showMines: true);
                ui?.ShowNextStage(stageIndex < stages.Count - 1);
                return true;
            }

            // ✅ 아직 남은 지뢰가 있으니: 타겟을 “남은 지뢰”로 바꾸고, 모든 숫자 갱신
            RecomputeDistanceMap();
            RefreshAllRevealedNumbers();
            ui?.SetStatus($"지뢰 발견! ({foundMines.Count}/{mines.Count})");

            // 횟수 소진 시 실패
            if (AttemptsLeft <= 0)
            {
                IsGameOver = true;
                ui?.SetStatus("실패!");
                RevealAll(showMines: true);
                ui?.ShowRestart(true);
            }

            return true;
        }

        // ✅ 지뢰가 아닌 칸: 현재 거리맵(= 남은 지뢰 기준)에서 값 가져오기
        dist = distMap[pos.x, pos.y];

        if (AttemptsLeft <= 0)
        {
            IsGameOver = true;
            ui?.SetStatus("실패!");
            RevealAll(showMines: true);
            ui?.ShowRestart(true);
        }
        else
        {
            ui?.SetStatus($"탐색 중… ({foundMines.Count}/{mines.Count})");
        }

        return true;
    }

    private void RevealAll(bool showMines)
    {
        int w = tiles.GetLength(0);
        int h = tiles.GetLength(1);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var t = tiles[x, y];
                if (t == null) continue;

                var p = new Vector2Int(x, y);

                if (!playableTiles.Contains(p))
                {
                    t.SetBlocked(true, "");
                    continue;
                }

                if (blockedTiles.Contains(p))
                {
                    t.SetBlocked(true, "X");
                    continue;
                }

                if (showMines && mines.Contains(p))
                {
                    t.ShowMine(); // ✅ 발견 여부와 관계없이 M 표시 유지
                }
                else if (!t.IsRevealed)
                {
                    t.Lock();
                }
            }
        }
    }
}
