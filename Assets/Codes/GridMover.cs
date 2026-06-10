using UnityEngine;

// 격자(타일) 단위로 부드럽게 이동하는 공통 베이스 클래스.
// 타일 중심에 도착했을 때만 방향을 다시 결정하므로 동작이 안정적이다.
public abstract class GridMover : MonoBehaviour
{
    public float speed = 5f;
    public int Row, Col;                 // 현재(혹은 마지막으로 차지한) 타일
    public Vector2 Direction = Vector2.zero;   // 월드 기준 진행 방향
    protected Vector2 nextDirection = Vector2.zero; // 다음에 가고 싶은 방향(버퍼)

    protected GameManager gm;
    private Vector3 targetPos;           // 지금 이동 중인 목표 타일의 월드 좌표
    private int stepRow, stepCol;        // 그 목표 타일의 격자 좌표
    private bool moving;

    // 유령은 문('-')을 통과할 수 있으므로 구분한다.
    protected abstract bool IsGhost { get; }

    protected virtual void Start()
    {
        gm = GameManager.Instance;
        SetCell(Row, Col);
    }

    // 특정 타일로 순간 배치(스폰/리스폰용)
    public void SetCell(int row, int col)
    {
        if (gm == null) gm = GameManager.Instance;
        Row = row; Col = col;
        transform.position = gm.CellToWorld(Row, Col);
        targetPos = transform.position;
        Direction = Vector2.zero;
        nextDirection = Vector2.zero;
        moving = false;
    }

    protected bool CanMove(Vector2 dir)
    {
        if (dir == Vector2.zero) return false;
        int nCol = Col + Mathf.RoundToInt(dir.x);
        int nRow = Row - Mathf.RoundToInt(dir.y); // 월드 +y(위)는 행이 감소
        return !gm.Blocked(nRow, nCol, IsGhost);
    }

    // 매 프레임 자식이 nextDirection 을 정하도록 한다.
    protected abstract void DecideDirection();

    // 타일 중심에 막 도착했을 때 호출(먹기/AI 재계산 등)
    protected virtual void OnArrivedAtCell() { }

    protected virtual void Update()
    {
        if (gm != null && gm.Paused) return;

        DecideDirection();
        if (!moving) TryStartStep();
        if (moving) Step();
    }

    private void TryStartStep()
    {
        // 가고 싶은 방향이 가능하면 그쪽으로, 아니면 가던 방향 유지
        if (nextDirection != Vector2.zero && CanMove(nextDirection))
            Direction = nextDirection;

        if (Direction != Vector2.zero && CanMove(Direction))
        {
            stepCol = Col + Mathf.RoundToInt(Direction.x);
            stepRow = Row - Mathf.RoundToInt(Direction.y);
            targetPos = gm.CellToWorld(stepRow, stepCol);
            moving = true;
        }
        else
        {
            Direction = Vector2.zero; // 벽에 막히면 정지
        }
    }

    private void Step()
    {
        transform.position = Vector3.MoveTowards(
            transform.position, targetPos, speed * Time.deltaTime);

        if ((transform.position - targetPos).sqrMagnitude < 0.0000001f)
        {
            transform.position = targetPos;
            Row = stepRow; Col = stepCol;
            moving = false;
            OnArrivedAtCell();
            // 한 프레임 멈춤 없이 곧바로 다음 칸으로 이어서 이동
            DecideDirection();
            TryStartStep();
        }
    }
}
