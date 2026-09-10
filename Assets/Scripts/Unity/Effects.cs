using System;
using System.Collections.Generic;

namespace CyberRider.Unity
{
    public sealed class Particle
    {
        public double X, Y, Vx, Vy, Life, Max, Size, Gravity, Drag;
        public string Color;
    }

    public sealed class Popup
    {
        public double X, Y, Life, Max, Scale, Vy;
        public string Text;
        public string Color;
    }

    public sealed class Ring
    {
        public double X, Y, Life, Max, Radius;
        public string Color;
    }

    /// <summary>Transient visual effects in world space: sparks, shards, explosion rings, score popups.</summary>
    public sealed class Effects
    {
        public readonly List<Particle> Particles = new List<Particle>();
        public readonly List<Popup> Popups = new List<Popup>();
        public readonly List<Ring> Rings = new List<Ring>();
        public double Shake;
        public double Flash;
        public string FlashColor = "#ffffff";
        private readonly Random _rng = new Random();

        private double Rand() => _rng.NextDouble();

        public void Spark(double x, double y, int count, string color, double speed = 2, double spread = Math.PI * 2, double dir = 0, double gravity = 0.08)
        {
            for (int i = 0; i < count; i++)
            {
                double a = dir + (Rand() - 0.5) * spread;
                double s = speed * (0.3 + Rand());
                Particles.Add(new Particle
                {
                    X = x,
                    Y = y,
                    Vx = Math.Cos(a) * s,
                    Vy = Math.Sin(a) * s,
                    Life = 0,
                    Max = 18 + Rand() * 22,
                    Size = 0.8 + Rand() * 1.4,
                    Color = color,
                    Gravity = gravity,
                    Drag = 0.96,
                });
                if (Particles.Count > 1500) Particles.RemoveAt(0);
            }
        }

        public void Shards(double x1, double y1, double x2, double y2, string color)
        {
            double len = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
            int n = Math.Max(3, Math.Min(30, (int)Math.Round(len / 6)));
            for (int i = 0; i < n; i++)
            {
                double t = (i + Rand()) / n;
                Spark(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, 2, color, 1.2, Math.PI * 2, 0, 0.12);
            }
        }

        public void AddRing(double x, double y, double radius, string color, int frames = 24)
        {
            Rings.Add(new Ring { X = x, Y = y, Life = 0, Max = frames, Radius = radius, Color = color });
        }

        public void AddPopup(double x, double y, string text, string color, double scale = 1)
        {
            Popups.Add(new Popup { X = x, Y = y, Text = text, Life = 0, Max = 70, Color = color, Scale = scale, Vy = -0.5 });
            if (Popups.Count > 30) Popups.RemoveAt(0);
        }

        public void Explosion(double x, double y, double radius)
        {
            Spark(x, y, 60, "#ffb347", 5, Math.PI * 2, 0, 0.05);
            Spark(x, y, 40, "#ff3d7f", 3.5, Math.PI * 2, 0, 0.05);
            AddRing(x, y, radius, "#ffb347", 20);
            AddRing(x, y, radius * 0.6, "#ffffff", 12);
            Shake = Math.Max(Shake, 10);
            Flash = Math.Max(Flash, 0.5);
            FlashColor = "#ffb347";
        }

        public void Clear()
        {
            Particles.Clear();
            Popups.Clear();
            Rings.Clear();
            Shake = 0;
            Flash = 0;
        }

        /// <summary>Advance by a number of simulation frames (fractional allowed).</summary>
        public void Update(double frames)
        {
            for (int i = Particles.Count - 1; i >= 0; i--)
            {
                Particle p = Particles[i];
                p.Life += frames;
                p.Vy += p.Gravity * frames;
                p.Vx *= p.Drag;
                p.Vy *= p.Drag;
                p.X += p.Vx * frames;
                p.Y += p.Vy * frames;
                if (p.Life >= p.Max) Particles.RemoveAt(i);
            }
            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                Popup p = Popups[i];
                p.Life += frames;
                p.Y += p.Vy * frames;
                if (p.Life >= p.Max) Popups.RemoveAt(i);
            }
            for (int i = Rings.Count - 1; i >= 0; i--)
            {
                Ring r = Rings[i];
                r.Life += frames;
                if (r.Life >= r.Max) Rings.RemoveAt(i);
            }
            Shake *= Math.Pow(0.85, frames);
            if (Shake < 0.2) Shake = 0;
            Flash *= Math.Pow(0.8, frames);
            if (Flash < 0.01) Flash = 0;
        }
    }
}
