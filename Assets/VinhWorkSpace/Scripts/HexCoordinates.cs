using System;
using UnityEngine;

[System.Serializable]
public struct HexCoordinates : IEquatable<HexCoordinates>
{
    // Chỉ lưu 2 trục Q và R trong bộ nhớ để tiết kiệm RAM
    [SerializeField] private int q;
    [SerializeField] private int r;

    public int Q => q;
    public int R => r;

    // Trục S được suy ra tự động theo công thức Q + R + S = 0
    public int S => -q - r;

    public HexCoordinates(int q, int r)
    {
        this.q = q;
        this.r = r;
    }

    // Constructor trực tiếp từ tọa độ Cube (Q, R, S)
    public HexCoordinates(int q, int r, int s)
    {
        this.q = q;
        this.r = r;
        if (q + r + s != 0)
        {
            Debug.LogWarning($"Tọa độ Cube không hợp lệ: Q + R + S = {q + r + s} (phải bằng 0)");
        }
    }

    // Vector chỉ phương của 6 ô liền kề xung quanh theo hệ Axial (Pointed-Top)
    public static readonly HexCoordinates[] Directions = new HexCoordinates[]
    {
        new HexCoordinates(1, 0),   // Hướng 0: Phải - Trên
        new HexCoordinates(1, -1),  // Hướng 1: Phải - Dưới
        new HexCoordinates(0, -1),  // Hướng 2: Dưới
        new HexCoordinates(-1, 0),  // Hướng 3: Trái - Dưới
        new HexCoordinates(-1, 1),  // Hướng 4: Trái - Trên
        new HexCoordinates(0, 1)    // Hướng 5: Trên
    };

    // Lấy tọa độ của ô láng giềng theo hướng index từ 0 đến 5
    public HexCoordinates GetNeighbor(int directionIndex)
    {
        int index = ((directionIndex % 6) + 6) % 6; // Đảm bảo index luôn từ 0 đến 5
        return this + Directions[index];
    }

    // Tính khoảng cách giữa 2 ô lục giác bất kỳ (số bước đi tối thiểu)
    public static int Distance(HexCoordinates a, HexCoordinates b)
    {
        return (Mathf.Abs(a.Q - b.Q) + Mathf.Abs(a.R - b.R) + Mathf.Abs(a.S - b.S)) / 2;
    }

    public int DistanceTo(HexCoordinates other)
    {
        return Distance(this, other);
    }

    // Xoay tọa độ 60 độ theo chiều kim đồng hồ quanh gốc (0,0) n lần (n từ 0 đến 5)
    public HexCoordinates Rotate60Clockwise(int steps = 1)
    {
        steps = ((steps % 6) + 6) % 6; // Luôn nằm trong [0..5]
        int curQ = this.q;
        int curR = this.r;
        int curS = this.S;

        for (int i = 0; i < steps; i++)
        {
            int nextQ = -curR;
            int nextR = -curS;
            int nextS = -curQ;

            curQ = nextQ;
            curR = nextR;
            curS = nextS;
        }

        return new HexCoordinates(curQ, curR);
    }

    // Nạp chồng toán tử cộng trừ tọa độ
    public static HexCoordinates operator +(HexCoordinates a, HexCoordinates b)
    {
        return new HexCoordinates(a.q + b.q, a.r + b.r);
    }

    public static HexCoordinates operator -(HexCoordinates a, HexCoordinates b)
    {
        return new HexCoordinates(a.q - b.q, a.r - b.r);
    }

    public static bool operator ==(HexCoordinates a, HexCoordinates b) => a.Equals(b);
    public static bool operator !=(HexCoordinates a, HexCoordinates b) => !a.Equals(b);

    // Bắt buộc triển khai để làm Key cho Dictionary/HashSet nhanh chuẩn xác
    public bool Equals(HexCoordinates other) => q == other.q && r == other.r;
    public override bool Equals(object obj) => obj is HexCoordinates other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(q, r);

    public override string ToString() => $"Hex({Q}, {R}, {S})";
}