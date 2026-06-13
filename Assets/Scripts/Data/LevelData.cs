using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Level_", menuName = "NANA/Level Data")]
public class LevelData : ScriptableObject
{
    [System.Serializable]
    public struct Edge
    {
        public int from;   // 시작 점 번호
        public int to;     // 끝 점 번호
    }

    public List<Vector2> nodes = new List<Vector2>();   // 점들의 위치
    public List<Edge> edges = new List<Edge>();         // 점을 잇는 선들
}
