using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;

namespace CSharp_ImGui_Client
{
    public class ParticleBackgroundEngine : Canvas
    {
        private class Particle
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double Vx { get; set; }
            public double Vy { get; set; }
            public double Size { get; set; }
            public double Opacity { get; set; }
        }

        private readonly List<Particle> _particles = new();
        private readonly Random _random = new();
        private Point _mousePos = new(-1000, -1000);
        private const int MaxParticles = 65;
        private const double ConnectionDistance = 90.0;
        private const double MouseAttractDistance = 120.0;
        private bool _isInitialized;

        // Draw caching
        private readonly Brush _particleBrush;
        private readonly Pen[] _connectionPens;
        private readonly Pen[] _mousePens;

        public ParticleBackgroundEngine()
        {
            Background = Brushes.Transparent;
            
            // Initialize brushes with Cyan theme (0, 229, 255) - #00E5FF
            Color baseColor = Color.FromRgb(0, 229, 255);
            _particleBrush = new SolidColorBrush(Color.FromArgb(140, baseColor.R, baseColor.G, baseColor.B));
            _particleBrush.Freeze();

            _connectionPens = new Pen[256];
            _mousePens = new Pen[256];
            
            for (int i = 0; i < 256; i++)
            {
                var connBrush = new SolidColorBrush(Color.FromArgb((byte)i, baseColor.R, baseColor.G, baseColor.B));
                connBrush.Freeze();
                _connectionPens[i] = new Pen(connBrush, 0.6);
                _connectionPens[i].Freeze();

                var mouseBrush = new SolidColorBrush(Color.FromArgb((byte)i, 128, 243, 255)); // Lighter cyan for mouse
                mouseBrush.Freeze();
                _mousePens[i] = new Pen(mouseBrush, 0.9);
                _mousePens[i].Freeze();
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            MouseMove += OnMouseMove;
            MouseLeave += OnMouseLeave;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                InitializeParticles();
                _isInitialized = true;
            }
            CompositionTarget.Rendering += OnRendering;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        private void InitializeParticles()
        {
            double w = ActualWidth > 0 ? ActualWidth : 420;
            double h = ActualHeight > 0 ? ActualHeight : 380;

            _particles.Clear();
            for (int i = 0; i < MaxParticles; i++)
            {
                _particles.Add(new Particle
                {
                    X = _random.NextDouble() * w,
                    Y = _random.NextDouble() * h,
                    Vx = (_random.NextDouble() - 0.5) * 0.5, // Slower, more elegant movement
                    Vy = (_random.NextDouble() - 0.5) * 0.5,
                    Size = _random.NextDouble() * 2.0 + 1.0, // Slightly smaller, refined particles
                    Opacity = _random.NextDouble() * 0.4 + 0.2
                });
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _mousePos = e.GetPosition(this);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _mousePos = new Point(-1000, -1000);
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            double w = ActualWidth;
            double h = ActualHeight;

            // Handle resize/reinit check
            if (_particles.Count == 0)
            {
                InitializeParticles();
            }

            foreach (var p in _particles)
            {
                // Update physics
                p.X += p.Vx;
                p.Y += p.Vy;

                // Bounce on boundaries
                if (p.X < 0) { p.X = 0; p.Vx = -p.Vx; }
                else if (p.X > w) { p.X = w; p.Vx = -p.Vx; }

                if (p.Y < 0) { p.Y = 0; p.Vy = -p.Vy; }
                else if (p.Y > h) { p.Y = h; p.Vy = -p.Vy; }

                // Interactive mouse effect
                if (_mousePos.X > -500)
                {
                    double dx = _mousePos.X - p.X;
                    double dy = _mousePos.Y - p.Y;
                    double dist = Math.Sqrt(dx * dx + dy * dy);

                    if (dist < MouseAttractDistance)
                    {
                        // Gentle pull towards cursor
                        double force = (MouseAttractDistance - dist) / MouseAttractDistance * 0.05;
                        p.Vx += (dx / dist) * force;
                        p.Vy += (dy / dist) * force;

                        // Cap speeds to prevent flying off
                        double speed = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
                        if (speed > 1.2)
                        {
                            p.Vx = (p.Vx / speed) * 1.2;
                            p.Vy = (p.Vy / speed) * 1.2;
                        }
                    }
                }
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            // Draw lines between nearby particles (Constellations)
            int count = _particles.Count;
            for (int i = 0; i < count; i++)
            {
                var p1 = _particles[i];
                for (int j = i + 1; j < count; j++)
                {
                    var p2 = _particles[j];
                    double dx = p1.X - p2.X;
                    double dy = p1.Y - p2.Y;
                    double distSq = dx * dx + dy * dy;

                    if (distSq < ConnectionDistance * ConnectionDistance)
                    {
                        double dist = Math.Sqrt(distSq);
                        double alpha = 1.0 - (dist / ConnectionDistance);
                        byte alphaByte = (byte)(alpha * 45); // Max opacity for connections
                        
                        dc.DrawLine(_connectionPens[alphaByte], new Point(p1.X, p1.Y), new Point(p2.X, p2.Y));
                    }
                }

                // Draw lines to mouse
                if (_mousePos.X > -500)
                {
                    double dx = p1.X - _mousePos.X;
                    double dy = p1.Y - _mousePos.Y;
                    double distSq = dx * dx + dy * dy;

                    if (distSq < MouseAttractDistance * MouseAttractDistance)
                    {
                        double dist = Math.Sqrt(distSq);
                        double alpha = 1.0 - (dist / MouseAttractDistance);
                        byte alphaByte = (byte)(alpha * 70); // Max opacity for mouse connections
                        
                        dc.DrawLine(_mousePens[alphaByte], new Point(p1.X, p1.Y), _mousePos);
                    }
                }

                // Draw Particle itself
                dc.DrawEllipse(_particleBrush, null, new Point(p1.X, p1.Y), p1.Size, p1.Size);
            }
        }
    }
}
