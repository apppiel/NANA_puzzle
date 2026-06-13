using UnityEngine;

public class BoardRenderer : MonoBehaviour
{
    public LevelData level;          // 그릴 레벨 데이터
    public GameObject cellPrefab;    // 칸 하나를 그릴 프리팹
    public float cellSize = 1.1f;    // 칸 사이 간격

    void Start()
    {
        DrawBoard();
    }

    void DrawBoard()
    {
        // 격자를 화면 중앙에 오도록 위치를 보정
        float offsetX = (level.width - 1) / 2f;
        float offsetY = (level.height - 1) / 2f;

        for (int x = 0; x < level.width; x++)
        {
            for (int y = 0; y < level.height; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (level.blockedCells.Contains(cell)) continue; // 막힌 칸은 건너뜀

                Vector3 pos = new Vector3((x - offsetX) * cellSize, (y - offsetY) * cellSize, 0f);
                // Quaternion.identity: 회전 없이 생성 / transform: 이 오브젝트 아래 자식으로 묶음
                GameObject obj = Instantiate(cellPrefab, pos, Quaternion.identity, transform);
                obj.name = $"Cell_{x}_{y}";

                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                if (cell == level.startCell)
                    sr.color = new Color(1f, 0.8f, 0.2f);      // 시작 칸: 노란색
                else
                    sr.color = new Color(0.85f, 0.85f, 0.85f); // 보통 칸: 연한 회색
            }
        }
    }
}
