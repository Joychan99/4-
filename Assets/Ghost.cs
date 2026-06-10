using System.Collections.Generic;
using UnityEngine;

// 유령 AI: 추격(Chase) / 흩어짐(Scatter) / 겁먹음(Frightened) / 먹힘(집으로 복귀) 상태를 가진다.
// 각 분기점에서 목표 타일에 가장 가까워지는 방향을 고른다(역주행은 원칙적으로 금지).
public class Ghost : GridMover
{
    public Color baseColor = Color.red;
    public int aheadOffset = 0;        // 팩맨 진행방향 앞쪽 몇 칸을 노릴지 (성격 차이)
    public Vector2Int scatterCorner;   // 흩어짐 모드에서 향하는 구석 (row, col)

    private int homeRow, homeCol;      // 스폰(집) 위치
    private int exitRow, exitCol;      // 집 밖 출구(문 위 칸)
    private bool inHouse = true;       // 아직 집 안에 있는가
    private bool eaten;                // true면 집으로 복귀 중(눈알 상태)
    private SpriteRenderer sr;

    protected override bool IsGhost => true;

    public void Init(int row, int col, Color color, int ahead, Vector2Int corner)
    {
        Row = row; Col = col; homeRow = row; homeCol = col;
        baseColor = color; aheadOffset = ahead; scatterCorner = corner;
    }

    public void SetHouseExit(int row, int col) { exitRow = row; exitCol = col; }

    protected override void Start()
    {
        base.Start();
        sr = GetComponent<SpriteRenderer>();
        speed = 4.2f;
    }

    public void ResetToHome()
    {
        eaten = false;
        inHouse = true;
        SetCell(homeRow, homeCol);
    }

    public bool IsEaten => eaten;
    public void GetEaten() { eaten = true; } // 겁먹은 상태에서 팩맨에게 먹혔을 때

    protected override void DecideDirection()
    {
        UpdateAppearance();

        // 현재 칸에서 갈 수 있는 방향들(역주행 제외)
        Vector2[] dirs = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        List<Vector2> options = new List<Vector2>();
        foreach (var d in dirs)
            if (CanMove(d) && d != -Direction) options.Add(d);

        // 막다른 길이면 역주행 허용
        if (options.Count == 0 && Direction != Vector2.zero && CanMove(-Direction))
            options.Add(-Direction);
        if (options.Count == 0) { nextDirection = Vector2.zero; return; }

        // 겁먹음(아직 안 먹힌 상태)이면 무작위 이동
        if (gm.Frightened && !eaten)
        {
            nextDirection = options[Random.Range(0, options.Count)];
            return;
        }

        // 목표 타일 결정
        int tRow, tCol;
        GetTarget(out tRow, out tCol);

        // 목표에 가장 가까워지는 방향 선택
        float best = float.MaxValue;
        Vector2 chosen = options[0];
        foreach (var d in options)
        {
            int nCol = Col + Mathf.RoundToInt(d.x);
            int nRow = Row - Mathf.RoundToInt(d.y);
            float dist = (nRow - tRow) * (nRow - tRow) + (nCol - tCol) * (nCol - tCol);
            if (dist < best) { best = dist; chosen = d; }
        }
        nextDirection = chosen;

        // 속도: 먹힘 > 평소 > 겁먹음
        speed = eaten ? 7f : (gm.Frightened ? 3f : 4.2f);
    }

    private void GetTarget(out int tRow, out int tCol)
    {
        if (eaten) { tRow = homeRow; tCol = homeCol; return; }      // 집으로
        if (inHouse) { tRow = exitRow; tCol = exitCol; return; }    // 일단 집 밖으로
        if (gm.GhostScatter) { tRow = scatterCorner.x; tCol = scatterCorner.y; return; } // 구석으로

        // 추격: 팩맨 위치 + 진행방향 앞 offset 칸
        Vector2 pd = gm.PacmanDirection;
        tCol = gm.PacmanCol + Mathf.RoundToInt(pd.x) * aheadOffset;
        tRow = gm.PacmanRow - Mathf.RoundToInt(pd.y) * aheadOffset;
    }

    protected override void OnArrivedAtCell()
    {
        // 출구(문 위)에 도달하면 집에서 나온 것으로 처리
        if (inHouse && Row <= exitRow) inHouse = false;

        // 먹힌 상태로 집에 도착하면 부활(다시 집에서 나가야 함)
        if (eaten && Row == homeRow && Col == homeCol)
        {
            eaten = false;
            inHouse = true;
        }
    }

    private void UpdateAppearance()
    {
        if (sr == null) return;
        if (eaten)            sr.color = new Color(0.5f, 0.5f, 0.5f, 0.5f); // 눈알(반투명)
        else if (gm.Frightened) sr.color = new Color(0.25f, 0.35f, 1f);    // 파랗게
        else                  sr.color = baseColor;
    }
}
