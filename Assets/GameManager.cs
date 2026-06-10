using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 게임 전체를 코드로 생성·관리한다.
// 빈 GameObject 하나에 이 스크립트만 붙이고 Play 하면 미로/팩맨/유령/점수까지 모두 생성된다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    // 미로 기호: # 벽 / . 먹이 / o 파워먹이 / - 유령문 / P 팩맨시작 / G 유령시작 / (공백) 빈칸
    private readonly string[] maze =
    {
        "###################",
        "#........#........#",
        "#o##.###.#.###.##o#",
        "#.................#",
        "#.##.#.#####.#.##.#",
        "#....#...#...#....#",
        "####.###.#.###.####",
        "####.#.......#.####",
        "####.#.##-##.#.####",
        "#......#GGG#......#",
        "####.#.#####.#.####",
        "####.#.......#.####",
        "####.#.#####.#.####",
        "#........#........#",
        "#.##.###.#.###.##.#",
        "#o.#.....P.....#.o#",
        "##.#.#.#####.#.#.##",
        "#....#...#...#....#",
        "#.######.#.######.#",
        "#.................#",
        "###################",
    };

    public int Rows { get; private set; }
    public int Cols { get; private set; }

    private char[,] tiles;                 // 벽/문 판정용
    private GameObject[,] pellets;         // 먹이 오브젝트 참조(먹으면 파괴)
    private readonly List<Transform> powerPellets = new List<Transform>();

    // 상태
    public int Score { get; private set; }
    public int Lives { get; private set; } = 3;
    public int PelletsRemaining { get; private set; }
    public bool Paused { get; private set; }   // 게임오버/승리 시 정지
    private bool gameOver, won;

    // 유령 모드(겁먹음 / 흩어짐<->추격 교대)
    public bool Frightened { get; private set; }
    private float frightenedTimer;
    public float frightenedDuration = 7f;
    public bool GhostScatter { get; private set; } = true;
    private float modeTimer;

    private Pacman pacman;
    private int pacSpawnRow, pacSpawnCol;
    private readonly List<Ghost> ghosts = new List<Ghost>();
    private readonly List<Vector2Int> ghostSpawns = new List<Vector2Int>();

    // 스프라이트(코드 생성)
    private Sprite squareSprite, circleSprite, pacmanSprite, ghostSprite;

    public int PacmanRow => pacman != null ? pacman.Row : 0;
    public int PacmanCol => pacman != null ? pacman.Col : 0;
    public Vector2 PacmanDirection => pacman != null ? pacman.Direction : Vector2.zero;

    // ---------- 초기화 ----------
    void Awake()
    {
        Instance = this;
        Rows = maze.Length;
        Cols = maze[0].Length;

        squareSprite  = MakeSquareSprite();
        circleSprite  = MakeCircleSprite(48);
        pacmanSprite  = MakePacmanSprite(48);
        ghostSprite   = MakeGhostSprite(48);

        BuildLevel();
        SpawnPacman();
        SpawnGhosts();
        SetupCamera();
    }

    public Vector3 CellToWorld(int row, int col)
        => new Vector3(col, (Rows - 1) - row, 0f);

    public bool Blocked(int row, int col, bool isGhost)
    {
        if (row < 0 || col < 0 || row >= Rows || col >= Cols) return true;
        char ch = tiles[row, col];
        if (ch == '#') return true;
        if (ch == '-') return !isGhost;   // 문은 유령만 통과
        return false;
    }

    void BuildLevel()
    {
        tiles = new char[Rows, Cols];
        pellets = new GameObject[Rows, Cols];
        PelletsRemaining = 0;

        for (int r = 0; r < Rows; r++)
        {
            for (int c = 0; c < Cols; c++)
            {
                char ch = maze[r][c];
                tiles[r, c] = ch;
                Vector3 pos = CellToWorld(r, c);

                switch (ch)
                {
                    case '#':
                        MakeSprite("Wall", pos, squareSprite,
                                   new Color(0.13f, 0.18f, 0.9f), 0.92f, 0);
                        break;
                    case '-':
                        MakeSprite("Door", pos + new Vector3(0, 0.0f, 0), squareSprite,
                                   new Color(1f, 0.6f, 0.8f), 0.05f, 0)
                                   .transform.localScale = new Vector3(0.92f, 0.12f, 1f);
                        break;
                    case '.':
                        pellets[r, c] = MakeSprite("Pellet", pos, circleSprite,
                                   new Color(1f, 0.85f, 0.6f), 0.16f, 1);
                        PelletsRemaining++;
                        break;
                    case 'o':
                        var pp = MakeSprite("PowerPellet", pos, circleSprite,
                                   new Color(1f, 0.85f, 0.6f), 0.5f, 1);
                        pellets[r, c] = pp;
                        powerPellets.Add(pp.transform);
                        PelletsRemaining++;
                        break;
                    case 'P':
                        pacSpawnRow = r; pacSpawnCol = c;
                        break;
                    case 'G':
                        ghostSpawns.Add(new Vector2Int(r, c));
                        break;
                }
            }
        }
    }

    void SpawnPacman()
    {
        var go = new GameObject("Pacman");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = pacmanSprite;
        sr.color = new Color(1f, 0.92f, 0.15f);
        sr.sortingOrder = 2;
        go.transform.localScale = Vector3.one * 0.85f;
        pacman = go.AddComponent<Pacman>();
        pacman.speed = 5.2f;
        pacman.Row = pacSpawnRow; pacman.Col = pacSpawnCol;
    }

    void SpawnGhosts()
    {
        // 문('-') 위치를 찾아 그 위 칸을 유령 출구로 사용
        int doorRow = 8, doorCol = Cols / 2;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
                if (maze[r][c] == '-') { doorRow = r; doorCol = c; }
        int exitRow = doorRow - 1, exitCol = doorCol;

        // (색, 앞칸offset, 흩어짐 구석)
        Color[] colors = { new Color(1f,0.2f,0.2f), new Color(1f,0.55f,0.85f), new Color(0.3f,0.9f,1f) };
        int[] aheads = { 0, 4, 2 };
        Vector2Int[] corners = {
            new Vector2Int(0, Cols - 1),   // 우상단
            new Vector2Int(0, 0),          // 좌상단
            new Vector2Int(Rows - 1, Cols-1) // 우하단
        };

        for (int i = 0; i < ghostSpawns.Count && i < 3; i++)
        {
            var sp = ghostSpawns[i];
            var go = new GameObject("Ghost" + i);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ghostSprite;
            sr.sortingOrder = 2;
            go.transform.localScale = Vector3.one * 0.8f;
            var g = go.AddComponent<Ghost>();
            g.Init(sp.x, sp.y, colors[i], aheads[i], corners[i]);
            g.SetHouseExit(exitRow, exitCol);
            ghosts.Add(g);
        }
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.backgroundColor = Color.black;
        cam.transform.position = new Vector3((Cols - 1) / 2f, (Rows - 1) / 2f, -10f);
        float vert = Rows / 2f + 0.5f;
        float horiz = (Cols / 2f + 0.5f) / Mathf.Max(0.0001f, cam.aspect);
        cam.orthographicSize = Mathf.Max(vert, horiz);
    }

    // ---------- 게임 루프 ----------
    void Update()
    {
        if (gameOver || won)
        {
            if (Input.GetKeyDown(KeyCode.R))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return;
        }

        // 겁먹음 타이머
        if (Frightened)
        {
            frightenedTimer -= Time.deltaTime;
            if (frightenedTimer <= 0f) Frightened = false;
        }
        else
        {
            // 흩어짐 <-> 추격 교대
            modeTimer += Time.deltaTime;
            if (GhostScatter && modeTimer > 7f) { GhostScatter = false; modeTimer = 0f; }
            else if (!GhostScatter && modeTimer > 20f) { GhostScatter = true; modeTimer = 0f; }
        }

        // 파워먹이 깜빡임
        float s = 0.5f + Mathf.Sin(Time.time * 6f) * 0.12f;
        foreach (var p in powerPellets)
            if (p != null) p.localScale = Vector3.one * s;

        CheckCollisions();
    }

    public void EatPellet(int row, int col)
    {
        if (pellets[row, col] == null) return;
        char ch = tiles[row, col];
        if (ch != '.' && ch != 'o') return;

        Destroy(pellets[row, col]);
        pellets[row, col] = null;
        tiles[row, col] = ' ';
        PelletsRemaining--;

        if (ch == 'o')
        {
            Score += 50;
            Frightened = true;
            frightenedTimer = frightenedDuration;
        }
        else Score += 10;

        if (PelletsRemaining <= 0) { won = true; Paused = true; }
    }

    void CheckCollisions()
    {
        if (pacman == null) return;
        foreach (var g in ghosts)
        {
            if (g.IsEaten) continue;
            float d = (g.transform.position - pacman.transform.position).sqrMagnitude;
            if (d < 0.25f) // 약 0.5칸 이내
            {
                if (Frightened) { g.GetEaten(); Score += 200; }
                else PacmanHit();
            }
        }
    }

    void PacmanHit()
    {
        Lives--;
        if (Lives <= 0) { gameOver = true; Paused = true; return; }

        // 위치 리셋
        pacman.SetCell(pacSpawnRow, pacSpawnCol);
        foreach (var g in ghosts) g.ResetToHome();
        Frightened = false;
        GhostScatter = true; modeTimer = 0f;
    }

    // ---------- 간단한 화면 UI ----------
    void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(12, 8, 300, 24), "SCORE: " + Score);
        GUI.Label(new Rect(12, 30, 300, 24), "LIVES: " + Lives);

        if (won || gameOver)
        {
            var style = new GUIStyle(GUI.skin.label)
            { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = won ? Color.yellow : Color.red;
            string msg = won ? "YOU WIN!" : "GAME OVER";
            GUI.Label(new Rect(0, Screen.height / 2 - 40, Screen.width, 40), msg, style);

            var sub = new GUIStyle(GUI.skin.label)
            { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            sub.normal.textColor = Color.white;
            GUI.Label(new Rect(0, Screen.height / 2 + 4, Screen.width, 30),
                      "Press R to restart", sub);
        }
    }

    // ---------- 스프라이트 생성 유틸 ----------
    GameObject MakeSprite(string name, Vector3 pos, Sprite sprite, Color color, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        return go;
    }

    static Sprite MakeSquareSprite()
    {
        var tex = new Texture2D(4, 4);
        var px = new Color[16];
        for (int i = 0; i < 16; i++) px[i] = Color.white;
        tex.SetPixels(px); tex.Apply();
        tex.filterMode = FilterMode.Point;
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size);
        float r = size / 2f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                px[y * size + x] = (dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f))
                                   ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite MakePacmanSprite(int size)
    {
        var tex = new Texture2D(size, size);
        float r = size / 2f;
        float mouth = 32f * Mathf.Deg2Rad; // 입 벌림(반각)
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r, dy = y + 0.5f - r;
                bool inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                float ang = Mathf.Atan2(dy, dx);          // +x 방향이 0
                if (Mathf.Abs(ang) < mouth) inside = false; // +x 쪽 쐐기를 잘라 입 모양
                px[y * size + x] = inside ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static Sprite MakeGhostSprite(int size)
    {
        var tex = new Texture2D(size, size);
        float r = size / 2f;
        float domeCy = size * 0.55f; // 위쪽 반원 중심
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                bool inside;
                if (y >= domeCy)
                {
                    float dy = y + 0.5f - domeCy;       // 위쪽: 반원(머리)
                    inside = dx * dx + dy * dy <= (r - 0.5f) * (r - 0.5f);
                }
                else
                {
                    // 아래쪽: 사각 몸통 + 물결 모양 밑단
                    inside = Mathf.Abs(dx) <= r - 0.5f;
                    if (inside && y < size * 0.16f)
                    {
                        float wave = Mathf.Sin((x / (float)size) * Mathf.PI * 4f);
                        if (wave < -0.2f) inside = false;
                    }
                }
                px[y * size + x] = inside ? Color.white : Color.clear;
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
