using SpaceGame.Systems;

namespace SpaceGame.Models;

public class Starfield
{
    public struct Star
    {
        public float X, Y;
        public int Size;
        public float Brightness;
        public float Parallax;
    }

    public Star[] Stars;
    private readonly Random _rng = new();

    public Starfield()
    {
        Stars = new Star[300];
        for (int i = 0; i < Stars.Length; i++)
        {
            Stars[i] = new Star
            {
                X = (float)_rng.NextDouble() * 20000f - 10000f,
                Y = (float)_rng.NextDouble() * 20000f - 10000f,
                Size = _rng.Next(1, 3),
                Brightness = 0.3f + (float)_rng.NextDouble() * 0.7f,
                Parallax = 0.1f + (float)_rng.NextDouble() * 0.3f,
            };
        }
    }

    public void Update(float dt, Vector2 playerVelocity)
    {
        for (int i = 0; i < Stars.Length; i++)
        {
            Stars[i].X -= playerVelocity.X * Stars[i].Parallax * dt;
            Stars[i].Y -= playerVelocity.Y * Stars[i].Parallax * dt;

            if (Stars[i].X > 10000f) Stars[i].X -= 20000f;
            if (Stars[i].X < -10000f) Stars[i].X += 20000f;
            if (Stars[i].Y > 10000f) Stars[i].Y -= 20000f;
            if (Stars[i].Y < -10000f) Stars[i].Y += 20000f;
        }
    }
}
