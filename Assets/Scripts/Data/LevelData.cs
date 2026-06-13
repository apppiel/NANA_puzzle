using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_", menuName = "NANA/Level Data")]
public class LevelData : ScriptableObject
{
    public int width = 5;        // 가로 칸 수
    public int height = 5;       // 세로 칸 수
    public Vector2Int startCell; // 시작 칸 (고정 시작점)
    public List<Vector2Int> blockedCells = new List<Vector2Int>(); // 못 쓰는 빈 칸들
}
