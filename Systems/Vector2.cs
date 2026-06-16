namespace SpaceGame.Systems;

public struct Vector2
{
    public float X;
    public float Y;

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static readonly Vector2 Zero = new(0, 0);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2 operator *(Vector2 v, float s) => new(v.X * s, v.Y * s);
    public static Vector2 operator *(float s, Vector2 v) => new(v.X * s, v.Y * s);
    public static Vector2 operator -(Vector2 v) => new(-v.X, -v.Y);

    public float Length() => MathF.Sqrt(X * X + Y * Y);

    public Vector2 Normalized()
    {
        float len = Length();
        if (len < 0.0001f) return Zero;
        return new Vector2(X / len, Y / len);
    }

    public static Vector2 FromAngle(float radians) =>
        new(MathF.Cos(radians), MathF.Sin(radians));

    public static float Distance(Vector2 a, Vector2 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
