using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public struct Quaternion
{
    public float x;
    public float y;
    public float z;
    public float w;

    public Quaternion(float x, float y, float z, float w)
    {
        this.x = x;
        this.y = y;
        this.z = z;
        this.w = w;
    }

    public static Quaternion Identity => new Quaternion(0, 0, 0, 1);

    public override string ToString()
    {
        return $"Quaternion({x}, {y}, {z}, {w})";
    }
}

public struct Vector3
{
    public float x;
    public float y;
    public float z;

    public Vector3(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public override string ToString()
    {
        return $"Vector3({x}, {y}, {z})";
    }
}

