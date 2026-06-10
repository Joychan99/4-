using UnityEngine;

// 플레이어(팩맨): 키보드 입력으로 방향을 정하고, 도착한 칸의 먹이를 먹는다.
public class Pacman : GridMover
{
    protected override bool IsGhost => false;

    protected override void DecideDirection()
    {
        Vector2 input = Vector2.zero;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) input = Vector2.up;
        else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) input = Vector2.down;
        else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) input = Vector2.left;
        else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) input = Vector2.right;

        // 입력이 있을 때만 갱신 → 마지막 입력이 버퍼링되어 모퉁이에서 자동 회전
        if (input != Vector2.zero) nextDirection = input;
    }

    protected override void OnArrivedAtCell()
    {
        gm.EatPellet(Row, Col);

        // 진행 방향을 향해 입(스프라이트)을 회전
        if (Direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(Direction.y, Direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}